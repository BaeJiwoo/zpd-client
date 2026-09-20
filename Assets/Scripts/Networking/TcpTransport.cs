using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Zpd.Networking
{
    public sealed class TcpTransport : IDisposable
    {
        private readonly TcpClient m_client = new TcpClient();
        private NetworkStream m_stream;

        public async Task ConnectAsync(string host, int port, CancellationToken cancellationToken)
        {
            using (cancellationToken.Register(Dispose))
            {
                cancellationToken.ThrowIfCancellationRequested();
                await m_client.ConnectAsync(host, port).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                m_client.NoDelay = true;
                m_stream = m_client.GetStream();
            }
        }

        public async Task<bool> ReadExactlyAsync(byte[] buffer, CancellationToken cancellationToken)
        {
            int offset = 0;
            while (offset < buffer.Length)
            {
                int received = await m_stream.ReadAsync(buffer, offset, buffer.Length - offset,
                    cancellationToken).ConfigureAwait(false);
                if (received == 0)
                {
                    if (offset != 0)
                        throw new EndOfStreamException("Connection closed during a packet.");
                    return false;
                }
                offset += received;
            }
            return true;
        }

        public Task WriteAsync(byte[] bytes, CancellationToken cancellationToken)
        {
            return m_stream.WriteAsync(bytes, 0, bytes.Length, cancellationToken);
        }

        public void Dispose()
        {
            m_client.Dispose();
        }
    }
}
