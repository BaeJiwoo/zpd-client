namespace Zpd.Networking
{
    public static class NetworkSettings
    {
        public const int DefaultPort = 20000;
        public const int HeaderSize = 8;
        public const int MaxPacketBytes = 4096;
        public const int MaxPayloadBytes = MaxPacketBytes - HeaderSize;
        public const int MaxQueuedSends = 256;
        public const int MaxQueuedEvents = 1024;
        public const int ConnectTimeoutMilliseconds = 5000;
        public const int MaxEventsPerFrame = 100;
    }
}
