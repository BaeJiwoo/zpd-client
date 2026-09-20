using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Zpd.Networking
{
    public sealed class NetworkClient : IDisposable
    {
        private readonly object m_mutex = new object();
        private readonly TcpTransport m_transport = new TcpTransport();
        private readonly Queue<byte[]> m_sendQueue = new Queue<byte[]>();
        private readonly Queue<NetworkEvent> m_eventQueue = new Queue<NetworkEvent>();
        private readonly SemaphoreSlim m_sendReady = new SemaphoreSlim(0);
        private readonly CancellationTokenSource m_lifetime = new CancellationTokenSource();
        private bool m_started;
        private bool m_connected;
        private bool m_closed;
        private uint m_requestId;

        public Task Completion { get; private set; } = Task.CompletedTask;

        public bool IsConnected
        {
            get
            {
                lock (m_mutex)
                    return m_connected;
            }
        }

        public void Connect(string host, int port)
        {
            if (string.IsNullOrWhiteSpace(host))
                throw new ArgumentException("Server address is required.", nameof(host));
            if (port < 1 || port > 65535)
                throw new ArgumentOutOfRangeException(nameof(port));

            lock (m_mutex)
            {
                if (m_started || m_closed)
                    throw new InvalidOperationException("Use a new client for each connection.");
                m_started = true;
                Completion = RunAsync(host, port);
            }
        }

        public void Send(Packet packet)
        {
            if (packet == null)
                throw new ArgumentNullException(nameof(packet));
            byte[] bytes = PacketCodec.Encode(packet);
            lock (m_mutex)
            {
                if (!m_connected)
                    throw new InvalidOperationException("The client is not connected.");
                if (m_sendQueue.Count >= NetworkSettings.MaxQueuedSends)
                    throw new InvalidOperationException("The send queue is full.");
                m_sendQueue.Enqueue(bytes);
                m_sendReady.Release();
            }
        }

        public uint SendRequest(byte code, byte[] payload)
        {
            lock (m_mutex)
            {
                if (m_requestId == uint.MaxValue)
                    throw new InvalidOperationException("Reconnect before sending more requests.");
                uint requestId = ++m_requestId;
                Send(new Packet(code, requestId, payload));
                return requestId;
            }
        }

        public bool TryDequeue(out NetworkEvent networkEvent)
        {
            lock (m_mutex)
            {
                if (m_eventQueue.Count == 0)
                {
                    networkEvent = null;
                    return false;
                }
                networkEvent = m_eventQueue.Dequeue();
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
                using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(m_lifetime.Token))
                {
                    timeout.CancelAfter(NetworkSettings.ConnectTimeoutMilliseconds);
                    await m_transport.ConnectAsync(host, port, timeout.Token).ConfigureAwait(false);
                }

                lock (m_mutex)
                {
                    if (m_closed)
                        return;
                    m_connected = true;
                    m_eventQueue.Enqueue(new NetworkEvent(NetworkEventType.Connected));
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
                while (await m_transport.ReadExactlyAsync(header, m_lifetime.Token).ConfigureAwait(false))
                {
                    int size = PacketCodec.ReadSize(header);
                    var payload = new byte[size - NetworkSettings.HeaderSize];
                    if (!await m_transport.ReadExactlyAsync(payload, m_lifetime.Token).ConfigureAwait(false))
                        throw new EndOfStreamException("Connection closed before the packet body.");

                    Packet packet = PacketCodec.Decode(header, payload);
                    lock (m_mutex)
                    {
                        if (m_closed)
                            return;
                        if (m_eventQueue.Count >= NetworkSettings.MaxQueuedEvents - 1)
                            throw new InvalidOperationException("The receive queue is full.");
                        m_eventQueue.Enqueue(new NetworkEvent(NetworkEventType.PacketReceived, packet));
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
                    await m_sendReady.WaitAsync(m_lifetime.Token).ConfigureAwait(false);
                    byte[] bytes;
                    lock (m_mutex)
                    {
                        if (m_closed)
                            return;
                        bytes = m_sendQueue.Dequeue();
                    }
                    await m_transport.WriteAsync(bytes, m_lifetime.Token).ConfigureAwait(false);
                }
            }
            catch (Exception error)
            {
                Close(error.Message);
            }
        }

        private void Close(string reason)
        {
            lock (m_mutex)
            {
                if (m_closed)
                    return;
                m_closed = true;
                m_connected = false;
                m_sendQueue.Clear();
                m_eventQueue.Enqueue(new NetworkEvent(NetworkEventType.Disconnected, reason: reason));
            }
            m_lifetime.Cancel();
            m_transport.Dispose();
        }
    }
}
