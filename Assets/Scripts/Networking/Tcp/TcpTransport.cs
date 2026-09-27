using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Zpd.Networking
{
    public sealed class TcpTransport : IDisposable
    {
        private readonly TcpClient tcp_client = new TcpClient();
        private NetworkStream network_stream;

        public async Task ConnectAsync(string host, int port, CancellationToken cancellationToken)
        {
            using (cancellationToken.Register(Dispose))
            {
                cancellationToken.ThrowIfCancellationRequested();
                await tcp_client.ConnectAsync(host, port).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                tcp_client.NoDelay = true;
                network_stream = tcp_client.GetStream();
            }
        }

        public async Task<bool> ReadExactlyAsync(byte[] buffer, CancellationToken cancellationToken)
        {
            int offset = 0;

            while (offset < buffer.Length)
            {
                int received = await network_stream.ReadAsync(buffer, offset, buffer.Length - offset, cancellationToken).ConfigureAwait(false);

                if (received == 0)
                {
                    if (offset != 0)
                    {
                        throw new EndOfStreamException("Connection closed during a packet.");
                    }

                    return false;
                }

                offset += received;
            }

            return true;
        }

        public Task WriteAsync(byte[] bytes, CancellationToken cancellationToken)
        {
            return network_stream.WriteAsync(bytes, 0, bytes.Length, cancellationToken);
        }

        public void Dispose()
        {
            tcp_client.Dispose();
        }
    }
}
