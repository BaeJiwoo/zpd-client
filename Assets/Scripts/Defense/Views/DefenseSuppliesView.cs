using UnityEngine;
using UnityEngine.UI;

namespace Zpd.Defense
{
    public sealed class DefenseSuppliesView : MonoBehaviour
    {
        public GameObject shopPanel;
        public Text walletText;
        public Text shopTitle;
        public Text shopMessage;
        public Button[] cardButtons = new Button[0];
        public Text[] cardLabels = new Text[0];
        public Button healButton;
        public Button repairButton;
        public Text healLabel;
        public Text repairLabel;
        public SpriteRenderer weaponRenderer;
        public Sprite baseWeaponSprite;

        public void ShowMessage(string message) => shopMessage.text = message;

        public void ShowShop(bool show) => shopPanel.SetActive(show);

        public void ResetWeapon()
        {
            if (baseWeaponSprite == null)
            {
                return;
            }

            weaponRenderer.sprite = baseWeaponSprite;
            weaponRenderer.transform.localScale = Vector3.one * 0.38f / baseWeaponSprite.bounds.size.y;
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
            walletText.text = "GOLD " + model.Gold + "   /   SHOTS " + model.Stats.ProjectileCount + " / DAMAGE " + model.Stats.Damage + "\nRate: " + (1 / model.Stats.FireInterval).ToString("0.0") + " shots/s";
            shopTitle.text = model.AwaitingCard
                ? "CHOOSE ONE FREE UPGRADE"
                : "NEXT WAVE / " + Mathf.CeilToInt(run.PhaseRemaining) + "s";
            bool can = model.CanShop(run);

            for (int i = 0; i < cardButtons.Length; i++)
            {
                var command = DefenseUpgradeCatalog.At(i);
                bool eligible = command != null && command.CanApply(model.Stats);
                cardButtons[i].interactable = can && !model.CardChosen && eligible;
                cardLabels[i].text = (i + 1) + "  " + (model.ShopOpen
                    ? model.OfferDescriptions[i]
                    : command.Describe(model.Stats)) + (model.ShopOpen && model.SelectedCard == i
                    ? "\nSELECTED"
                    : !eligible
                    ? "\nMAX LEVEL"
                    : model.CardChosen ? "\nNEXT SUPPLY BREAK" : "\nCHOOSE");
            }

            healButton.interactable = can && model.Gold >= healPrice && run.PlayerHealth < 100;
            repairButton.interactable = can && !model.RepairedThisWave && model.Gold >= repairPrice && run.BeaconHealth < 100;
            healLabel.text = "4  HEAL +" + healAmount + " / " + healPrice + "G";
            repairLabel.text = model.RepairedThisWave
                ? "BEACON REPAIRED"
                : "5  REPAIR +" + repairAmount + " / " + repairPrice + "G";

            if (nextWaveButton != null)
            {
                nextWaveButton.interactable = can && model.CardChosen;
            }
        }
    }
}
