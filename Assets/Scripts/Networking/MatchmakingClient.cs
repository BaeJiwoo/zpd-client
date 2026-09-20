using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Google.Protobuf;
using Protocol;

namespace Zpd.Networking
{
    public enum MatchState
    {
        Disconnected,
        Ready,
        Waiting,
        InSession
    }

    public static class MatchMessageCode
    {
        public const byte MatchRequest = 16;
        public const byte CancelMatchRequest = 17;
        public const byte LeaveSessionRequest = 18;
        public const byte MatchResponse = 144;
        public const byte CancelMatchResponse = 145;
        public const byte LeaveSessionResponse = 146;
        public const byte MatchFound = 208;
        public const byte SessionPlayerLeft = 209;
    }

    public sealed class MatchmakingClient
    {
        private readonly NetworkClient m_client;
        private readonly List<ulong> m_players = new List<ulong>();
        private readonly Stopwatch m_requestTimer = new Stopwatch();
        private uint m_pendingRequestId;
        private byte m_expectedResponse;

        public MatchState State { get; private set; }
        public ulong PlayerId { get; private set; }
        public ulong SessionId { get; private set; }
        public IReadOnlyList<ulong> Players => m_players.AsReadOnly();
        public bool IsBusy => m_pendingRequestId != 0;
        public event Action<string> MessageReceived;

        public MatchmakingClient(NetworkClient client)
        {
            m_client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public void HandleConnected()
        {
            if (!m_client.IsConnected)
                return;
            State = MatchState.Ready;
            RequestMatch();
        }

        public void HandleDisconnected()
        {
            State = MatchState.Disconnected;
            PlayerId = 0;
            SessionId = 0;
            m_players.Clear();
            CompleteRequest();
        }

        public void RequestMatch()
        {
            if (State != MatchState.Ready)
                throw new InvalidOperationException("Leave the current session before requesting a match.");
            SendRequest(MatchMessageCode.MatchRequest, MatchMessageCode.MatchResponse, new MatchRequest());
            MessageReceived?.Invoke("Match requested.");
        }

        public void CancelMatch()
        {
            if (State != MatchState.Waiting)
                throw new InvalidOperationException("There is no waiting match to cancel.");
            SendRequest(MatchMessageCode.CancelMatchRequest, MatchMessageCode.CancelMatchResponse,
                new CancelMatchRequest());
        }

        public void LeaveSession()
        {
            if (State != MatchState.InSession)
                throw new InvalidOperationException("There is no session to leave.");
            SendRequest(MatchMessageCode.LeaveSessionRequest, MatchMessageCode.LeaveSessionResponse,
                new LeaveSessionRequest());
        }

        public void Tick()
        {
            if (IsBusy && m_requestTimer.ElapsedMilliseconds >= 5000)
            {
                MessageReceived?.Invoke("Match request timed out. Check that the updated server is running.");
                m_client.Disconnect();
                HandleDisconnected();
            }
        }

        public bool HandlePacketReceived(Packet packet)
        {
            if (!IsMatchPacket(packet.Code))
                return false;
            try
            {
                if (packet.Code == MatchMessageCode.MatchFound || packet.Code == MatchMessageCode.SessionPlayerLeft)
                {
                    if (packet.RequestId != 0 || packet.Error != 0)
                        throw new InvalidDataException("Invalid match notification header.");
                    HandleNotification(packet);
                    return true;
                }
                if (!IsBusy || packet.RequestId != m_pendingRequestId || packet.Code != m_expectedResponse)
                    throw new InvalidDataException("Unexpected match response. Check the server version.");
                CompleteRequest();
                if (packet.Error != 0)
                {
                    if (packet.Payload.Length != 0)
                        throw new InvalidDataException("Error response must have an empty payload.");
                    MessageReceived?.Invoke("Match request rejected: " + packet.Error
                        + (packet.Error == 23 ? " (state changed; check the current session)." : "."));
                    return true;
                }
                switch (packet.Code)
                {
                    case MatchMessageCode.MatchResponse:
                        var entered = MatchResponse.Parser.ParseFrom(packet.Payload);
                        if (entered.PlayerId == 0)
                            throw new InvalidDataException("Invalid player ID.");
                        PlayerId = entered.PlayerId;
                        State = MatchState.Waiting;
                        MessageReceived?.Invoke("Waiting for other players. Player " + PlayerId);
                        break;
                    case MatchMessageCode.CancelMatchResponse:
                        CancelMatchResponse.Parser.ParseFrom(packet.Payload);
                        State = MatchState.Ready;
                        MessageReceived?.Invoke("Match cancelled.");
                        break;
                    case MatchMessageCode.LeaveSessionResponse:
                        var left = LeaveSessionResponse.Parser.ParseFrom(packet.Payload);
                        if (left.SessionId != SessionId || SessionId == 0)
                            throw new InvalidDataException("Unexpected session leave response.");
                        SessionId = 0;
                        m_players.Clear();
                        State = MatchState.Ready;
                        MessageReceived?.Invoke("Left the session.");
                        break;
                }
            }
            catch (Exception error) when (error is InvalidDataException || error is InvalidProtocolBufferException)
            {
                MessageReceived?.Invoke(error.Message);
                m_client.Disconnect();
                HandleDisconnected();
            }
            return true;
        }

        public static bool IsMatchPacket(byte code)
        {
            return code == MatchMessageCode.MatchRequest || code == MatchMessageCode.CancelMatchRequest
                || code == MatchMessageCode.LeaveSessionRequest || code == MatchMessageCode.MatchResponse
                || code == MatchMessageCode.CancelMatchResponse || code == MatchMessageCode.LeaveSessionResponse
                || code == MatchMessageCode.MatchFound || code == MatchMessageCode.SessionPlayerLeft;
        }

        private void HandleNotification(Packet packet)
        {
            if (packet.Code == MatchMessageCode.MatchFound)
            {
                var matched = MatchFound.Parser.ParseFrom(packet.Payload);
                var players = new HashSet<ulong>(matched.PlayerIds);
                if (State != MatchState.Waiting || matched.SessionId == 0 || players.Count < 2
                    || players.Count > 16 || players.Count != matched.PlayerIds.Count
                    || players.Contains(0) || !players.Contains(PlayerId))
                    throw new InvalidDataException("Invalid matched session.");
                SessionId = matched.SessionId;
                m_players.Clear();
                m_players.AddRange(matched.PlayerIds);
                State = MatchState.InSession;
                MessageReceived?.Invoke("Matched! Session " + SessionId + " / players: "
                    + string.Join(", ", m_players));
                return;
            }
            var left = SessionPlayerLeft.Parser.ParseFrom(packet.Payload);
            if (State != MatchState.InSession || left.SessionId != SessionId || left.PlayerId == PlayerId
                || !m_players.Remove(left.PlayerId))
                throw new InvalidDataException("Invalid session departure.");
            MessageReceived?.Invoke("Player " + left.PlayerId + " left the session.");
        }

        private void SendRequest(byte code, byte expectedResponse, IMessage message)
        {
            if (IsBusy)
                throw new InvalidOperationException("A match request is already pending.");
            m_pendingRequestId = m_client.SendRequest(code, message.ToByteArray());
            m_expectedResponse = expectedResponse;
            m_requestTimer.Restart();
        }

        private void CompleteRequest()
        {
            m_pendingRequestId = 0;
            m_expectedResponse = 0;
            m_requestTimer.Reset();
        }
    }
}
