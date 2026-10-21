using LiveSplit.Model;
using LiveSplit.Model.Comparisons;
using LiveSplit.Options;
using LiveSplit.Options.SettingsFactories;
using LiveSplit.Hosting;
using LiveSplit.Server;
using LiveSplit.UI;
using LiveSplit.UI.Components;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace LiveSplit.Tests.Server;

[Collection("Environment")]
public class CommandServerMust : IDisposable
{
    private readonly CommandServer server;
    private readonly int port;
    private readonly string runtimeDirectory = Path.Combine(Path.GetTempPath(), "livesplit-server-" + Guid.NewGuid().ToString("N"));
    private readonly string previousRuntimeDirectory = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");

    public CommandServerMust()
    {
        ComponentManager.RaceProviderFactories ??= new Dictionary<string, IRaceProviderFactory>();
        Directory.CreateDirectory(runtimeDirectory);
        Environment.SetEnvironmentVariable("XDG_RUNTIME_DIR", runtimeDirectory);

        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        ISettings settings = new StandardSettingsFactory().Create();
        settings.ServerPort = port;
        var run = new Run(new StandardComparisonGeneratorsFactory());
        run.Add(new Segment("Split"));
        var state = new LiveSplitState(run, null, new Layout(), new LayoutSettings(), settings);
        server = new CommandServer(state, () => { }, () => null, _ => true, (_, _) => true, (_, _) => true, (_, _) => true);
    }

    public void Dispose()
    {
        server.StopAll();
        Environment.SetEnvironmentVariable("XDG_RUNTIME_DIR", previousRuntimeDirectory);
        Directory.Delete(runtimeDirectory, true);
    }

    private static string Exchange(Stream stream, string command)
    {
        var writer = new StreamWriter(stream, new UTF8Encoding(false)) { NewLine = "\n", AutoFlush = true };
        var reader = new StreamReader(stream, Encoding.UTF8);
        writer.WriteLine(command);
        Task<string> line = reader.ReadLineAsync();
        Assert.True(line.Wait(TimeSpan.FromSeconds(5)), "no response from the server");
        return line.Result;
    }

    [Fact]
    public void AnswerCommandsOverTcp()
    {
        server.StartTcp();

        using var client = new TcpClient();
        client.Connect(IPAddress.Loopback, port);
        NetworkStream stream = client.GetStream();

        Assert.Equal("pong", Exchange(stream, "ping"));
        Assert.Equal("NotRunning", Exchange(stream, "getcurrenttimerphase"));
        Assert.Equal(ServerStateType.TCP, server.ServerState);
    }

    [Fact]
    public void ControlTheTimerOverTcp()
    {
        server.StartTcp();

        using var client = new TcpClient();
        client.Connect(IPAddress.Loopback, port);
        NetworkStream stream = client.GetStream();

        var writer = new StreamWriter(stream, new UTF8Encoding(false)) { NewLine = "\n", AutoFlush = true };
        writer.WriteLine("starttimer");

        Assert.Equal("Running", WaitFor(() => Exchange(stream, "getcurrenttimerphase"), "Running"));
        writer.WriteLine("reset");
        Assert.Equal("NotRunning", WaitFor(() => Exchange(stream, "getcurrenttimerphase"), "NotRunning"));
    }

    private static string WaitFor(Func<string> read, string expected)
    {
        string value = null;
        for (int i = 0; i < 50; i++)
        {
            value = read();
            if (value == expected)
            {
                break;
            }

            Thread.Sleep(20);
        }

        return value;
    }

    [Fact]
    public void AnswerCommandsOverTheUnixSocket()
    {
        server.StartNamedPipe();

        string path = Paths.CommandSocketPath;
        Assert.True(File.Exists(path));
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));

        using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        socket.Connect(new UnixDomainSocketEndPoint(path));
        using var stream = new NetworkStream(socket);

        Assert.Equal("pong", Exchange(stream, "ping"));

        server.StopPipe();
        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task AnswerCommandsOverWebSockets()
    {
        server.StartWs();

        using var client = new ClientWebSocket();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await client.ConnectAsync(new Uri($"ws://127.0.0.1:{port}/livesplit"), cancellation.Token);

        await client.SendAsync(Encoding.UTF8.GetBytes("ping"), WebSocketMessageType.Text, true, cancellation.Token);
        var buffer = new byte[256];
        WebSocketReceiveResult result = await client.ReceiveAsync(buffer, cancellation.Token);

        Assert.Equal("pong", Encoding.UTF8.GetString(buffer, 0, result.Count));
        Assert.Equal(ServerStateType.Websocket, server.ServerState);
        client.Abort();
    }
}
