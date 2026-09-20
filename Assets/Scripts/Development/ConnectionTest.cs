using System;
using System.Collections.Generic;
using UnityEngine;
using Zpd.Networking;

namespace Zpd.Development
{
    public sealed class ConnectionTest : MonoBehaviour
    {
        [SerializeField] private string m_host = "127.0.0.1";
        [SerializeField] private int m_port = NetworkSettings.DefaultPort;
        [SerializeField, Range(16, 32)] private int m_fontSize = 22;
        private PacketHandler m_packetHandler;
        private readonly Queue<string> m_messages = new Queue<string>();
        private NetworkClient m_client;
        private string m_text = "Hello, server!";
        private string m_code = "1";
        private string m_portText;
        private Vector2 m_scroll;
        private Vector2 m_pageScroll;
        private bool m_connecting;
        private GUIStyle m_titleStyle;
        private GUIStyle m_labelStyle;
        private GUIStyle m_buttonStyle;
        private GUIStyle m_inputStyle;
        private GUIStyle m_logStyle;
        private GUIStyle m_panelStyle;
        private int m_appliedFontSize;

        private void Awake()
        {
            m_portText = m_port.ToString();
        }

        private void Update()
        {
            for (int count = 0; count < NetworkSettings.MaxEventsPerFrame; ++count)
            {
                if (m_client == null || !m_client.TryDequeue(out NetworkEvent networkEvent))
                    break;
                switch (networkEvent.Type)
                {
                    case NetworkEventType.Connected:
                        m_connecting = false;
                        m_packetHandler.HandleConnected();
                        break;
                    case NetworkEventType.PacketReceived:
                        m_packetHandler.HandlePacketReceived(networkEvent.Packet);
                        break;
                    case NetworkEventType.Disconnected:
                        m_connecting = false;
                        m_packetHandler.HandleDisconnected(networkEvent.Reason);
                        break;
                }
            }
            m_packetHandler?.Matchmaking.Tick();
        }

