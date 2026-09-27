using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Zpd.Networking
{
    public sealed class NetworkClient : IDisposable
    {
        private readonly object state_lock = new object();
        private readonly TcpTransport tcp_transport = new TcpTransport();
        private readonly Queue<byte[]> send_queue = new Queue<byte[]>();
        private readonly Queue<NetworkEvent> event_queue = new Queue<NetworkEvent>();
        private readonly SemaphoreSlim send_queue_signal = new SemaphoreSlim(0);
        private readonly CancellationTokenSource cts_lifetime = new CancellationTokenSource();
        private bool is_started;
        private bool is_connected;
        private bool is_closed;
        private uint last_request_id;

        public Task Completion { get; private set; } = Task.CompletedTask;

        public bool IsConnected
        {
            get
            {
                lock (state_lock)
                {
                    return is_connected;
                }
            }
        }

        public void Connect(string host, int port)
        {
            if (string.IsNullOrWhiteSpace(host))
            {
                throw new ArgumentException("Server address is required.", nameof(host));
            }

            if (port < 1 || port > 65535)
            {
                throw new ArgumentOutOfRangeException(nameof(port));
            }

            lock (state_lock)
            {
                if (is_started || is_closed)
                {
                    throw new InvalidOperationException("Use a new client for each connection.");
                }

                is_started = true;
                Completion = RunAsync(host, port);
            }
        }

        public void Send(Packet packet)
        {
            if (packet == null)
            {
                throw new ArgumentNullException(nameof(packet));
            }

            byte[] bytes = PacketCodec.Encode(packet);

            lock (state_lock)
            {
                if (!is_connected)
                {
                    throw new InvalidOperationException("The client is not connected.");
                }

                if (send_queue.Count >= NetworkSettings.MaxQueuedSends)
                {
                    throw new InvalidOperationException("The send queue is full.");
                }

                send_queue.Enqueue(bytes);
                send_queue_signal.Release();
            }
        }

        public uint SendRequest(byte code, byte[] payload)
        {
            lock (state_lock)
            {
                if (last_request_id == uint.MaxValue)
                {
                    throw new InvalidOperationException("Reconnect before sending more requests.");
                }

                uint requestId = ++last_request_id;
                Send(new Packet(code, requestId, payload));
                return requestId;
            }
        }

        public bool TryDequeue(out NetworkEvent networkEvent)
        {
            lock (state_lock)
            {
                if (event_queue.Count == 0)
                {
                    networkEvent = null;
                    return false;
                }

                networkEvent = event_queue.Dequeue();
                return true;
            }
        }

        public void Disconnect()
        {
            Close("Disconnected by client.");
        }

        public void Dispose()
        {
            Disconnect();
        }

        private async Task RunAsync(string host, int port)
        {
            try
            {
                using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cts_lifetime.Token))
                {
                    timeout.CancelAfter(NetworkSettings.ConnectTimeoutMilliseconds);
                    await tcp_transport.ConnectAsync(host, port, timeout.Token).ConfigureAwait(false);
                }

                lock (state_lock)
                {
                    if (is_closed)
                    {
                        return;
                    }

                    is_connected = true;
                    event_queue.Enqueue(new NetworkEvent(NetworkEventType.Connected));
                }

                await Task.WhenAll(ReceiveLoopAsync(), SendLoopAsync()).ConfigureAwait(false);
            }
            catch (Exception error)
            {
                Close(error.Message);
            }
            finally
            {
                Close("Connection closed.");
            }
        }

        private async Task ReceiveLoopAsync()
        {
            try
            {
                var header = new byte[NetworkSettings.HeaderSize];

                while (await tcp_transport.ReadExactlyAsync(header, cts_lifetime.Token).ConfigureAwait(false))
                {
                    int size = PacketCodec.ReadSize(header);
                    var payload = new byte[size - NetworkSettings.HeaderSize];

                    if (!await tcp_transport.ReadExactlyAsync(payload, cts_lifetime.Token).ConfigureAwait(false))
                    {
                        throw new EndOfStreamException("Connection closed before the packet body.");
                    }

                    Packet packet = PacketCodec.Decode(header, payload);

                    lock (state_lock)
                    {
                        if (is_closed)
                        {
                            return;
                        }

                        if (event_queue.Count >= NetworkSettings.MaxQueuedEvents - 1)
                        {
                            throw new InvalidOperationException("The receive queue is full.");
                        }

                        event_queue.Enqueue(new NetworkEvent(NetworkEventType.PacketReceived, packet));
                    }
                }

                Close("Disconnected by server.");
            }
            catch (Exception error)
            {
                Close(error.Message);
            }
        }

        private async Task SendLoopAsync()
        {
            try
            {
                while (true)
                {
                    await send_queue_signal.WaitAsync(cts_lifetime.Token).ConfigureAwait(false);
                    byte[] bytes;

                    lock (state_lock)
                    {
                        if (is_closed)
                        {
                            return;
                        }

                        bytes = send_queue.Dequeue();
                    }

                    await tcp_transport.WriteAsync(bytes, cts_lifetime.Token).ConfigureAwait(false);
                }
            }
            catch (Exception error)
            {
                Close(error.Message);
            }
        }

        private void Close(string reason)
        {
            lock (state_lock)
            {
                if (is_closed)
                {
                    return;
                }

                is_closed = true;
                is_connected = false;
                send_queue.Clear();
                event_queue.Enqueue(new NetworkEvent(NetworkEventType.Disconnected, reason: reason));
            }

            cts_lifetime.Cancel();
            tcp_transport.Dispose();
        }
    }
}
