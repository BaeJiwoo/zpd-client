using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Zpd.Networking;

internal static class MatchmakingChecks
{
    private sealed class Peer : IDisposable
    {
        public readonly NetworkClient Client = new NetworkClient();
        public readonly MatchmakingClient Matchmaking;

        public Peer(int port)
        {
            Matchmaking = new MatchmakingClient(Client);
            Matchmaking.MessageReceived += message => Console.WriteLine("MATCH " + message);
            Client.Connect("127.0.0.1", port);
        }

        public void Pump()
        {
            while (Client.TryDequeue(out NetworkEvent item))
            {
                switch (item.Type)
                {
                    case NetworkEventType.Connected: Matchmaking.HandleConnected(); break;
                    case NetworkEventType.PacketReceived: Matchmaking.HandlePacketReceived(item.Packet); break;
                    case NetworkEventType.Disconnected: Matchmaking.HandleDisconnected(); break;
                }
            }
            Matchmaking.Tick();
        }

        public void Dispose()
        {
            Client.Dispose();
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
        Console.WriteLine("PASS " + message);
    }

    private static async Task Until(Func<bool> condition, params Peer[] peers)
    {
        var timer = Stopwatch.StartNew();
        while (timer.ElapsedMilliseconds < 6000)
        {
            foreach (Peer peer in peers)
                peer.Pump();
            if (condition())
                return;
            await Task.Delay(5);
        }
        throw new TimeoutException("Matchmaking state did not settle.");
    }

    private static async Task<NetworkEvent> Next(NetworkClient client)
    {
        var timer = Stopwatch.StartNew();
        while (timer.ElapsedMilliseconds < 5000)
        {
            if (client.TryDequeue(out NetworkEvent item))
                return item;
            await Task.Delay(5);
        }
        throw new TimeoutException("Missing server response.");
    }

    public static async Task Run(int port)
    {
        using var a = new Peer(port);
        await Until(() => a.Matchmaking.State == MatchState.Waiting, a);
        Check(a.Matchmaking.SessionId == 0, "one player waits without a session");
        using var b = new Peer(port);
        await Until(() => b.Matchmaking.State == MatchState.InSession, b);
        a.Matchmaking.CancelMatch();
        await Until(() => a.Matchmaking.State == MatchState.InSession && !a.Matchmaking.IsBusy, a, b);
        Check(a.Matchmaking.State == MatchState.InSession, "match winning cancellation race preserves the assigned session");
        ulong firstSession = a.Matchmaking.SessionId;
        Check(firstSession != 0 && firstSession == b.Matchmaking.SessionId
            && a.Matchmaking.Players.SequenceEqual(b.Matchmaking.Players)
            && a.Matchmaking.Players.Count == 2, "two clients share a matched session and roster");

        using var c = new Peer(port);
        await Until(() => c.Matchmaking.State == MatchState.Waiting, c);
        c.Matchmaking.CancelMatch();
        await Until(() => c.Matchmaking.State == MatchState.Ready && !c.Matchmaking.IsBusy, c);
        using var d = new Peer(port);
        await Until(() => d.Matchmaking.State == MatchState.Waiting, d);
        Check(d.Matchmaking.SessionId == 0, "cancelled player is removed from FIFO queue");
        c.Matchmaking.RequestMatch();
        await Until(() => c.Matchmaking.State == MatchState.InSession && d.Matchmaking.State == MatchState.InSession, c, d);
        Check(c.Matchmaking.SessionId != firstSession && c.Matchmaking.SessionId == d.Matchmaking.SessionId
            && c.Matchmaking.Players[0] == d.Matchmaking.PlayerId, "FIFO order and independent sessions");

        using var e = new Peer(port);
        await Until(() => e.Matchmaking.State == MatchState.Waiting, e);
        e.Client.Disconnect();
        await e.Client.Completion;
        using var f = new Peer(port);
        await Until(() => f.Matchmaking.State == MatchState.Waiting, f);
        using var g = new Peer(port);
        await Until(() => f.Matchmaking.State == MatchState.InSession && g.Matchmaking.State == MatchState.InSession, f, g);
        Check(f.Matchmaking.SessionId == g.Matchmaking.SessionId, "disconnected waiting player never occupies a match");
        g.Client.Disconnect();
        await Until(() => f.Matchmaking.Players.Count == 1, f);
        Check(f.Matchmaking.Players[0] == f.Matchmaking.PlayerId, "disconnect removes session member");

        a.Matchmaking.LeaveSession();
        await Until(() => a.Matchmaking.State == MatchState.Ready && b.Matchmaking.Players.Count == 1, a, b);
        b.Matchmaking.LeaveSession();
        await Until(() => b.Matchmaking.State == MatchState.Ready, b);
        a.Matchmaking.RequestMatch();
        await Until(() => a.Matchmaking.State == MatchState.Waiting, a);
        b.Matchmaking.RequestMatch();
        await Until(() => a.Matchmaking.State == MatchState.InSession && b.Matchmaking.State == MatchState.InSession, a, b);
        Check(a.Matchmaking.SessionId != firstSession && a.Matchmaking.SessionId == b.Matchmaking.SessionId,
            "leaving and rematching creates a new session");
        c.Pump();
        d.Pump();
        Check(c.Matchmaking.Players.Count == 2 && d.Matchmaking.Players.Count == 2, "other sessions are unaffected");

        using var raw = new NetworkClient();
        raw.Connect("127.0.0.1", port);
        Check((await Next(raw)).Type == NetworkEventType.Connected, "raw validation peer connected");
        raw.Send(new Packet(16, 0, Array.Empty<byte>()));
        Check((await Next(raw)).Packet.Error == 3, "zero request ID rejected");
        raw.Send(new Packet(16, 1, new byte[] { 255 }));
        Check((await Next(raw)).Packet.Error == 2, "malformed match protobuf rejected");
        raw.Send(new Packet(16, 2, Array.Empty<byte>()));
        Check((await Next(raw)).Packet.Code == 144, "match request acknowledged");
        raw.Send(new Packet(16, 3, Array.Empty<byte>()));
        Check((await Next(raw)).Packet.Error == 23, "duplicate queue request rejected");
        raw.Send(new Packet(17, 4, Array.Empty<byte>()));
        Check((await Next(raw)).Packet.Code == 145, "queue cancellation acknowledged");
        raw.Send(new Packet(18, 5, Array.Empty<byte>()));
        Check((await Next(raw)).Packet.Error == 23, "leave without a session rejected");
    }
}
