using LiveSplit.Model;
using LiveSplit.Racetime.Controller;
using LiveSplit.Racetime.Model;
using LiveSplit.Racetime.View;
using LiveSplit.UI.Components;
using LiveSplit.Web;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using LiveSplit.Hosting;
using System.Threading.Tasks;

namespace LiveSplit.Racetime;

public class RacetimeAPI : RaceProviderAPI
{
    protected static readonly Uri BaseUri = new($"{Properties.Resources.PROTOCOL_REST}://{Properties.Resources.DOMAIN}/");
    protected static string racesEndpoint => Properties.Resources.ENDPOINT_RACES;

    public static RacetimeAPI Instance => field ??= new();

    public event Action<ChannelForm> ChannelOpened;
    public HttpClient HttpClient { get; }

    public RacetimeAPI(HttpClient client = null)
    {
        HttpClient = client ?? new HttpClient();
        Authenticator = new RacetimeAuthenticator(new RTAuthentificationSettings(), HttpClient);
        JoinRace = Join;
        CreateRace = Create;
    }

    public void Join(ITimerModel model, string id)
    {
        var channel = new RacetimeChannel(model.CurrentState, model, (RacetimeSettings)Settings);
        var view = new ChannelForm(channel, id, model.CurrentState.LayoutSettings.AlwaysOnTop);
        ChannelOpened?.Invoke(view);
        view.Connect(id);
    }

    public void Warn()
    {

    }

    public void Create(ITimerModel model)
    {
        PlatformServices.Current.OpenUrl(GetUri(Properties.Resources.CREATE_RACE_ADDRESS).AbsoluteUri);
    }

    public IEnumerable<Race> Races { get; set; }

    internal RacetimeAuthenticator Authenticator { get; set; }

    public override string ProviderName => "racetime.gg";

    public override string Username => Authenticator.Identity?.Name;

    protected Uri GetUri(string subUri)
    {
        return new Uri(BaseUri, subUri);
    }

    public override void RefreshRacesListAsync()
    {
        Task.Factory.StartNew(RefreshRacesList);
    }

    protected void RefreshRacesList()
    {
        try
        {
            Races = GetRacesFromServer().ToArray();
            RacesRefreshedCallback?.Invoke(this);
        }
        catch { }
    }

    protected IEnumerable<Race> GetRacesFromServer()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(BaseUri.AbsoluteUri + racesEndpoint));
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Authenticator.AccessToken);
        using var response = HttpClient.Send(request);
        response.EnsureSuccessStatusCode();
        dynamic data = JSON.FromString(response.Content.ReadAsStringAsync().GetAwaiter().GetResult());

        dynamic races = data.races;
        foreach (dynamic r in races)
        {
            Race raceObj;
            r.entrants = new List<dynamic>();
            raceObj = RTModelBase.Create<Race>(r);
            yield return raceObj;
        }

        yield break;
    }

    public override IEnumerable<IRaceInfo> GetRaces()
    {
        return Races;
    }
}
