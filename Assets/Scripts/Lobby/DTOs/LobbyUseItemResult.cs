using System;

namespace Zpd.Lobby
{
    [Serializable]
    public sealed class LobbyUseItemResult
    {
        public LobbyInventoryData inventory;
        public LobbyProfileData profile;
    }
}
