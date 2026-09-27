using System;
using System.Collections.Generic;
using System.Linq;

namespace Zpd.Lobby
{
    public sealed class LobbyProfileSnapshot
    {
        public long Revision { get; }
        public string Nickname { get; }
        public int Level { get; }
        public int Wins { get; }
        public int Losses { get; }
        public string CharacterId { get; }
        public string CharacterArtKey { get; }
        public IReadOnlyList<string> History { get; }
        public long Matches => (long)Wins + Losses;
        public double? WinRate => Matches == 0 ? (double?)null : 100.0 * Wins / Matches;

        public LobbyProfileSnapshot(LobbyProfileData data)
        {
            if (data == null || data.revision < 0 || data.level < 0 || data.wins < 0 || data.losses < 0 || string.IsNullOrWhiteSpace(data.nickname))
            {
                throw new ArgumentException("Invalid profile response.");
            }

            Revision = data.revision;
            Nickname = data.nickname;
            Level = data.level;
            Wins = data.wins;
            Losses = data.losses;
            CharacterId = data.characterId;
            CharacterArtKey = data.characterArtKey;
            History = Array.AsReadOnly((data.recentMatches ?? Array.Empty<string>()).ToArray());
        }
    }
}
