using System.Net;
using System.Net.Sockets;
using System.Text;

namespace CcCalendar.Infrastructure.Sync;

public sealed class LoopbackOidcCallbackReceiver : IDisposable
{
    private readonly TcpListener listener;
    private readonly string callbackPath;

    public LoopbackOidcCallbackReceiver(string callbackPath = "/callback/")
    {
        this.callbackPath = callbackPath.StartsWith('/') ? callbackPath : $"/{callbackPath}";
        if (!this.callbackPath.EndsWith('/'))
        {
            this.callbackPath += "/";
        }

        listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        RedirectUri = new Uri($"http://127.0.0.1:{port}{this.callbackPath}");
    }

    public Uri RedirectUri { get; }

    public async Task<Uri> WaitAsync(CancellationToken cancellationToken)
    {
        using TcpClient client = await listener.AcceptTcpClientAsync(cancellationToken);
        await using NetworkStream stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
        string requestLine = await reader.ReadLineAsync(cancellationToken)
            ?? throw new InvalidDataException("The browser callback was empty.");
        while (!string.IsNullOrEmpty(await reader.ReadLineAsync(cancellationToken)))
        {
        }

        string[] parts = requestLine.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2 || !parts[0].Equals("GET", StringComparison.Ordinal))
        {
            throw new InvalidDataException("The browser callback was not an HTTP GET request.");
        }

        string target = parts[1];
        if (!target.StartsWith(callbackPath, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The browser callback path did not match the login request.");
        }

        const string body = "Login completed. You may close this window.";
        byte[] response = Encoding.UTF8.GetBytes(
            $"HTTP/1.1 200 OK\r\nContent-Type: text/plain; charset=utf-8\r\nContent-Length: {Encoding.UTF8.GetByteCount(body)}\r\nConnection: close\r\n\r\n{body}");
        await stream.WriteAsync(response, cancellationToken);
        return new Uri($"http://127.0.0.1:{RedirectUri.Port}{target}");
    }

    public void Dispose()
    {
        listener.Stop();
    }
}
