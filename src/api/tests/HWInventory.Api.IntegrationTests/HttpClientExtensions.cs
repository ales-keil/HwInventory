using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace HWInventory.Api.IntegrationTests;

internal static class HttpClientExtensions
{
    public static async Task EnsureLoginAsync(this HttpClient client, string userName, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            UserNameOrEmail = userName,
            Password = password,
            RememberMe = false
        });

        response.EnsureSuccessStatusCode();
    }
}
