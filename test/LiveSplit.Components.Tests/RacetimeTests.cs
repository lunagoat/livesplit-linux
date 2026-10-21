using LiveSplit.Model;
using LiveSplit.Racetime;
using LiveSplit.Racetime.Controller;
using LiveSplit.Racetime.View;
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml;
using Xunit;
namespace LiveSplit.Components.Tests;
public class RacetimeTests
{
    [Fact]
    public void RoundtripLegacyHiddenSettingsAndClone()
    {
        var settings = new RacetimeSettings { LoadChatHistory = false, HideResults = true };
        var xml = settings.ToXml(new XmlDocument());
        var loaded = new RacetimeSettings(); loaded.FromXml(xml, new Version(1, 0));
        Assert.Equal(xml.OuterXml, loaded.ToXml(new XmlDocument()).OuterXml);
        Assert.Equal(xml.OuterXml, ((RacetimeSettings)settings.Clone()).ToXml(new XmlDocument()).OuterXml);
        Assert.IsType<RacetimeSettingsControl>(loaded.GetSettingsControl());
    }
    [Fact]
    public void CreateProtocolCommandsAndChannelViewWithoutConnecting()
    {
        var state = TestSupport.CreateState();
        var channel = new RacetimeChannel(state, new TimerModel { CurrentState = state }, new RacetimeSettings());
        using var view = new ChannelForm(channel, "category/room", false);
        Assert.Equal("Connecting to room", view.Title);
        Assert.Contains("ready", channel.CreateCommand(".ready").ToString());
        Assert.Contains("message", channel.CreateCommand("hello \"runner\"").ToString());
        view.PageLoaded(false); Assert.Equal("Error loading page.", view.LoadingMessage);
        view.PageLoaded(true); Assert.True(view.IsPageVisible);
    }
    [Fact]
    public async Task TokenEndpointAndUserInfoUseInjectedTransport()
    {
        using var client = new HttpClient(new StubHttpHandler(request =>
        {
            Assert.Equal("example.test", request.RequestUri.Host);
            if (request.Method == HttpMethod.Post)
            {
                Assert.Equal("/token", request.RequestUri.AbsolutePath);
                return new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("{\"error\":\"invalid_grant\"}") };
            }
            Assert.Equal("Bearer token", request.Headers.Authorization.ToString());
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"id\":\"user-id\",\"name\":\"runner\"}") };
        }));
        var auth = new RacetimeAuthenticator(new AuthSettings(), client);
        var result = await auth.RestRequest("/token", "grant_type=refresh_token");
        Assert.Equal(400, result.Item1);
        Assert.Equal("runner", auth.GetUserInfo(new AuthSettings(), "token").Name);
    }
    private sealed class AuthSettings : IAuthentificationSettings
    {
        public string ClientID => "test";
        public string ClientSecret => "";
        public string AuthServer => "https://example.test";
        public string SuccessEndpoint => "/success";
        public string FailureEndpoint => "/failure";
        public string AuthorizationEndpoint => "/authorize";
        public string TokenEndpoint => "/token";
        public string UserInfoEndpoint => "/user";
        public string RevokeEndpoint => "/revoke";
        public string Scopes => "read";
        public IPAddress RedirectAddress => IPAddress.Loopback;
        public int RedirectPort => 1234;
        public string ChallengeMethod => "S256";
    }
}
