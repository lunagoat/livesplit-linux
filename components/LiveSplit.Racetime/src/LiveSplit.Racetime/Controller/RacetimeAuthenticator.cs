using LiveSplit.Racetime.Model;
using LiveSplit.Web;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using LiveSplit.Hosting;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace LiveSplit.Racetime.Controller;

public enum AuthResult { Pending, Success, Failure, Cancelled, Stale }

public class RacetimeAuthenticator
{
    private string _verifier = string.Empty;

    protected readonly IAuthentificationSettings s;

    protected string Code { get; set; }
    private TcpListener localEndpoint;
    protected string RedirectUri => $"http://{s.RedirectAddress}:{s.RedirectPort}/";

    public string AccessToken
    {
        get => WebCredentials.RacetimeAccessToken;
        set => WebCredentials.RacetimeAccessToken = value;
    }
    public string RefreshToken
    {
        get => WebCredentials.RacetimeRefreshToken;
        set => WebCredentials.RacetimeRefreshToken = value;
    }
    public RacetimeUser Identity { get; protected set; }
    public string Error { get; protected set; }
    public DateTime TokenExpireDate { get; protected set; }
    public bool IsAuthenticated => Code != null;
    public bool IsAuthorized => AccessToken != null;

    public bool IsAuthorizing { get; set; }

    public void ResetTokens()
    {
        Code = null;
        AccessToken = null;
        TokenExpireDate = DateTime.MaxValue;
    }

    protected async Task<bool> SendRedirectAsync(TcpClient client, string target)
    {
        await Task.Run(() =>
        {
            using var writer = new StreamWriter(client.GetStream(), new UTF8Encoding(false));
            writer.WriteLine("HTTP/1.0 301 Moved Permanently");
            writer.WriteLine($"Location: {s.AuthServer}{target}");
        });
        return true;
    }

    private readonly HttpClient client;
    public RacetimeAuthenticator(IAuthentificationSettings s, HttpClient client = null)
    {
        this.s = s;
        this.client = client ?? new HttpClient();
    }

