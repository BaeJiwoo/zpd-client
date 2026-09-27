using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Zpd.Defense
{
    public sealed class DefenseSuppliesView : MonoBehaviour
    {
        [FormerlySerializedAs("shopPanel")]
        public GameObject game_object_shop_panel;

        [FormerlySerializedAs("walletText")]
        public Text txt_wallet;

        [FormerlySerializedAs("shopTitle")]
        public Text txt_shop_title;

        [FormerlySerializedAs("shopMessage")]
        public Text txt_shop_message;

        [FormerlySerializedAs("cardButtons")]
        public Button[] btn_upgrade_cards = new Button[0];

        [FormerlySerializedAs("cardLabels")]
        public Text[] txt_upgrade_cards = new Text[0];

        [FormerlySerializedAs("healButton")]
        public Button btn_heal;

        [FormerlySerializedAs("repairButton")]
        public Button btn_repair;

        [FormerlySerializedAs("healLabel")]
        public Text txt_heal;

        [FormerlySerializedAs("repairLabel")]
        public Text txt_repair;

        [FormerlySerializedAs("weaponRenderer")]
        public SpriteRenderer sprite_renderer_weapon;

        [FormerlySerializedAs("baseWeaponSprite")]
        public Sprite sprite_base_weapon;

        public void ShowMessage(string message) => txt_shop_message.text = message;

        public void ShowShop(bool show) => game_object_shop_panel.SetActive(show);

        public void ResetWeapon()
        {
            if (sprite_base_weapon == null)
            {
                return;
            }

            sprite_renderer_weapon.sprite = sprite_base_weapon;
            sprite_renderer_weapon.transform.localScale = Vector3.one * 0.38f / sprite_base_weapon.bounds.size.y;
        }

        public void Render(
            DefenseSuppliesModel model,
            DefenseModel run,
            int healPrice,
            int healAmount,
            int repairPrice,
            int repairAmount,
            Button nextWaveButton)
        {
            txt_wallet.text = "GOLD " + model.Gold + "   /   SHOTS " + model.Stats.ProjectileCount + " / DAMAGE " + model.Stats.Damage + "\nRate: " + (1 / model.Stats.FireInterval).ToString("0.0") + " shots/s";
            txt_shop_title.text = model.AwaitingCard
                ? "CHOOSE ONE FREE UPGRADE"
                : "NEXT WAVE / " + Mathf.CeilToInt(run.PhaseRemaining) + "s";
            bool can = model.CanShop(run);

            for (int i = 0; i < btn_upgrade_cards.Length; i++)
            {
                var command = DefenseUpgradeCatalog.At(i);
                bool eligible = command != null && command.CanApply(model.Stats);
                btn_upgrade_cards[i].interactable = can && !model.CardChosen && eligible;
                txt_upgrade_cards[i].text = (i + 1) + "  " + (model.ShopOpen
                    ? model.OfferDescriptions[i]
                    : command.Describe(model.Stats)) + (model.ShopOpen && model.SelectedCard == i
                    ? "\nSELECTED"
                    : !eligible
                    ? "\nMAX LEVEL"
                    : model.CardChosen ? "\nNEXT SUPPLY BREAK" : "\nCHOOSE");
            }

            btn_heal.interactable = can && model.Gold >= healPrice && run.PlayerHealth < 100;
            btn_repair.interactable = can && !model.RepairedThisWave && model.Gold >= repairPrice && run.BeaconHealth < 100;
            txt_heal.text = "4  HEAL +" + healAmount + " / " + healPrice + "G";
            txt_repair.text = model.RepairedThisWave
                ? "BEACON REPAIRED"
                : "5  REPAIR +" + repairAmount + " / " + repairPrice + "G";

            if (nextWaveButton != null)
            {
                nextWaveButton.interactable = can && model.CardChosen;
            }
        }
    }
}
