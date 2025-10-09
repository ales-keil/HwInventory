using System.Text.Json.Serialization;
using HWInventory.Api.Middleware;
using HWInventory.Application.Abstractions;
using HWInventory.Infrastructure;
using HWInventory.Infrastructure.Observability;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuestPDF.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

QuestPDF.Settings.License = LicenseType.Community;

builder.Services.AddInfrastructure(builder.Configuration);
builder.Logging.AddFilter((_, _, level) => level >= ObservabilityLogging.MinimumLevel);
builder.Services.AddHealthChecks();
builder.Services.AddControllers(options =>
{
    options.Filters.Add(new ProducesResponseTypeAttribute(typeof(ProblemDetails), StatusCodes.Status400BadRequest));
    options.Filters.Add(new ProducesResponseTypeAttribute(typeof(ProblemDetails), StatusCodes.Status404NotFound));
}).AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("default", policy =>
    {
        policy.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin();
    });
});

var app = builder.Build();

await app.InitializeDatabaseAsync();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("default");
app.UseRouting();
app.UseCorrelationIds();
app.UseRequestMetrics();
app.UseAuthentication();
app.UseSessionTracking();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/health", async ([FromServices] IObservabilityRuntime runtime, CancellationToken cancellationToken) =>
    {
        var configuration = await runtime.GetAsync(cancellationToken);
        if (!configuration.HealthEndpointEnabled)
        {
            return Results.StatusCode(StatusCodes.Status404NotFound);
        }

        return Results.Ok(new { status = "Healthy" });
    })
    .WithMetadata(new AllowAnonymousAttribute());

app.MapGet("/metrics", async ([FromServices] IObservabilityRuntime runtime, [FromServices] IRequestMetricsCollector collector, CancellationToken cancellationToken) =>
    {
        var configuration = await runtime.GetAsync(cancellationToken);
        if (!configuration.MetricsEndpointEnabled)
        {
            return Results.StatusCode(StatusCodes.Status404NotFound);
        }

        var snapshot = collector.ExportSnapshot();
        return Results.Text(snapshot, "text/plain");
    })
    .WithMetadata(new AllowAnonymousAttribute());

await app.RunAsync();
