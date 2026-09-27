using System;

namespace Zpd.Lobby
{
    [Serializable]
    public sealed class LobbyItemData
    {
        public string id, name, description, iconKey;
        public LobbyItemKind kind;
        public int quantity, capacity = 100;
    }
}
