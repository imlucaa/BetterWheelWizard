using System.IO.Pipes;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace WheelWizard.DiscordPresence;

/// <summary>
/// Minimal client for Discord's local RPC socket, enough to set and clear one Rich Presence activity.
/// It talks to the Discord desktop app, so it never touches the game or the pack.
/// </summary>
public sealed class DiscordIpcClient : IDisposable
{
    private const int OpHandshake = 0;
    private const int OpFrame = 1;
    private Stream? _stream;

    public static async Task<DiscordIpcClient> ConnectAsync(string clientId, CancellationToken cancellationToken)
    {
        var client = new DiscordIpcClient();
        try
        {
            client._stream = await OpenStreamAsync(cancellationToken);
            await client.SendAsync(OpHandshake, JsonSerializer.Serialize(new { v = 1, client_id = clientId }), cancellationToken);
            await client.ReadAsync(cancellationToken);
            return client;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    public Task SetActivityAsync(DiscordActivity activity, CancellationToken cancellationToken) =>
        SendActivityAsync(activity, cancellationToken);

    public Task ClearActivityAsync(CancellationToken cancellationToken) => SendActivityAsync(null, cancellationToken);

    private async Task SendActivityAsync(DiscordActivity? activity, CancellationToken cancellationToken)
    {
        var payload = new
        {
            cmd = "SET_ACTIVITY",
            args = new { pid = Environment.ProcessId, activity = activity?.ToPayload() },
            nonce = Guid.NewGuid().ToString(),
        };
        await SendAsync(OpFrame, JsonSerializer.Serialize(payload), cancellationToken);
        await ReadAsync(cancellationToken);
    }

    private async Task SendAsync(int opcode, string json, CancellationToken cancellationToken)
    {
        var body = Encoding.UTF8.GetBytes(json);
        var frame = new byte[8 + body.Length];
        BitConverter.TryWriteBytes(frame.AsSpan(0, 4), opcode);
        BitConverter.TryWriteBytes(frame.AsSpan(4, 4), body.Length);
        body.CopyTo(frame, 8);
        await _stream!.WriteAsync(frame, cancellationToken);
        await _stream.FlushAsync(cancellationToken);
    }

    private async Task ReadAsync(CancellationToken cancellationToken)
    {
        var header = new byte[8];
        await _stream!.ReadExactlyAsync(header, cancellationToken);
        var length = BitConverter.ToInt32(header, 4);
        if (length is < 0 or > 1 << 20)
            throw new IOException("Unexpected response from Discord.");
        await _stream.ReadExactlyAsync(new byte[length], cancellationToken);
    }

    private static async Task<Stream> OpenStreamAsync(CancellationToken cancellationToken)
    {
        for (var index = 0; index < 10; index++)
        {
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    var pipe = new NamedPipeClientStream(".", $"discord-ipc-{index}", PipeDirection.InOut, PipeOptions.Asynchronous);
                    await pipe.ConnectAsync(500, cancellationToken);
                    return pipe;
                }

                foreach (var folder in UnixSocketFolders())
                {
                    var path = Path.Combine(folder, $"discord-ipc-{index}");
                    if (!File.Exists(path))
                        continue;
                    var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                    await socket.ConnectAsync(new UnixDomainSocketEndPoint(path), cancellationToken);
                    return new NetworkStream(socket, ownsSocket: true);
                }
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested) { }
        }

        throw new IOException("Discord is not running.");
    }

    private static IEnumerable<string> UnixSocketFolders()
    {
        var runtime = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
        foreach (var root in new[] { runtime, Environment.GetEnvironmentVariable("TMPDIR"), "/tmp" })
        {
            if (string.IsNullOrEmpty(root))
                continue;
            yield return root;
            yield return Path.Combine(root, "app", "com.discordapp.Discord");
            yield return Path.Combine(root, "snap.discord");
        }
    }

    public void Dispose()
    {
        _stream?.Dispose();
        _stream = null;
    }
}

public sealed record DiscordActivity(
    string? Details,
    string? State,
    string? LargeImageKey,
    string? LargeImageText,
    long StartUnixSeconds,
    int PartySize = 0,
    int PartyMax = 0,
    string? PlayerUrl = null,
    string? RoomUrl = null
)
{
    /// <summary>The buttons shown under the status: View Player when there is a player page, View Room when in a room.</summary>
    public IReadOnlyList<DiscordButton> Buttons
    {
        get
        {
            var buttons = new List<DiscordButton>();
            if (!string.IsNullOrWhiteSpace(PlayerUrl))
                buttons.Add(new("View Player", PlayerUrl));
            if (!string.IsNullOrWhiteSpace(RoomUrl))
                buttons.Add(new("View Room", RoomUrl));
            return buttons;
        }
    }

    internal object ToPayload()
    {
        var assets = new Dictionary<string, string>();
        if (!string.IsNullOrWhiteSpace(LargeImageKey))
            assets["large_image"] = LargeImageKey;
        if (!string.IsNullOrWhiteSpace(LargeImageText))
            assets["large_text"] = LargeImageText;

        var payload = new Dictionary<string, object> { ["timestamps"] = new { start = StartUnixSeconds } };
        if (!string.IsNullOrWhiteSpace(Details))
            payload["details"] = Details;
        if (!string.IsNullOrWhiteSpace(State))
            payload["state"] = State;
        if (assets.Count > 0)
            payload["assets"] = assets;
        if (PartySize > 0 && PartyMax >= PartySize)
            payload["party"] = new { size = new[] { PartySize, PartyMax } };

        // Discord shows up to two buttons, and only to other people looking at the profile.
        if (Buttons.Count > 0)
            payload["buttons"] = Buttons.Select(button => new { label = button.Label, url = button.Url }).ToList();
        return payload;
    }
}

public sealed record DiscordButton(string Label, string Url);