    private readonly Regex parameterRegex = new(@"(\w+)=([-_A-Z0-9]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static string ReadResponse(TcpClient client)
    {
        try
        {
            byte[] readBuffer = new byte[client.ReceiveBufferSize];

            using var inStream = new MemoryStream();
            NetworkStream stream = client.GetStream();

            while (stream.DataAvailable)
            {
                int numberOfBytesRead = stream.Read(readBuffer, 0, readBuffer.Length);
                if (numberOfBytesRead <= 0)
                {
                    break;
                }

                inStream.Write(readBuffer, 0, numberOfBytesRead);
            }

            return Encoding.UTF8.GetString(inStream.ToArray());
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string SHA256(string inputStirng)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(inputStirng);
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        byte[] hash = sha256.ComputeHash(bytes);

        string base64 = Convert.ToBase64String(hash);
        base64 = base64.Replace("+", "-");
        base64 = base64.Replace("/", "_");
        base64 = base64.Replace("=", "");
        return base64;
    }

    private static string GenerateRandomBase64Data(uint length)
    {
        using var rng = RandomNumberGenerator.Create();
        byte[] bytes = new byte[length];
        rng.GetBytes(bytes);
        string base64 = Convert.ToBase64String(bytes);
        base64 = base64.Replace("+", "-");
        base64 = base64.Replace("/", "_");
        base64 = base64.Replace("=", "");
        return base64;
    }

    private bool TryGetUserInfo()
    {
        //try to get the userinfo. if that works we are already authenticated and authorized
        if (AccessToken != null)
        {
            try
            {
                Identity = GetUserInfo(s, AccessToken);
                return Identity != null;
            }
            catch (Exception ex)
            {

                if (ex.InnerException is SocketException)
                {
                    throw;
                }

                AccessToken = null;
                return false;
            }
        }

        return false;
    }

    private async Task<bool> TryRefreshAccess()
    {
        string request;
        Tuple<int, dynamic> result;

        if (RefreshToken != null)
        {
            request = $"code={Code}&redirect_uri={RedirectUri}&client_id={s.ClientID}&client_secret={s.ClientSecret}&refresh_token={RefreshToken}&grant_type=refresh_token";

            result = await RestRequest(s.TokenEndpoint, request);
            if (result.Item1 == 200)
            {
                AccessToken = result.Item2.access_token;
                RefreshToken = result.Item2.refresh_token;
                return true;
            }
        }

        return false;
    }

    private async Task<bool> TryGetAccess()
    {
        string request;
        Tuple<int, dynamic> result;

        request = $"code={Code}&redirect_uri={RedirectUri}&client_id={s.ClientID}&code_verifier={_verifier}&client_secret={s.ClientSecret}&scope={s.Scopes}&grant_type=authorization_code";

        result = await RestRequest(s.TokenEndpoint, request);
        if (result.Item1 == 400)
        {
            Error = "Access has been revoked. Reauthentication required";
            return false;
        }

        if (result.Item1 != 200)
        {
            Error = "Authentication successful, but access wasn't granted by the server";
            return false;
        }

        AccessToken = result.Item2.access_token;
        RefreshToken = result.Item2.refresh_token;
        TokenExpireDate = DateTime.Now.AddSeconds(result.Item2.expires_in);

        if (AccessToken == null || RefreshToken == null)
        {
            Error = "Final access check failed. Server responded with success, but hasn't delivered a valid Token.";
            return false;
        }

        return true;
    }

    public void StopPendingAuthRequest()
    {
        IsAuthorizing = false;
    }

    public void StopLocalEndpoint()
    {
        if (localEndpoint != null)
        {
            localEndpoint.Server.Close(0);
            localEndpoint.Server.Dispose();
            localEndpoint.Stop();
            localEndpoint = null;
        }
    }

    private async Task<int> TryGetAuthenticated()
    {
        string reqState, state, challenge, request, response;

        _verifier = GenerateRandomBase64Data(32);

        Error = null;
        reqState = null;
        state = GenerateRandomBase64Data(32);

        challenge = SHA256(_verifier);

        try
        {
            StopLocalEndpoint();
            localEndpoint = new TcpListener(s.RedirectAddress, s.RedirectPort);
            localEndpoint.Start();

            request = $"{s.AuthServer}{s.AuthorizationEndpoint}?response_type=code&client_id={s.ClientID}&scope={s.Scopes}&redirect_uri={RedirectUri}&state={state}&code_challenge={challenge}&code_challenge_method={s.ChallengeMethod}";

            Task<TcpClient> serverConnectionTask = localEndpoint.AcceptTcpClientAsync();

            PlatformServices.Current.OpenUrl(request);

            using TcpClient serverConnection = await serverConnectionTask;
            response = ReadResponse(serverConnection);

            StopLocalEndpoint();

            foreach (Match m in parameterRegex.Matches(response))
            {
                switch (m.Groups[1].Value)
                {
                    case "state": reqState = m.Groups[2].Value; break;
                    case "code": Code = m.Groups[2].Value; break;
                    case "error": Error = m.Groups[2].Value; break;
                }
            }

            if (Error != null)
            {
                if (Error == "invalid_token" && RefreshToken != null)
                {
                    Error = "Access Token expired";
                    return 401;
                }
                else
                {
                    await SendRedirectAsync(serverConnection, s.FailureEndpoint);
                    Error = "Unable to authenticate: The server rejected the request";
                    return 403;
                }
            }

            if (state != reqState)
            {
                await SendRedirectAsync(serverConnection, s.FailureEndpoint);
                Error = "Unable to authenticate: The server hasn't responded correctly. Possible protocol error";
                return 400;
            }

            await SendRedirectAsync(serverConnection, s.SuccessEndpoint);
            serverConnection.Close();

            StopLocalEndpoint();
        }
        catch (ObjectDisposedException)
        {
            return 404;
        }
        catch
        {
            Error = "Unknown Error";
            StopLocalEndpoint();
        }

        return Error == null ? 200 : 500;
    }

    public async Task<AuthResult> Authorize()
    {
        if (IsAuthorizing)
        {
            return AuthResult.Pending;
        }

        IsAuthorizing = true;
        Error = null;
        bool secondRun = false;

    start:

        //1st: Try to get User information
        try
        {
            if (TryGetUserInfo())
            {
                goto success;
            }
        }
        catch (SocketException)
        {
            goto failure;
        }
        catch { }

        //2nd: if this fails, try to renew access
        try
        {
            //if there is a refresh token
            if (await TryRefreshAccess())
            {
                goto start;
            }

            //or not
            if (await TryGetAccess())
            {
                goto start;
            }
        }
        catch { }

        //3rd: everything failed, ask the user to authenticate
        try
        {
            int errorcode = await TryGetAuthenticated();
            switch (errorcode)
            {
                case 403: goto cancelled;
                case 200: goto start;
                case 404: return AuthResult.Stale;
                default: goto failure;
            }
        }
        catch { }

        //safety safe for a theoretical endless loop
        if (secondRun)
        {
            goto failure;
        }

        secondRun = true;

        if (Error == null)
        {
            goto start;
        }

    failure:
        StopPendingAuthRequest();
        ResetTokens();
        return AuthResult.Failure;
    cancelled:
        StopPendingAuthRequest();
        return AuthResult.Cancelled;
    success:
        StopPendingAuthRequest();
        return AuthResult.Success;
    }

    public RacetimeUser GetUserInfo(IAuthentificationSettings s, string AccessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{s.AuthServer}{s.UserInfoEndpoint}");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AccessToken);
        using var response = client.Send(request);
        response.EnsureSuccessStatusCode();
        dynamic userdata = JSON.FromString(response.Content.ReadAsStringAsync().GetAwaiter().GetResult());
        return RTModelBase.Create<RacetimeUser>(userdata);
    }

    public void UpdateUserInfo()
    {
        Identity = GetUserInfo(s, AccessToken);
    }

    public async Task<Tuple<int, dynamic>> RestRequest(string endpoint, string body)
    {
        string state = GenerateRandomBase64Data(32);
        body += "&state=" + state;
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{s.AuthServer}{endpoint}");
        request.Content = new StringContent(body, Encoding.UTF8, "application/x-www-form-urlencoded");
        using var response = await client.SendAsync(request);
        dynamic data = JSON.FromString(await response.Content.ReadAsStringAsync());
        return new Tuple<int, dynamic>((int)response.StatusCode, data);
    }
}
