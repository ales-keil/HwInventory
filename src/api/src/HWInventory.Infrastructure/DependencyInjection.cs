using System;
using System.Threading.Tasks;
using HWInventory.Application.Abstractions;
using HWInventory.Domain.Entities;
using HWInventory.Domain.Enums;
using HWInventory.Domain.Security;
using HWInventory.Infrastructure.Jobs;
using HWInventory.Infrastructure.Labels;
using HWInventory.Infrastructure.Persistence;
using HWInventory.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace HWInventory.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = "Server=(localdb)\\MSSQLLocalDB;Database=HWInventory;Trusted_Connection=True;MultipleActiveResultSets=True;TrustServerCertificate=True";
        }

        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseSqlServer(connectionString, sql =>
            {
                sql.EnableRetryOnFailure();
                sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
            });
        });

        services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<AppDbContext>());
        services.AddHttpContextAccessor();

        var identityBuilder = services.AddIdentityCore<AppUser>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Password.RequiredLength = 12;
            options.SignIn.RequireConfirmedAccount = false;
            options.User.RequireUniqueEmail = true;
            options.Tokens.AuthenticatorTokenProvider = TokenOptions.DefaultAuthenticatorProvider;
        });

        identityBuilder = new IdentityBuilder(identityBuilder.UserType, typeof(AppRole), identityBuilder.Services);
        identityBuilder.AddRoles<AppRole>();
        identityBuilder.AddEntityFrameworkStores<AppDbContext>();
        identityBuilder.AddDefaultTokenProviders();
        identityBuilder.AddSignInManager();

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = IdentityConstants.ApplicationScheme;
            options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
            options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
        }).AddIdentityCookies().AddOpenIdConnect("oidc", options =>
        {
            options.SignInScheme = IdentityConstants.ExternalScheme;
            options.ResponseType = "code";
            options.CallbackPath = "/signin-oidc";
            options.SaveTokens = true;
            options.UsePkce = true;
            options.Scope.Clear();
            options.Scope.Add("openid");
            options.Scope.Add("profile");
            options.Scope.Add("email");
        });

        services.AddOptions<OpenIdConnectOptions>("oidc").Configure<IServiceProvider>((options, serviceProvider) =>
        {
            using var scope = serviceProvider.CreateScope();
            var identityService = scope.ServiceProvider.GetRequiredService<IExternalIdentityService>();
            var configuration = identityService.GetConfigurationAsync().GetAwaiter().GetResult();

            if (configuration is null || !configuration.Enabled)
            {
                options.Authority = "https://placeholder";
                options.ClientId = "placeholder";
                options.ClientSecret = string.Empty;
                options.DisableTelemetry = true;
                options.Events ??= new OpenIdConnectEvents();
                options.Events.OnRedirectToIdentityProvider = context =>
                {
                    context.HandleResponse();
                    context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                    return Task.CompletedTask;
                };
                return;
            }

            options.Authority = configuration.Authority;
            options.ClientId = configuration.ClientId;
            options.ClientSecret = configuration.ClientSecret;
            options.ResponseType = configuration.ResponseType ?? "code";
            options.UsePkce = configuration.UsePkce;
            options.Scope.Clear();
            foreach (var scopeValue in configuration.Scopes)
            {
                options.Scope.Add(scopeValue);
            }

            options.ClaimActions.DeleteClaim("name");
            options.ClaimActions.DeleteClaim("email");
            options.ClaimActions.MapJsonKey("name", configuration.ClaimMappings.TryGetValue("name", out var nameClaim) ? nameClaim : "name");
            options.ClaimActions.MapJsonKey("email", configuration.ClaimMappings.TryGetValue("email", out var emailClaim) ? emailClaim : "email");
        });

        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = "HWInventory.Auth";
            options.SlidingExpiration = true;
            options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
            options.LoginPath = "/signin";
            options.LogoutPath = "/signout";
        });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthorizationPolicies.ServersRead, policy =>
                policy.RequireRole(SystemRoleNames.SuperAdmin, SystemRoleNames.ServerAdmin, SystemRoleNames.AppAdmin));

            options.AddPolicy(AuthorizationPolicies.ServersManage, policy =>
                policy.RequireRole(SystemRoleNames.SuperAdmin, SystemRoleNames.ServerAdmin, SystemRoleNames.AppAdmin));

            options.AddPolicy(AuthorizationPolicies.NetworkRead, policy =>
                policy.RequireRole(SystemRoleNames.SuperAdmin, SystemRoleNames.NetworkAdmin));

            options.AddPolicy(AuthorizationPolicies.NetworkManage, policy =>
                policy.RequireRole(SystemRoleNames.SuperAdmin, SystemRoleNames.NetworkAdmin));

            options.AddPolicy(AuthorizationPolicies.WorkstationsRead, policy =>
                policy.RequireRole(SystemRoleNames.SuperAdmin, SystemRoleNames.Helpdesk, SystemRoleNames.HelpdeskRead));

            options.AddPolicy(AuthorizationPolicies.WorkstationsManage, policy =>
                policy.RequireRole(SystemRoleNames.SuperAdmin, SystemRoleNames.Helpdesk));

            options.AddPolicy(AuthorizationPolicies.DictionariesManage, policy =>
                policy.RequireRole(SystemRoleNames.SuperAdmin));

            options.AddPolicy(AuthorizationPolicies.AuditRead, policy =>
                policy.RequireRole(SystemRoleNames.SuperAdmin, SystemRoleNames.ServerAdmin, SystemRoleNames.NetworkAdmin));

            options.AddPolicy(AuthorizationPolicies.LabelsManage, policy =>
                policy.RequireRole(SystemRoleNames.SuperAdmin, SystemRoleNames.Helpdesk));

            options.AddPolicy(AuthorizationPolicies.ModulesManage, policy =>
                policy.RequireRole(SystemRoleNames.SuperAdmin));

            options.AddPolicy(AuthorizationPolicies.DashboardView, policy =>
                policy.RequireRole(
                    SystemRoleNames.SuperAdmin,
                    SystemRoleNames.ServerAdmin,
                    SystemRoleNames.NetworkAdmin,
                    SystemRoleNames.AppAdmin,
                    SystemRoleNames.Helpdesk,
                    SystemRoleNames.HelpdeskRead));

            options.AddPolicy(AuthorizationPolicies.SecurityManage, policy =>
                policy.RequireRole(SystemRoleNames.SuperAdmin));

            options.AddPolicy(AuthorizationPolicies.SessionsManage, policy =>
                policy.RequireRole(SystemRoleNames.SuperAdmin));
        });

        services.AddHttpClient("captcha");
        services.AddHttpClient("sms");

        services.AddScoped<ITotpService, TotpService>();
        services.AddScoped<ILabelRenderingService, LabelRenderingService>();
        services.AddScoped<ILdapSyncService, LdapSyncService>();
        services.AddScoped<ILdapConfigurationStore, LdapConfigurationStore>();
        services.AddScoped<IExternalIdentityService, ExternalIdentityService>();
        services.AddScoped<ICaptchaConfigurationStore, CaptchaConfigurationStore>();
        services.AddScoped<ICaptchaValidator, CaptchaValidator>();
        services.AddScoped<ISmsGateway, SmsGateway>();
        services.AddScoped<ISmsConnectorStore, SmsConnectorStore>();
        services.AddScoped<IPasswordPolicyConfigurationStore, PasswordPolicyConfigurationStore>();
        services.AddScoped<ISsprService, SsprService>();
        services.AddScoped<ISsprConfigurationStore, SsprConfigurationStore>();
        services.AddScoped<ISessionTracker, SessionTracker>();

        services.AddQuartz(q =>
        {
            q.UseMicrosoftDependencyInjectionJobFactory();
            var jobKey = new JobKey("LabelPrintJobProcessor");
            q.AddJob<LabelPrintJobProcessor>(options => options.WithIdentity(jobKey));
            q.AddTrigger(trigger => trigger
                .ForJob(jobKey)
                .WithIdentity("LabelPrintJobProcessor-trigger")
                .WithSimpleSchedule(x => x.WithIntervalInSeconds(30).RepeatForever()));
        });

        services.AddQuartzHostedService(options =>
        {
            options.WaitForJobsToComplete = true;
        });

        return services;
    }
}
