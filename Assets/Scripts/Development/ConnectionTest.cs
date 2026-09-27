using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Zpd.Networking;

namespace Zpd.Development
{
    public sealed class ConnectionTest : MonoBehaviour
    {
        [FormerlySerializedAs("m_host")]
        [SerializeField]
        private string server_host = "127.0.0.1";

        [FormerlySerializedAs("m_port")]
        [SerializeField]
        private int server_port = NetworkSettings.DefaultPort;

        [FormerlySerializedAs("m_fontSize")]
        [SerializeField, Range(16, 32)]
        private int font_size = 22;
        private PacketHandler packet_handler;
        private readonly Queue<string> log_messages = new Queue<string>();
        private NetworkClient network_client;
        private string message_input = "Hello, server!";
        private string message_code_input = "1";
        private string port_input;
        private Vector2 log_scroll_position;
        private Vector2 page_scroll_position;
        private bool is_connecting;
        private GUIStyle gui_style_title;
        private GUIStyle gui_style_label;
        private GUIStyle gui_style_button;
        private GUIStyle gui_style_input;
        private GUIStyle gui_style_log;
        private GUIStyle gui_style_panel;
        private int applied_font_size;

        private void Awake()
        {
            port_input = server_port.ToString();
        }

        private void Update()
        {
            for (int count = 0; count < NetworkSettings.MaxEventsPerFrame; ++count)
            {
                if (network_client == null || !network_client.TryDequeue(out NetworkEvent networkEvent))
                {
                    break;
                }

                switch (networkEvent.Type)
                {
                    case NetworkEventType.Connected:
                        is_connecting = false;
                        packet_handler.HandleConnected();
                        break;
                    case NetworkEventType.PacketReceived:
                        packet_handler.HandlePacketReceived(networkEvent.Packet);
                        break;
                    case NetworkEventType.Disconnected:
                        is_connecting = false;
                        packet_handler.HandleDisconnected(networkEvent.Reason);
                        break;
                }
            }

            packet_handler?.Matchmaking.Tick();
        }

