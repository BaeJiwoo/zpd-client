namespace Zpd.Networking
{
    public enum NetworkEventType
    {
        Connected,
        PacketReceived,
        Disconnected
    }

    public sealed class NetworkEvent
    {
        public NetworkEventType Type { get; }
        public Packet Packet { get; }
        public string Reason { get; }

        public NetworkEvent(NetworkEventType type, Packet packet = null, string reason = "")
        {
            Type = type;
            Packet = packet;
            Reason = reason;
        }
    }
}
