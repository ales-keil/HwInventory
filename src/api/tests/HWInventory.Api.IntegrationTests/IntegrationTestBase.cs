using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using HWInventory.Api.Models;
using HWInventory.Domain.Entities;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace HWInventory.Api.IntegrationTests;

public abstract class IntegrationTestBase : IClassFixture<ApiWebApplicationFactory>, IAsyncLifetime
{
    protected IntegrationTestBase(ApiWebApplicationFactory factory)
    {
        Factory = factory;
        Client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
    }

    protected ApiWebApplicationFactory Factory { get; }

    protected HttpClient Client { get; }

    public virtual async Task InitializeAsync()
    {
        await Client.EnsureLoginAsync(TestCredentials.Username, TestCredentials.Password);
    }

    public virtual Task DisposeAsync()
    {
        Client.Dispose();
        return Task.CompletedTask;
    }

    protected async Task<Guid> EnsureDictionaryEntryAsync(string dictType, string fallbackKey, string fallbackValue)
    {
        var entries = await Client.GetFromJsonAsync<List<DictionaryEntry>>($"/api/dictionaries?type={dictType}") ?? new();
        if (entries.Count > 0)
        {
            if (!string.IsNullOrWhiteSpace(fallbackKey))
            {
                var match = entries.FirstOrDefault(x => string.Equals(x.Key, fallbackKey, StringComparison.OrdinalIgnoreCase));
                if (match is not null)
                {
                    return match.Id;
                }
            }

            return entries[0].Id;
        }

        var payload = new DictionaryEntryRequestDto(dictType, fallbackKey, fallbackValue, null, 0);
        var createResponse = await Client.PostAsJsonAsync("/api/dictionaries", payload);
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<DictionaryEntry>();
        return created!.Id;
    }
}
