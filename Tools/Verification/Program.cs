using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Zpd.Networking;

internal static class Program
{
    private static void Check(bool condition, string name)
    {
        if (!condition)
            throw new Exception(name);
        Console.WriteLine("PASS " + name);
    }

    private static async Task<NetworkEvent> Next(NetworkClient client)
    {
        var timeout = Stopwatch.StartNew();
        while (timeout.ElapsedMilliseconds < 5000)
        {
            if (client.TryDequeue(out NetworkEvent result))
                return result;
            await Task.Delay(5);
        }
        throw new TimeoutException("No network event.");
    }

    private static async Task Main(string[] args)
    {
        byte[] fixture = PacketCodec.Encode(new Packet(7, 0x12345678, new byte[] { 0, 255 }, 17));
        Check(fixture.SequenceEqual(new byte[] { 0, 10, 7, 17, 18, 52, 86, 120, 0, 255 }), "server wire format");
        await TestPeer();
        await TestInvalidFrame();
        await TestQueueLimit();
        await TestConnectFailure();
        if (args.Length != 1)
            throw new ArgumentException("Provide the zpd-server executable path.");
        await TestServer(Path.GetFullPath(args[0]));
    }

    private static async Task TestPeer()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var client = new NetworkClient();
        client.Connect("127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port);
        using TcpClient peer = await listener.AcceptTcpClientAsync();
        Check((await Next(client)).Type == NetworkEventType.Connected, "connected event first");
        NetworkStream stream = peer.GetStream();
        byte[] response = PacketCodec.Encode(new Packet(129, 42, new byte[] { 0, 255, 128 }));
        await stream.WriteAsync(response.AsMemory(0, 3));
        await Task.Delay(30);
        Check(!client.TryDequeue(out _), "partial header waits");
        await stream.WriteAsync(response.AsMemory(3, 6));
        await Task.Delay(30);
        Check(!client.TryDequeue(out _), "partial payload waits");
        byte[] notification = PacketCodec.Encode(new Packet(197, 0, Array.Empty<byte>()));
        await stream.WriteAsync(response.Skip(9).Concat(notification).ToArray());
        Packet packet = (await Next(client)).Packet;
        Check(packet.Code == 129 && packet.RequestId == 42 && packet.Payload.SequenceEqual(new byte[] { 0, 255, 128 }), "fragmented response with changed code");
        packet = (await Next(client)).Packet;
        Check(packet.Code == 197 && packet.RequestId == 0, "coalesced server notification");
        peer.Dispose();
        Check((await Next(client)).Type == NetworkEventType.Disconnected, "peer close event");
        await client.Completion.WaitAsync(TimeSpan.FromSeconds(5));
        client.Dispose();
        Check(!client.TryDequeue(out _), "disconnect emitted once");
    }

    private static async Task TestInvalidFrame()
    {
        foreach (byte[] bytes in new[] { new byte[8], new byte[] { 16, 1, 0, 0, 0, 0, 0, 0 }, new byte[] { 0, 10, 1, 0, 0, 0, 0, 1, 3 } })
        {
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            using var client = new NetworkClient();
            client.Connect("127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port);
            using TcpClient peer = await listener.AcceptTcpClientAsync();
            await Next(client);
            await peer.GetStream().WriteAsync(bytes);
            peer.Dispose();
            Check((await Next(client)).Type == NetworkEventType.Disconnected, "invalid or truncated frame closes connection");
            await client.Completion.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    private static async Task TestQueueLimit()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var client = new NetworkClient();
        client.Connect("127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port);
        using TcpClient peer = await listener.AcceptTcpClientAsync();
        await Next(client);
        byte[] packet = PacketCodec.Encode(new Packet(1, 1, Array.Empty<byte>()));
        byte[] batch = Enumerable.Range(0, NetworkSettings.MaxQueuedEvents).SelectMany(_ => packet).ToArray();
        await peer.GetStream().WriteAsync(batch);
        await client.Completion.WaitAsync(TimeSpan.FromSeconds(5));
        int count = 0;
        NetworkEvent last = null;
        while (client.TryDequeue(out NetworkEvent item))
        {
            ++count;
            last = item;
        }
        Check(count == NetworkSettings.MaxQueuedEvents && last.Type == NetworkEventType.Disconnected, "bounded queue reserves disconnect event");
    }

    private static async Task TestConnectFailure()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        using var client = new NetworkClient();
        client.Connect("127.0.0.1", port);
        Check((await Next(client)).Type == NetworkEventType.Disconnected, "connection refusal reported");
        await client.Completion.WaitAsync(TimeSpan.FromSeconds(5));
    }

    private static async Task TestServer(string executable)
    {
        using var reservation = new TcpListener(IPAddress.Loopback, 0);
        reservation.Start();
        int port = ((IPEndPoint)reservation.LocalEndpoint).Port;
        reservation.Stop();
        using var server = Process.Start(new ProcessStartInfo(executable, port.ToString())
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        });
        Task<string> output = server.StandardOutput.ReadToEndAsync();
        Task<string> errors = server.StandardError.ReadToEndAsync();
        try
        {
            for (int round = 0; round < 2; ++round)
            {
                NetworkClient client = null;
                for (int attempt = 0; attempt < 50; ++attempt)
                {
                    client = new NetworkClient();
                    client.Connect("127.0.0.1", port);
                    if ((await Next(client)).Type == NetworkEventType.Connected)
                        break;
                    client.Dispose();
                    await client.Completion;
                    client = null;
                    await Task.Delay(50);
                }
                if (client == null)
                    throw new Exception("Server did not start.");
                using (client)
                {
                    for (uint id = 1; id <= 40; ++id)
                    {
                        byte[] payload = id == 40 ? new byte[4088] : new byte[] { 0, 255, 128, (byte)id };
                        client.Send(new Packet(7, id, payload, 17));
                    }
                    for (uint id = 1; id <= 40; ++id)
                    {
                        NetworkEvent item = await Next(client);
                        Check(item.Type == NetworkEventType.PacketReceived && item.Packet.RequestId == id
                            && item.Packet.Code == 7 && item.Packet.Error == 17
                            && item.Packet.Payload.SequenceEqual(id == 40 ? new byte[4088] : new byte[] { 0, 255, 128, (byte)id }), "actual server echo " + round + "/" + id);
                    }
                    client.Disconnect();
                    await client.Completion.WaitAsync(TimeSpan.FromSeconds(5));
                }
            }
            await MatchmakingChecks.Run(port);
        }
        finally
        {
            await server.StandardInput.WriteLineAsync();
            await server.StandardInput.FlushAsync();
            try { await server.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (TimeoutException) { server.Kill(); }
            string serverOutput = await output;
            string error = await errors;
            if (server.HasExited && server.ExitCode != 0)
                Console.Error.WriteLine("Server exit " + server.ExitCode + "\n" + error + "\n" + serverOutput);
        }
        Check(server.ExitCode == 0, "actual IOCP server, reconnect and clean shutdown");
    }
}


