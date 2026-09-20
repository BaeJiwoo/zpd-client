using System;
using System.Text;
using Zpd.Networking;

namespace Zpd.Development
{
    public sealed class PacketHandler
    {
        private readonly NetworkClient m_client;
        public MatchmakingClient Matchmaking { get; }

        public event Action<string> MessageReceived;

        public PacketHandler(NetworkClient client)
        {
            m_client = client ?? throw new ArgumentNullException(nameof(client));
            Matchmaking = new MatchmakingClient(client);
            Matchmaking.MessageReceived += message => MessageReceived?.Invoke(message);
        }

        public void SendText(byte code, string text)
        {
            byte[] payload = Encoding.UTF8.GetBytes(text);
            if (payload.Length > NetworkSettings.MaxPayloadBytes)
                throw new InvalidOperationException("Message must fit in 4088 UTF-8 bytes.");
            if (MatchmakingClient.IsMatchPacket(code))
                throw new InvalidOperationException("Use the matching controls for reserved match messages.");
            uint requestId = m_client.SendRequest(code, payload);
            MessageReceived?.Invoke("Queued code=" + code + " requestId=" + requestId);
        }

        public void HandleConnected()
        {
            MessageReceived?.Invoke("Connected.");
            Matchmaking.HandleConnected();
        }

        public void HandlePacketReceived(Packet packet)
        {
            if (Matchmaking.HandlePacketReceived(packet))
                return;
            string text = Encoding.UTF8.GetString(packet.Payload);
            MessageReceived?.Invoke("Received code=" + packet.Code + " error=" + packet.Error
                + " requestId=" + packet.RequestId + " bytes=" + packet.Payload.Length + "\n" + text);
        }

        public void HandleDisconnected(string reason)
        {
            Matchmaking.HandleDisconnected();
            MessageReceived?.Invoke(reason);
        }
    }
}
