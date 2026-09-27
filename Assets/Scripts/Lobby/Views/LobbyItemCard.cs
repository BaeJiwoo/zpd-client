using System;
using UnityEngine;
using UnityEngine.UI;

namespace Zpd.Lobby
{
    public sealed class LobbyItemCard : MonoBehaviour
    {
        public Text title, category, quantity;
        public Image icon;
        public Slider amount;
        public Button button;

        public void Bind(LobbyItemSnapshot item, Sprite sprite, Action<string> selected)
        {
            title.text = item.Name;
            category.text = item.Kind == LobbyItemKind.Consumable ? "CONSUMABLE" : "EQUIPMENT";
            icon.sprite = sprite;
            icon.enabled = sprite != null;
            bool consumable = item.Kind == LobbyItemKind.Consumable;
            amount.gameObject.SetActive(consumable);
            quantity.gameObject.SetActive(consumable);
            amount.maxValue = Mathf.Max(1, item.Capacity);
            amount.value = item.Quantity;
            quantity.text = "OWNED  " + item.Quantity;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => selected(item.Id));
        }
    }
}
