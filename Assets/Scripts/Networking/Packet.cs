using System;

namespace Zpd.Networking
{
    public sealed class Packet
    {
        public byte Code { get; }
        public byte Error { get; }
        public uint RequestId { get; }
        public byte[] Payload { get; }

        public Packet(byte code, uint requestId, byte[] payload, byte error = 0)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));
            if (payload.Length > NetworkSettings.MaxPayloadBytes)
                throw new ArgumentOutOfRangeException(nameof(payload));

            Code = code;
            Error = error;
            RequestId = requestId;
            Payload = (byte[])payload.Clone();
        }
    }
}
