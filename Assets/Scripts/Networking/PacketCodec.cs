using System;
using System.IO;

namespace Zpd.Networking
{
    public static class PacketCodec
    {
        public static byte[] Encode(Packet packet)
        {
            int size = NetworkSettings.HeaderSize + packet.Payload.Length;
            var bytes = new byte[size];
            bytes[0] = (byte)(size >> 8);
            bytes[1] = (byte)size;
            bytes[2] = packet.Code;
            bytes[3] = packet.Error;
            bytes[4] = (byte)(packet.RequestId >> 24);
            bytes[5] = (byte)(packet.RequestId >> 16);
            bytes[6] = (byte)(packet.RequestId >> 8);
            bytes[7] = (byte)packet.RequestId;
            Buffer.BlockCopy(packet.Payload, 0, bytes, NetworkSettings.HeaderSize, packet.Payload.Length);
            return bytes;
        }

        public static int ReadSize(byte[] header)
        {
            int size = (header[0] << 8) | header[1];
            if (size < NetworkSettings.HeaderSize || size > NetworkSettings.MaxPacketBytes)
                throw new InvalidDataException("Invalid packet size: " + size);
            return size;
        }

        public static Packet Decode(byte[] header, byte[] payload)
        {
            if (ReadSize(header) != NetworkSettings.HeaderSize + payload.Length)
                throw new InvalidDataException("Packet size does not match its payload.");

            uint requestId = ((uint)header[4] << 24) | ((uint)header[5] << 16)
                | ((uint)header[6] << 8) | header[7];
            return new Packet(header[2], requestId, payload, header[3]);
        }
    }
}
