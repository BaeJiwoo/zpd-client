using System;
using System.Globalization;
using Zpd.Networking;
using static Zpd.Networking.DTO.LobbyApiDtos;

namespace Zpd.Lobby
{
    internal static class LobbyResponseParser
    {
        public static LobbyProfileData ParseProfile(ProfileResponse data)
        {
            return Profile(data);
        }

        public static LobbyInventoryData ParseInventory(InventoryResponse data)
        {
            return Inventory(data);
        }

        public static LobbyUseItemResult ParseItemUse(UseResponse data)
        {
            try
            {

                if (data == null)
                {
                    throw Invalid();
                }

                // JsonUtility may materialize omitted nested objects with default fields.

                bool hasProfile = data.profile != null && (data.profile.revision != null || data.profile.displayName != null);

                return new LobbyUseItemResult
                {
                    inventory = Inventory(data.inventory),
                    profile = hasProfile ? Profile(data.profile) : null
                };
            }
            catch (Exception)
            {
                throw new LobbyServiceException(ApiErrorCode.InvalidItemUseResponse, outcomeUnknown: true);
            }
        }

        private static LobbyServiceException Invalid() => new LobbyServiceException(ApiErrorCode.InvalidResponse);

        private static long Revision(string value)
        {
            if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out long revision) || revision < 0)
            {
                throw Invalid();
            }

            return revision;
        }

        private static LobbyProfileData Profile(ProfileResponse data)
        {
            if (data == null || data.stats == null || data.recentMatches == null)
            {
                throw Invalid();
            }

            var result = new LobbyProfileData
            {
                revision = Revision(data.revision),
                nickname = data.displayName,
                level = data.level,
                wins = data.stats.wins,
                losses = data.stats.losses,
                characterId = data.currentCharacterId,
                characterArtKey = data.characterArtKey,
                recentMatches = data.recentMatches
            };

            try
            {
                _ = new LobbyProfileSnapshot(result);
            }
            catch (ArgumentException)
            {
                throw Invalid();
            }

            return result;
        }

        private static LobbyInventoryData Inventory(InventoryResponse data)
        {
            // This contract returns a complete snapshot. Never replace inventory with a partial page.

            if (data == null || data.items == null || !string.IsNullOrEmpty(data.nextCursor))
            {
                throw Invalid();
            }

            var result = new LobbyInventoryData
            {
                revision = Revision(data.revision),
                items = new LobbyItemData[data.items.Length]
            };

            for (int i = 0; i < data.items.Length; i++)
            {
                result.items[i] = Item(data.items[i]);
            }

            try
            {
                new LobbyModel().ApplyInventory(result);
            }
            catch (ArgumentException)
            {
                throw Invalid();
            }

            return result;
        }

        private static LobbyItemData Item(ItemResponse item)
        {
            if (item == null || (item.kind != "consumable" && item.kind != "equipment") || string.IsNullOrWhiteSpace(item.name))
            {
                throw Invalid();
            }

            return new LobbyItemData
            {
                id = item.instanceId,
                name = item.name,
                description = item.description,
                iconKey = item.artKey,
                kind = item.kind == "consumable" ? LobbyItemKind.Consumable : LobbyItemKind.Equipment,
                quantity = item.quantity,
                capacity = item.capacity
            };
        }
    }
}