        private void OnGUI()
        {
            PrepareStyles();
            bool wideLayout = Screen.width >= 900;
            GUILayout.BeginArea(new Rect(12, 12, Mathf.Max(1, Screen.width - 24),
                Mathf.Max(1, Screen.height - 24)), m_panelStyle);
            m_pageScroll = GUILayout.BeginScrollView(m_pageScroll);
            GUILayout.Label("ZPD Connection Test", m_titleStyle);
            GUILayout.Space(12);
            if (wideLayout)
                GUILayout.BeginHorizontal();

            GUILayout.BeginVertical(m_panelStyle,
                wideLayout ? GUILayout.Width(380) : GUILayout.ExpandWidth(true));
            GUILayout.Label("Server: " + m_host, m_labelStyle);
            bool connected = m_client != null && m_client.IsConnected;
            GUILayout.Label("Port (1-65535)", m_labelStyle);
            GUI.enabled = !m_connecting && !connected;
            m_portText = GUILayout.TextField(m_portText, m_inputStyle, GUILayout.Height(48));
            GUI.enabled = true;
            bool validPort = TryReadPort(out _);
            if (!validPort)
                GUILayout.Label("Enter a port from 1 to 65535.", m_labelStyle);
            GUILayout.Space(8);
            Color previousColor = GUI.contentColor;
            GUI.contentColor = connected ? new Color(0.4f, 1f, 0.6f) : new Color(1f, 0.8f, 0.4f);
            GUILayout.Label(m_connecting ? "Connecting..." :
                connected ? "Connected" : "Disconnected", m_labelStyle);
            GUI.contentColor = previousColor;
            GUILayout.Space(12);
            GUILayout.BeginHorizontal();
            GUI.enabled = !m_connecting && !connected && validPort;
            if (GUILayout.Button("Connect", m_buttonStyle))
                Connect();
            GUI.enabled = m_connecting || connected;
            if (GUILayout.Button("Disconnect", m_buttonStyle))
                m_client.Disconnect();
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.Space(20);
            DrawMatchmaking(connected);
            GUILayout.Space(20);
            GUILayout.Label("Message code (0-255)", m_labelStyle);
            m_code = GUILayout.TextField(m_code, 3, m_inputStyle, GUILayout.Height(48));
            GUILayout.Space(12);
            GUILayout.Label("UTF-8 message", m_labelStyle);
            m_text = GUILayout.TextArea(m_text, m_inputStyle, GUILayout.Height(140));
            GUILayout.Space(12);
            GUI.enabled = connected;
            if (GUILayout.Button("Send message", m_buttonStyle))
                SendPacket();
            GUI.enabled = true;
            GUILayout.EndVertical();

            GUILayout.Space(16);
            GUILayout.BeginVertical(m_panelStyle, GUILayout.ExpandWidth(true));
            GUILayout.BeginHorizontal();
            GUILayout.Label("Communication log", m_labelStyle);
            if (GUILayout.Button("Clear", m_buttonStyle, GUILayout.Width(100)))
                m_messages.Clear();
            GUILayout.EndHorizontal();
            GUILayout.Space(8);
            m_scroll = GUILayout.BeginScrollView(m_scroll,
                GUILayout.Height(wideLayout ? Mathf.Max(300, Screen.height - 210) : 300));
            if (m_messages.Count == 0)
                GUILayout.Label("Connect and send a message to see the server response here.", m_labelStyle);
            foreach (string message in m_messages)
            {
                GUILayout.Label(message, m_logStyle);
                GUILayout.Space(8);
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            if (wideLayout)
                GUILayout.EndHorizontal();
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void PrepareStyles()
        {
            if (m_titleStyle != null && m_appliedFontSize == m_fontSize)
                return;

            m_appliedFontSize = m_fontSize;
            m_titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = m_fontSize + 10,
                fontStyle = FontStyle.Bold,
                wordWrap = true
            };
            m_labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = m_fontSize,
                wordWrap = true
            };
            m_buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = m_fontSize,
                padding = new RectOffset(14, 14, 12, 12),
                fixedHeight = 52
            };
            m_inputStyle = new GUIStyle(GUI.skin.textArea)
            {
                fontSize = m_fontSize,
                padding = new RectOffset(12, 12, 10, 10),
                wordWrap = true
            };
            m_logStyle = new GUIStyle(m_labelStyle)
            {
                padding = new RectOffset(12, 12, 12, 12)
            };
            m_logStyle.normal.background = GUI.skin.box.normal.background;
            m_panelStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(16, 16, 16, 16)
            };
        }

        private void DrawMatchmaking(bool connected)
        {
            GUILayout.Label("Matchmaking", m_titleStyle);
            MatchmakingClient matchmaking = m_packetHandler?.Matchmaking;
            if (matchmaking == null || !connected)
            {
                GUILayout.Label("Connect to join the matchmaking queue.", m_labelStyle);
                return;
            }
            GUILayout.Label(matchmaking.IsBusy ? "Request pending..." : matchmaking.State.ToString(), m_labelStyle);
            if (matchmaking.PlayerId != 0)
                GUILayout.Label("Player: " + matchmaking.PlayerId, m_labelStyle);
            if (matchmaking.State == MatchState.Waiting)
                GUILayout.Label("Waiting for other players...", m_labelStyle);
            if (matchmaking.SessionId != 0)
            {
                GUILayout.Label("Session: " + matchmaking.SessionId, m_titleStyle);
                foreach (ulong playerId in matchmaking.Players)
                    GUILayout.Label("Player " + playerId + (playerId == matchmaking.PlayerId ? " (You)" : ""), m_labelStyle);
            }
            GUI.enabled = !matchmaking.IsBusy;
            try
            {
                if (matchmaking.State == MatchState.Ready && GUILayout.Button("Find match", m_buttonStyle))
                    matchmaking.RequestMatch();
                if (matchmaking.State == MatchState.Waiting && GUILayout.Button("Cancel match", m_buttonStyle))
                    matchmaking.CancelMatch();
                if (matchmaking.State == MatchState.InSession && GUILayout.Button("Leave session", m_buttonStyle))
                    matchmaking.LeaveSession();
            }
            catch (Exception error)
            {
                AddMessage(error.Message);
            }
            GUI.enabled = true;
        }

        private void Connect()
        {
            if (!TryReadPort(out int port))
            {
                AddMessage("Enter a port from 1 to 65535.");
                return;
            }
            try
            {
                m_port = port;
                m_client?.Dispose();
                if (m_packetHandler != null)
                    m_packetHandler.MessageReceived -= AddMessage;
                m_client = new NetworkClient();
                m_packetHandler = new PacketHandler(m_client);
                m_packetHandler.MessageReceived += AddMessage;
                m_connecting = true;
                AddMessage("Connecting to " + m_host + ":" + m_port);
                m_client.Connect(m_host, m_port);
            }
            catch (Exception error)
            {
                m_connecting = false;
                AddMessage(error.Message);
            }
        }

        private bool TryReadPort(out int port)
        {
            return int.TryParse(m_portText, out port) && port >= 1 && port <= 65535;
        }

        private void SendPacket()
        {
            if (!byte.TryParse(m_code, out byte code))
            {
                AddMessage("Message code must be between 0 and 255.");
                return;
            }
            try
            {
                m_packetHandler.SendText(code, m_text);
            }
            catch (Exception error)
            {
                AddMessage(error.Message);
            }
        }

        private void AddMessage(string message)
        {
            if (m_messages.Count >= 100)
                m_messages.Dequeue();
            m_messages.Enqueue(message);
            m_scroll.y = float.MaxValue;
        }

        private void OnDestroy()
        {
            m_client?.Dispose();
            if (m_packetHandler != null)
                m_packetHandler.MessageReceived -= AddMessage;
        }

        private void OnApplicationQuit()
        {
            m_client?.Dispose();
        }
    }
}