        private void OnGUI()
        {
            PrepareStyles();
            bool wideLayout = Screen.width >= 900;
            GUILayout.BeginArea(
                new Rect(12, 12, Mathf.Max(1, Screen.width - 24), Mathf.Max(1, Screen.height - 24)),
                gui_style_panel);
            page_scroll_position = GUILayout.BeginScrollView(page_scroll_position);
            GUILayout.Label("ZPD Connection Test", gui_style_title);
            GUILayout.Space(12);

            if (wideLayout)
            {
                GUILayout.BeginHorizontal();
            }

            GUILayout.BeginVertical(gui_style_panel, wideLayout ? GUILayout.Width(380) : GUILayout.ExpandWidth(true));
            GUILayout.Label("Server: " + server_host, gui_style_label);
            bool connected = network_client != null && network_client.IsConnected;
            GUILayout.Label("Port (1-65535)", gui_style_label);
            GUI.enabled = !is_connecting && !connected;
            port_input = GUILayout.TextField(port_input, gui_style_input, GUILayout.Height(48));
            GUI.enabled = true;
            bool validPort = TryReadPort(out _);

            if (!validPort)
            {
                GUILayout.Label("Enter a port from 1 to 65535.", gui_style_label);
            }

            GUILayout.Space(8);
            Color previousColor = GUI.contentColor;
            GUI.contentColor = connected ? new Color(0.4f, 1f, 0.6f) : new Color(1f, 0.8f, 0.4f);
            GUILayout.Label(is_connecting ? "Connecting..." : connected ? "Connected" : "Disconnected", gui_style_label);
            GUI.contentColor = previousColor;
            GUILayout.Space(12);
            GUILayout.BeginHorizontal();
            GUI.enabled = !is_connecting && !connected && validPort;

            if (GUILayout.Button("Connect", gui_style_button))
            {
                Connect();
            }

            GUI.enabled = is_connecting || connected;

            if (GUILayout.Button("Disconnect", gui_style_button))
            {
                network_client.Disconnect();
            }

            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.Space(20);
            DrawMatchmaking(connected);
            GUILayout.Space(20);
            GUILayout.Label("Message code (0-255)", gui_style_label);
            message_code_input = GUILayout.TextField(message_code_input, 3, gui_style_input, GUILayout.Height(48));
            GUILayout.Space(12);
            GUILayout.Label("UTF-8 message", gui_style_label);
            message_input = GUILayout.TextArea(message_input, gui_style_input, GUILayout.Height(140));
            GUILayout.Space(12);
            GUI.enabled = connected;

            if (GUILayout.Button("Send message", gui_style_button))
            {
                SendPacket();
            }

            GUI.enabled = true;
            GUILayout.EndVertical();

            GUILayout.Space(16);
            GUILayout.BeginVertical(gui_style_panel, GUILayout.ExpandWidth(true));
            GUILayout.BeginHorizontal();
            GUILayout.Label("Communication log", gui_style_label);

            if (GUILayout.Button("Clear", gui_style_button, GUILayout.Width(100)))
            {
                log_messages.Clear();
            }

            GUILayout.EndHorizontal();
            GUILayout.Space(8);
            log_scroll_position = GUILayout.BeginScrollView(
                log_scroll_position,
                GUILayout.Height(wideLayout ? Mathf.Max(300, Screen.height - 210) : 300));

            if (log_messages.Count == 0)
            {
                GUILayout.Label("Connect and send a message to see the server response here.", gui_style_label);
            }

            foreach (string message in log_messages)
            {
                GUILayout.Label(message, gui_style_log);
                GUILayout.Space(8);
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            if (wideLayout)
            {
                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void PrepareStyles()
        {
            if (gui_style_title != null && applied_font_size == font_size)
            {
                return;
            }

            var font = Resources.Load<Font>("Fonts/NexonLv1/NEXONLv1GothicRegular");
            applied_font_size = font_size;
            gui_style_title = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = font_size + 10,
                fontStyle = FontStyle.Bold,
                wordWrap = true
            };
            gui_style_label = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = font_size,
                wordWrap = true
            };
            gui_style_button = new GUIStyle(GUI.skin.button)
            {
                font = font,
                fontSize = font_size,
                padding = new RectOffset(14, 14, 12, 12),
                fixedHeight = 52
            };
            gui_style_input = new GUIStyle(GUI.skin.textArea)
            {
                font = font,
                fontSize = font_size,
                padding = new RectOffset(12, 12, 10, 10),
                wordWrap = true
            };
            gui_style_log = new GUIStyle(gui_style_label)
            {
                padding = new RectOffset(12, 12, 12, 12)
            };
            gui_style_log.normal.background = GUI.skin.box.normal.background;
            gui_style_panel = new GUIStyle(GUI.skin.box)
            {
                font = font,
                padding = new RectOffset(16, 16, 16, 16)
            };
        }

        private void DrawMatchmaking(bool connected)
        {
            GUILayout.Label("Matchmaking", gui_style_title);
            MatchmakingClient matchmaking = packet_handler?.Matchmaking;

            if (matchmaking == null || !connected)
            {
                GUILayout.Label("Connect to join the matchmaking queue.", gui_style_label);
                return;
            }

            GUILayout.Label(matchmaking.IsBusy ? "Request pending..." : matchmaking.State.ToString(), gui_style_label);

            if (matchmaking.PlayerId != 0)
            {
                GUILayout.Label("Player: " + matchmaking.PlayerId, gui_style_label);
            }

            if (matchmaking.State == MatchState.Waiting)
            {
                GUILayout.Label("Waiting for other players...", gui_style_label);
            }

            if (matchmaking.SessionId != 0)
            {
                GUILayout.Label("Session: " + matchmaking.SessionId, gui_style_title);

                foreach (ulong playerId in matchmaking.Players)
                {
                    GUILayout.Label(
                        "Player " + playerId + (playerId == matchmaking.PlayerId ? " (You)" : ""),
                        gui_style_label);
                }
            }

            GUI.enabled = !matchmaking.IsBusy;

            try
            {
                if (matchmaking.State == MatchState.Ready && GUILayout.Button("Find match", gui_style_button))
                {
                    matchmaking.RequestMatch();
                }

                if (matchmaking.State == MatchState.Waiting && GUILayout.Button("Cancel match", gui_style_button))
                {
                    matchmaking.CancelMatch();
                }

                if (matchmaking.State == MatchState.InSession && GUILayout.Button("Leave session", gui_style_button))
                {
                    matchmaking.LeaveSession();
                }
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
                server_port = port;
                network_client?.Dispose();

                if (packet_handler != null)
                {
                    packet_handler.MessageReceived -= AddMessage;
                }

                network_client = new NetworkClient();
                packet_handler = new PacketHandler(network_client);
                packet_handler.MessageReceived += AddMessage;
                is_connecting = true;
                AddMessage("Connecting to " + server_host + ":" + server_port);
                network_client.Connect(server_host, server_port);
            }
            catch (Exception error)
            {
                is_connecting = false;
                AddMessage(error.Message);
            }
        }

        private bool TryReadPort(out int port)
        {
            return int.TryParse(port_input, out port) && port >= 1 && port <= 65535;
        }

        private void SendPacket()
        {
            if (!byte.TryParse(message_code_input, out byte code))
            {
                AddMessage("Message code must be between 0 and 255.");
                return;
            }

            try
            {
                packet_handler.SendText(code, message_input);
            }
            catch (Exception error)
            {
                AddMessage(error.Message);
            }
        }

        private void AddMessage(string message)
        {
            if (log_messages.Count >= 100)
            {
                log_messages.Dequeue();
            }

            log_messages.Enqueue(message);
            log_scroll_position.y = float.MaxValue;
        }

        private void OnDestroy()
        {
            network_client?.Dispose();

            if (packet_handler != null)
            {
                packet_handler.MessageReceived -= AddMessage;
            }
        }

        private void OnApplicationQuit()
        {
            network_client?.Dispose();
        }
    }
}
