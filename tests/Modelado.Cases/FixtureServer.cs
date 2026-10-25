using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Modelado.Cases;

/// <summary>
/// MOD11: the download server a case talks to, on this machine's loopback, so a case can watch a
/// model download without the network (after PhotoBoost's PB59 FixtureServer), and since MOD37 a
/// stand-in for Meshy AI's API under /meshy/. It serves
/// cases/fixtures/download under /files/, answers 404 for anything else, and honours a Range request
/// as Hugging Face does. A whole file is sent no faster than <see cref="BytesPerSecond"/>, so a
/// download lasts long enough for a case to read its progress while it runs. A raw socket rather
/// than HttpListener, which needs a URL reservation off an administrator's desk.
/// </summary>
public sealed class FixtureServer : IDisposable
{
    /// <summary>The port cases/fixtures/download/manifest.json names; PhotoBoost's is 47623.</summary>
    public const int Port = 47631;

    /// <summary>How fast a whole file is sent; a Range request is not held back.</summary>
    public const int BytesPerSecond = 40 * 1024;

    private const int Chunk = 16 * 1024;

    private readonly string folder;
    private readonly TcpListener listener = new(IPAddress.Loopback, Port);
    private readonly CancellationTokenSource stopping = new();
    private readonly Task accepting;

    /// <summary>Starts serving a folder.</summary>
    /// <param name="folder">The files /files/ serves.</param>
    public FixtureServer(string folder)
    {
        this.folder = folder;
        listener.Start();
        accepting = AcceptAsync(stopping.Token);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        stopping.Cancel();
        listener.Stop();
        try
        {
            accepting.Wait(TimeSpan.FromSeconds(5));
        }
        catch (AggregateException)
        {
            // The accept loop ends by its listener being stopped under it.
        }

        stopping.Dispose();
    }

