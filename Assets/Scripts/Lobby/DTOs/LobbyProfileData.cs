using System;

namespace Zpd.Lobby
{
    [Serializable]
    public sealed class LobbyProfileData
    {
        public long revision;
        public string nickname, characterId, characterArtKey;
        public int level, wins, losses;
        public string[] recentMatches;
    }
}
