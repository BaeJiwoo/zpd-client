using System;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Zpd.Lobby
{
    public sealed class LobbyItemCard : MonoBehaviour
    {
        [FormerlySerializedAs("title")]
        public Text txt_title;

        [FormerlySerializedAs("category")]
        public Text txt_category;

        [FormerlySerializedAs("quantity")]
        public Text txt_quantity;

        [FormerlySerializedAs("icon")]
        public Image img_icon;

        [FormerlySerializedAs("amount")]
        public Slider slider_owned_quantity;

        [FormerlySerializedAs("button")]
        public Button btn_select_item;

        public void Bind(LobbyItemSnapshot item, Sprite sprite, Action<string> selected)
        {
            txt_title.text = item.Name;
            txt_category.text = item.Kind == LobbyItemKind.Consumable ? "CONSUMABLE" : "EQUIPMENT";
            img_icon.sprite = sprite;
            img_icon.enabled = sprite != null;
            bool consumable = item.Kind == LobbyItemKind.Consumable;
            slider_owned_quantity.gameObject.SetActive(consumable);
            txt_quantity.gameObject.SetActive(consumable);
            slider_owned_quantity.maxValue = Mathf.Max(1, item.Capacity);
            slider_owned_quantity.value = item.Quantity;
            txt_quantity.text = "OWNED  " + item.Quantity;
            btn_select_item.onClick.RemoveAllListeners();
            btn_select_item.onClick.AddListener(() => selected(item.Id));
        }
    }
}
