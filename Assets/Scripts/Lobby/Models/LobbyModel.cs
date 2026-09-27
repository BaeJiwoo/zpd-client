using System;
using System.Collections.Generic;
using System.Linq;

namespace Zpd.Lobby
{
    /// <summary>Account snapshots and UI state only. No Unity objects, transport or optimistic inventory changes.</summary>
    public sealed class LobbyModel
    {
        public LobbyProfileSnapshot Profile { get; private set; }
        public IReadOnlyList<LobbyItemSnapshot> Items { get; private set; } = Array.AsReadOnly(Array.Empty<LobbyItemSnapshot>());
        public long InventoryRevision { get; private set; } = -1;
        public LobbyLoadState ProfileState { get; private set; }
        public LobbyLoadState InventoryState { get; private set; }
        public string ProfileError { get; private set; }
        public string InventoryError { get; private set; }
        public LobbyWindow Window { get; private set; }
        public int Filter { get; private set; }
        public string SelectedItemId { get; private set; }
        public bool IsUsingItem { get; private set; }
        public string OperationId { get; private set; }
        public string OperationItemId { get; private set; }
        public string UseError { get; private set; }
        public LobbyItemSnapshot SelectedItem => Items.FirstOrDefault(i => i.Id == SelectedItemId);
        public IEnumerable<LobbyItemSnapshot> VisibleItems => Items.Where(
            i => Filter == 0 || Filter == 1 && i.Kind == LobbyItemKind.Consumable || Filter == 2 && i.Kind == LobbyItemKind.Equipment);

        public void Open(LobbyWindow window)
        {
            Window = window;
            SelectedItemId = null;
        }

        public void SelectFilter(int value)
        {
            if (value >= 0 && value <= 2)
            {
                Filter = value;
            }
        }

        public void SelectItem(string id)
        {
            SelectedItemId = Items.Any(i => i.Id == id) ? id : null;
        }

        public void CloseItem()
        {
            SelectedItemId = null;
        }

        public void BeginProfile()
        {
            ProfileState = LobbyLoadState.Loading;
            ProfileError = null;
        }

        public void BeginInventory()
        {
            InventoryState = LobbyLoadState.Loading;
            InventoryError = null;
        }

        public void FailProfile(string error)
        {
            ProfileState = LobbyLoadState.Error;
            ProfileError = error;
        }

        public void FailInventory(string error)
        {
            InventoryState = LobbyLoadState.Error;
            InventoryError = error;
        }

        public bool ApplyProfile(LobbyProfileData data)
        {
            var snapshot = new LobbyProfileSnapshot(data);

            if (Profile != null && snapshot.Revision < Profile.Revision)
            {
                ProfileState = LobbyLoadState.Ready;
                return false;
            }

            Profile = snapshot;
            ProfileState = LobbyLoadState.Ready;
            ProfileError = null;
            return true;
        }

        public bool ApplyInventory(LobbyInventoryData data)
        {
            if (data == null || data.revision < 0 || data.items == null)
            {
                throw new ArgumentException("Invalid inventory response.");
            }

            var snapshot = data.items.Select(i => new LobbyItemSnapshot(i)).ToArray();

            if (snapshot.Select(i => i.Id).Distinct().Count() != snapshot.Length)
            {
                throw new ArgumentException("Duplicate inventory item IDs.");
            }

            if (data.revision < InventoryRevision)
            {
                InventoryState = LobbyLoadState.Ready;
                return false;
            }

            Items = Array.AsReadOnly(snapshot.Where(i => i.Quantity > 0).ToArray());
            InventoryRevision = data.revision;
            InventoryState = LobbyLoadState.Ready;
            InventoryError = null;

            if (SelectedItem == null)
            {
                SelectedItemId = null;
            }

            return true;
        }

        public bool CanUseSelected => SelectedItem != null && SelectedItem.Kind == LobbyItemKind.Consumable && SelectedItem.Quantity > 0 && !IsUsingItem && (OperationId == null || OperationItemId == SelectedItemId);

        public bool BeginUse()
        {
            if (!CanUseSelected)
            {
                return false;
            }

            OperationId = OperationId ?? Guid.NewGuid().ToString("N");
            OperationItemId = SelectedItemId;
            IsUsingItem = true;
            UseError = null;
            return true;
        }

        public void CompleteUse()
        {
            IsUsingItem = false;
            OperationId = null;
            OperationItemId = null;
            UseError = null;
        }

        public void FailUse(string error, bool outcomeUnknown)
        {
            IsUsingItem = false;
            UseError = error;

            if (!outcomeUnknown)
            {
                OperationId = null;
                OperationItemId = null;
            }
        }
    }
}
