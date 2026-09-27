using System;

namespace Zpd.Lobby
{
    [Serializable]
    public sealed class LobbyInventoryData
    {
        public long revision;
        public LobbyItemData[] items;
    }
}
