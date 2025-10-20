using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using HWInventory.Api.Models;
using Xunit;

namespace HWInventory.Api.IntegrationTests;

public class HandoverSettingsTests : IntegrationTestBase
{
    public HandoverSettingsTests(ApiWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Get_ReturnsEmptyDefaults_WhenNoConfigurationSaved()
    {
        var response = await Client.GetAsync("/api/settings/handover");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<HandoverConfigurationResponse>();
        Assert.NotNull(payload);
        Assert.Empty(payload!.DefaultTo);
        Assert.Empty(payload.DefaultCc);
        Assert.Empty(payload.DefaultBcc);
        Assert.Null(payload.DefaultSubject);
        Assert.Null(payload.DefaultBody);
        Assert.False(payload.UseMinimalPdf);
    }

    [Fact]
    public async Task Put_PersistsConfiguration_AndNormalizesValues()
    {
        var request = new HandoverConfigurationRequest(
            DefaultTo: new List<string> { "owner@example.com", "Owner@example.com" },
            DefaultCc: new List<string> { " cc@example.com " },
            DefaultBcc: new List<string> { "", "bcc@example.com" },
            DefaultSubject: "  Předání zařízení {Name}  ",
            DefaultBody: "Dobrý den {Name}, {OldLocation} → {NewLocation}",
            PdfLogoBase64: null,
            UseMinimalPdf: true,
            PdfFooterNote: "  Interní evidenční systém  ");

        var putResponse = await Client.PutAsJsonAsync("/api/settings/handover", request);
        putResponse.EnsureSuccessStatusCode();

        var saved = await putResponse.Content.ReadFromJsonAsync<HandoverConfigurationResponse>();
        Assert.NotNull(saved);
        Assert.Equal(new[] { "owner@example.com" }, saved!.DefaultTo);
        Assert.Equal(new[] { "cc@example.com" }, saved.DefaultCc);
        Assert.Equal(new[] { "bcc@example.com" }, saved.DefaultBcc);
        Assert.Equal("Předání zařízení {Name}", saved.DefaultSubject);
        Assert.Equal("Dobrý den {Name}, {OldLocation} → {NewLocation}", saved.DefaultBody);
        Assert.True(saved.UseMinimalPdf);
        Assert.Equal("Interní evidenční systém", saved.PdfFooterNote);

        var getResponse = await Client.GetAsync("/api/settings/handover");
        getResponse.EnsureSuccessStatusCode();
        var roundTrip = await getResponse.Content.ReadFromJsonAsync<HandoverConfigurationResponse>();
        Assert.NotNull(roundTrip);
        Assert.Equal(saved.DefaultTo, roundTrip!.DefaultTo);
        Assert.Equal(saved.DefaultCc, roundTrip.DefaultCc);
        Assert.Equal(saved.DefaultBcc, roundTrip.DefaultBcc);
    }

    [Fact]
    public async Task Put_ReturnsBadRequest_WhenMoreThanThreeAddressesProvided()
    {
        var request = new HandoverConfigurationRequest(
            DefaultTo: new List<string> { "a@example.com", "b@example.com", "c@example.com", "d@example.com" },
            DefaultCc: null,
            DefaultBcc: null,
            DefaultSubject: null,
            DefaultBody: null,
            PdfLogoBase64: null,
            UseMinimalPdf: false,
            PdfFooterNote: null);

        var response = await Client.PutAsJsonAsync("/api/settings/handover", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
