using System;

namespace Zpd.Networking.DTO
{
    // Wire DTOs only; response validation and mapping belong to LobbyResponseParser.

    internal static class LobbyApiDtos
    {
        [Serializable]
        public sealed class UseRequest
        {
            public int quantity;
        }

        [Serializable]
        public sealed class ProfileEnvelope
        {
            public ProfileResponse data;
        }

        [Serializable]
        public sealed class InventoryEnvelope
        {
            public InventoryResponse data;
        }

        [Serializable]
        public sealed class UseEnvelope
        {
            public UseResponse data;
        }

        [Serializable]
        public sealed class UseResponse
        {
            public InventoryResponse inventory;
            public ProfileResponse profile;
        }

        [Serializable]
        public sealed class ProfileResponse
        {
            public string revision;
            public string displayName;
            public string currentCharacterId;
            public string characterArtKey;
            public int level = -1;
            public StatsResponse stats;
            public string[] recentMatches;
        }

        [Serializable]
        public sealed class StatsResponse
        {
            public int wins = -1;
            public int losses = -1;
        }

        [Serializable]
        public sealed class InventoryResponse
        {
            public string revision;
            public string nextCursor;
            public ItemResponse[] items;
        }

        [Serializable]
        public sealed class ItemResponse
        {
            public string instanceId;
            public string name;
            public string description;
            public string artKey;
            public string kind;
            public int quantity = -1;
            public int capacity = -1;
        }
    }
}
