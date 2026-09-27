using System;

namespace Zpd.Lobby
{
    public sealed class LobbyItemSnapshot
    {
        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public string IconKey { get; }
        public LobbyItemKind Kind { get; }
        public int Quantity { get; }
        public int Capacity { get; }

        public LobbyItemSnapshot(LobbyItemData data)
        {
            if (data == null || string.IsNullOrWhiteSpace(data.id) || data.quantity < 0 || data.capacity < 0 || !Enum.IsDefined(typeof(LobbyItemKind), data.kind))
            {
                throw new ArgumentException("Invalid inventory item response.");
            }

            Id = data.id;
            Name = data.name ?? "";
            Description = data.description ?? "";
            IconKey = data.iconKey;
            Kind = data.kind;
            Quantity = data.quantity;
            Capacity = Math.Max(data.capacity, data.quantity);
        }
    }
}