    private async Task AcceptAsync(CancellationToken stop)
    {
        while (!stop.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await listener.AcceptTcpClientAsync(stop).ConfigureAwait(false);
            }
            catch (Exception ended) when (ended is OperationCanceledException or SocketException or ObjectDisposedException)
            {
                return;
            }

            _ = ServeAsync(client, stop);
        }
    }

    private async Task ServeAsync(TcpClient client, CancellationToken stop)
    {
        using (client)
        {
            try
            {
                NetworkStream stream = client.GetStream();
                (string method, string path, long? from, string? authorization) = await ReadRequestAsync(stream, stop).ConfigureAwait(false);
                if (path.StartsWith("/meshy/", StringComparison.Ordinal))
                {
                    (int status, string json) = Meshy(method, path, authorization);
                    await WriteAsync(stream, string.Create(CultureInfo.InvariantCulture, $"HTTP/1.1 {status} Fixture\r\nContent-Type: application/json\r\nContent-Length: {Encoding.UTF8.GetByteCount(json)}\r\nConnection: close\r\n\r\n{json}"), stop).ConfigureAwait(false);
                    return;
                }

                string file = Path.Combine(folder, Path.GetFileName(path));
                if (!path.StartsWith("/files/", StringComparison.Ordinal) || !File.Exists(file))
                {
                    await WriteAsync(stream, "HTTP/1.1 404 Not Found\r\nContent-Length: 0\r\nConnection: close\r\n\r\n", stop).ConfigureAwait(false);
                    return;
                }

                byte[] bytes = await File.ReadAllBytesAsync(file, stop).ConfigureAwait(false);
                long start = from is long asked && asked < bytes.Length ? asked : 0;

                string head = start > 0
                    ? string.Create(CultureInfo.InvariantCulture, $"HTTP/1.1 206 Partial Content\r\nContent-Range: bytes {start}-{bytes.Length - 1}/{bytes.Length}\r\n")
                    : "HTTP/1.1 200 OK\r\n";
                await WriteAsync(stream, head + string.Create(CultureInfo.InvariantCulture, $"Content-Length: {bytes.Length - start}\r\nContent-Type: application/octet-stream\r\nConnection: close\r\n\r\n"), stop).ConfigureAwait(false);

                // The manifest comes at once; the stand-in model is throttled.
                bool throttle = from is null && !file.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
                for (long at = start; at < bytes.Length; at += Chunk)
                {
                    int length = (int)Math.Min(Chunk, bytes.Length - at);
                    await stream.WriteAsync(bytes.AsMemory((int)at, length), stop).ConfigureAwait(false);
                    if (throttle)
                    {
                        await Task.Delay(TimeSpan.FromSeconds((double)length / BytesPerSecond), stop).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception gone) when (gone is IOException or SocketException or OperationCanceledException or ObjectDisposedException)
            {
                // The app cancelled, or the suite ended: the connection going is the point.
            }
        }
    }

    /// <summary>
    /// MOD37: a stand-in for Meshy AI's REST API, which the app is pointed at with
    /// --cloud-api meshy=http://127.0.0.1:47631/meshy/. It takes only the case's key, and every
    /// image-to-3D task succeeds at once with the cube, for 30 credits.
    /// </summary>
    public const string MeshyKey = "msy_case_key";

    private static (int Status, string Json) Meshy(string method, string path, string? authorization)
    {
        if (authorization != $"Bearer {MeshyKey}")
        {
            return (401, """{"message": "Invalid API key"}""");
        }

        return (method, path) switch
        {
            ("GET", "/meshy/openapi/v1/balance") => (200, """{"balance": 470, "monthly_credits": 470, "permanent_credits": 0, "monthly_resets_at": null}"""),
            ("POST", "/meshy/openapi/v1/image-to-3d") => (200, """{"result": "case-image"}"""),
            ("GET", "/meshy/openapi/v1/image-to-3d/case-image") => (200, string.Create(
                CultureInfo.InvariantCulture,
                $$$"""{"id": "case-image", "status": "SUCCEEDED", "progress": 100, "consumed_credits": 30, "model_urls": {"glb": "http://127.0.0.1:{{{Port}}}/files/cube.glb"}}""")),
            _ => (404, """{"message": "Not found"}"""),
        };
    }

    private static async Task<(string Method, string Path, long? From, string? Authorization)> ReadRequestAsync(NetworkStream stream, CancellationToken stop)
    {
        StringBuilder head = new();
        byte[] one = new byte[1];
        while (!head.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal) && head.Length < 16 * 1024)
        {
            if (await stream.ReadAsync(one, stop).ConfigureAwait(false) == 0)
            {
                break;
            }

            head.Append((char)one[0]);
        }

        string[] lines = head.ToString().Split("\r\n");
        string[] request = lines[0].Split(' ');
        string method = request.Length > 0 ? request[0] : "GET";
        string path = request is [_, var target, ..] ? target : "/";
        long? from = null;
        long body = 0;
        string? authorization = null;
        foreach (string line in lines)
        {
            const string Prefix = "range: bytes=", Length = "content-length:", Auth = "authorization:";
            if (line.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)
                && long.TryParse(line[Prefix.Length..].Split('-')[0], NumberStyles.None, CultureInfo.InvariantCulture, out long start))
            {
                from = start;
            }
            else if (line.StartsWith(Length, StringComparison.OrdinalIgnoreCase))
            {
                _ = long.TryParse(line[Length.Length..].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out body);
            }
            else if (line.StartsWith(Auth, StringComparison.OrdinalIgnoreCase))
            {
                authorization = line[Auth.Length..].Trim();
            }
        }

        // A posted body is read to its end, so the answer never races the upload it answers.
        byte[] drain = new byte[16 * 1024];
        for (long left = body; left > 0;)
        {
            int read = await stream.ReadAsync(drain.AsMemory(0, (int)Math.Min(drain.Length, left)), stop).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            left -= read;
        }

        return (method, path, from, authorization);
    }

    private static Task WriteAsync(NetworkStream stream, string text, CancellationToken stop) =>
        stream.WriteAsync(Encoding.ASCII.GetBytes(text), stop).AsTask();
}
