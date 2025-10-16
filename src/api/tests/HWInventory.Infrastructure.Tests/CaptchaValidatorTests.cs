using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using HWInventory.Domain.Entities;
using HWInventory.Infrastructure.Persistence;
using HWInventory.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HWInventory.Infrastructure.Tests;

public class CaptchaValidatorTests
{
    [Fact]
    public async Task ValidateAsync_ReturnsTrue_WhenDisabled()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var dbContext = new AppDbContext(options);
        dbContext.Settings.Add(new AppSetting
        {
            Section = "Security.Captcha",
            Key = "Enabled",
            Value = "false",
            CreatedBy = "tests",
            ModifiedBy = "tests"
        });
        await dbContext.SaveChangesAsync();

        var validator = new CaptchaValidator(dbContext, new StubHttpClientFactory(new HttpClient()), NullLogger<CaptchaValidator>.Instance);

        var result = await validator.ValidateAsync("anything", CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task ValidateAsync_CallsEndpoint_WhenEnabled()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var dbContext = new AppDbContext(options);
        dbContext.Settings.AddRange(
            new AppSetting
            {
                Section = "Security.Captcha",
                Key = "Enabled",
                Value = "true",
                CreatedBy = "tests",
                ModifiedBy = "tests"
            },
            new AppSetting
            {
                Section = "Security.Captcha",
                Key = "VerificationEndpoint",
                Value = "https://captcha.local/verify",
                CreatedBy = "tests",
                ModifiedBy = "tests"
            });
        await dbContext.SaveChangesAsync();

        var handler = new StubHttpMessageHandler((request, _) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("https://captcha.local/verify", request.RequestUri!.ToString());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { success = true })
            });
        });

        var validator = new CaptchaValidator(dbContext, new StubHttpClientFactory(new HttpClient(handler)), NullLogger<CaptchaValidator>.Instance);

        var result = await validator.ValidateAsync("token", CancellationToken.None);

        Assert.True(result);
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;

        public StubHttpClientFactory(HttpClient client)
        {
            _client = client;
        }

        public HttpClient CreateClient(string name) => _client;
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        public StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => _handler(request, cancellationToken);
    }
}
