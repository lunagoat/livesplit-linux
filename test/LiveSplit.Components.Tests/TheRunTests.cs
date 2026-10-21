using LiveSplit.UI.Components;
using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Xunit;
namespace LiveSplit.Components.Tests;

public class TheRunTests
{
    [Fact]
    public async Task ValidateKeyThroughMockHttpAndExposeViewState()
    {
        var handler = new StubHttpHandler(request =>
        {
            Assert.EndsWith("/validate/test%2Fkey", request.RequestUri.AbsoluteUri);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"result":{"data":{"username":"runner","picture":"https://example.test/avatar.png"}}}""") };
        });
        using var client = new HttpClient(handler);
        var settings = new CollectorSettings(client, "/no/such/test-key") { UploadKey = "test/key" };
        await settings.ValidateKeyAsync();
        Assert.Equal(CollectorSettings.ConnectionStatus.Connected, settings.Status);
        Assert.Equal("runner", settings.ValidatedUsername);
        Assert.Equal("https://therun.gg/runner", settings.ProfileUrl);
        Assert.Equal("https://therun.gg/live/runner", settings.LiveUrl);
        Assert.True(settings.CanValidate);
        settings.UploadKey = "";
        await settings.ValidateKeyAsync();
        Assert.Null(settings.ValidatedUsername);
        Assert.Equal(CollectorSettings.ConnectionStatus.None, settings.Status);
    }

    [Fact]
    public async Task ExposeInvalidKeyAndConnectionFailureWithoutNetwork()
    {
        using var invalid = new HttpClient(new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)));
        var settings = new CollectorSettings(invalid, "/no/such/test-key") { UploadKey = "invalid" };
        await settings.ValidateKeyAsync();
        Assert.Equal("Invalid upload key.", settings.StatusMessage);
        using var failure = new HttpClient(new StubHttpHandler(_ => throw new HttpRequestException("offline")));
        settings = new CollectorSettings(failure, "/no/such/test-key") { UploadKey = "key" };
        await settings.ValidateKeyAsync();
        Assert.Equal("Could not reach therun.gg.", settings.StatusMessage);
        Assert.True(settings.CanValidate);
    }

    [Fact]
    public void RoundtripSettingsWithoutPersistingSecretOrMakingRequests()
    {
        using var client = new HttpClient(new StubHttpHandler(_ => throw new Exception("Unexpected request")));
        var settings = new CollectorSettings(client, "/no/such/test-key") { IsStatsUploadingEnabled = false, IsUploadOnResetEnabled = false, IsLiveTrackingEnabled = false, IsLayoutPathUploadEnabled = true, UploadKey = "secret" };
        string xml = settings.GetSettings(new XmlDocument()).OuterXml;
        Assert.DoesNotContain("secret", xml);
        var loaded = new CollectorSettings(client, "/no/such/test-key");
        var doc = new XmlDocument(); doc.LoadXml(xml); loaded.SetSettings(doc.DocumentElement);
        Assert.Equal(xml, loaded.GetSettings(new XmlDocument()).OuterXml);
        Assert.Equal(settings.GetSettingsHashCode(), loaded.GetSettingsHashCode());
        doc.LoadXml("<Settings />"); loaded.SetSettings(doc.DocumentElement);
        Assert.True(loaded.IsStatsUploadingEnabled); Assert.False(loaded.IsLayoutPathUploadEnabled);
    }
}

internal sealed class StubHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
{
    protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken token) => response(request);
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => Task.FromResult(response(request));
}
