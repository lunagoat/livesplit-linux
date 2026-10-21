using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
namespace LiveSplit.Racetime.Controller;
public interface IRacetimeSocket : IDisposable
{
    WebSocketState State { get; }
    void SetRequestHeader(string name, string value);
    Task ConnectAsync(Uri uri, CancellationToken token);
    Task SendAsync(ArraySegment<byte> data, WebSocketMessageType type, bool end, CancellationToken token);
    Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> data, CancellationToken token);
}
public sealed class RacetimeSocket : IRacetimeSocket
{
    private readonly ClientWebSocket socket = new();
    public WebSocketState State => socket.State;
    public void SetRequestHeader(string name, string value) => socket.Options.SetRequestHeader(name, value);
    public Task ConnectAsync(Uri uri, CancellationToken token) => socket.ConnectAsync(uri, token);
    public Task SendAsync(ArraySegment<byte> data, WebSocketMessageType type, bool end, CancellationToken token) => socket.SendAsync(data, type, end, token);
    public Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> data, CancellationToken token) => socket.ReceiveAsync(data, token);
    public void Dispose() => socket.Dispose();
}
