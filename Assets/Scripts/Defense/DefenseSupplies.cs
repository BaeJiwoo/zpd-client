using System;
using UnityEngine;
using UnityEngine.UI;
using Zpd.Gameplay;

namespace Zpd.Defense
{
    public sealed class DefenseSupplies : MonoBehaviour
    {
        [Serializable] public sealed class GoldSlot { public Transform root; [NonSerialized] public int amount; }
        public DefenseGame game;
        public GoldSlot[] drops;
        public GameObject shopPanel;
        public Text walletText, shopTitle, shopMessage;
        public Button[] cardButtons = new Button[0];
        public Text[] cardLabels = new Text[0];
        public Button healButton, repairButton;
        public Text healLabel, repairLabel;
        public SpriteRenderer weaponRenderer;
        public Sprite baseWeaponSprite;
        [Min(1)] public int goldPerKill = 8;
        [Min(1)] public int healPrice = 15, healAmount = 35, repairPrice = 25, repairAmount = 15;
        [Min(0.1f)] public float pickupRadius = 1.4f;
        public DefenseCombatStats Stats { get; } = new DefenseCombatStats();
        public int Gold { get; private set; }
        public bool ShopOpen { get; private set; }
        public float ShopRemaining => ShopOpen ? game.PhaseRemaining : 0;
        public bool RepairedThisWave { get; private set; }
        public bool CardChosen { get; private set; }
        public bool AwaitingCard => ShopOpen && !CardChosen;
        public float FireInterval => Stats.FireInterval;
        public int PelletCount => Stats.ProjectileCount;
        public int Damage => Stats.Damage;
        private int selectedCard = -1;
        private readonly string[] offerDescriptions = new string[3];

        public void ResetRun()
        {
            Gold = 0; Stats.Reset(); ShopOpen = RepairedThisWave = CardChosen = false; selectedCard = -1;
            foreach (var slot in drops) { slot.amount = 0; slot.root.gameObject.SetActive(false); }
            shopPanel.SetActive(false);
            if (baseWeaponSprite != null)
            {
                weaponRenderer.sprite = baseWeaponSprite;
                weaponRenderer.transform.localScale = Vector3.one * 0.38f / baseWeaponSprite.bounds.size.y;
            }
            if (GameSessionTracker.Instance != null && GameSessionTracker.Instance.IsRunning)
                GameSessionTracker.Instance.SetWeapon("SIGNAL_BLASTER");
            Refresh();
        }

        public void EnemyKilled(Vector2 position)
        {
            GoldSlot free = null, nearest = drops[0];
            float distance = float.MaxValue;
            foreach (var slot in drops)
            {
                if (!slot.root.gameObject.activeSelf) { free = slot; break; }
                float d = ((Vector2)slot.root.position - position).sqrMagnitude;
                if (d < distance) { distance = d; nearest = slot; }
            }
            var target = free ?? nearest;
            if (free != null) { target.amount = 0; target.root.position = position; target.root.gameObject.SetActive(true); }
            target.amount += goldPerKill;
            GameSessionTracker.Instance.Record("gold_dropped", goldPerKill, position);
        }

        public void Tick(float dt)
        {
            if (game.State != DefenseState.Playing) return;
            foreach (var slot in drops)
            {
                if (!slot.root.gameObject.activeSelf) continue;
                float d = Vector2.Distance(slot.root.position, game.player.position);
                if (d <= pickupRadius)
                {
                    Gold += slot.amount;
                    GameSessionTracker.Instance.GoldCollected(slot.amount, game.player.position);
                    slot.amount = 0; slot.root.gameObject.SetActive(false);
                    game.audioEffects.Play(DefenseCue.Pickup);
                    game.PickupFeedback(game.player.position);
                }
                else if (d < pickupRadius + 1)
                    slot.root.position = Vector2.MoveTowards(slot.root.position, game.player.position, dt * 5);
            }
            Refresh();
        }

        public void OpenShop()
        {
            if (ShopOpen || game.State != DefenseState.Playing || game.Phase != DefensePhase.Preparation) return;
            ShopOpen = true; RepairedThisWave = false; selectedCard = -1;
            CardChosen = true;
            for (int i = 0; i < 3; i++)
            {
                var command = DefenseUpgradeCatalog.At(i);
                offerDescriptions[i] = command.Describe(Stats);
                if (command.CanApply(Stats)) CardChosen = false;
            }
            shopPanel.SetActive(true); shopMessage.text = "";
            game.audioEffects.Play(DefenseCue.Shop);
            GameSessionTracker.Instance.Record("shop_opened", game.Wave, game.player.position);
            Refresh();
        }

        public void CloseShop()
        {
            if (ShopOpen) GameSessionTracker.Instance.Record("shop_closed", game.Wave, game.player.position);
            ShopOpen = false; shopPanel.SetActive(false); Refresh();
        }

        public void ChooseProjectiles() => ChooseCard(0);
        public void ChooseDamage() => ChooseCard(1);
        public void ChooseFireRate() => ChooseCard(2);
        public bool ChooseCard(int index)
        {
            var command = DefenseUpgradeCatalog.At(index);
            if (!CanShop() || CardChosen || command == null || !command.CanApply(Stats)) return false;
            command.Apply(Stats);
            selectedCard = index;
            CardChosen = true;
            GameSessionTracker.Instance.Record("upgrade_" + command.Id, game.Wave, game.player.position);
            game.audioEffects.Play(DefenseCue.Purchase);
            game.PickupFeedback(game.player.position);
            shopMessage.text = "강화 완료 · 이번 판에 계속 적용됩니다";
            Refresh();
            return true;
        }
        public void BuyHeal()
        {
            if (!CanShop() || game.PlayerHealth >= 100 || !Spend(healPrice)) return;
            int restored = game.Heal(healAmount);
            GameSessionTracker.Instance.Record("healed", restored, game.player.position);
            game.audioEffects.Play(DefenseCue.Purchase); shopMessage.text = "+" + restored + " HEALTH"; Refresh();
        }
        public void BuyRepair()
        {
            if (!CanShop() || RepairedThisWave || game.BeaconHealth >= 100 || !Spend(repairPrice)) return;
            int restored = game.RepairBeacon(repairAmount);
            RepairedThisWave = true;
            GameSessionTracker.Instance.Record("beacon_repaired", restored, game.beacon.position);
            game.audioEffects.Play(DefenseCue.Purchase);
            shopMessage.text = "+" + restored + " BEACON"; Refresh();
        }
        private bool CanShop() => game.State == DefenseState.Playing && game.Phase == DefensePhase.Preparation && ShopOpen && game.PhaseRemaining > 0;
        private bool Spend(int amount)
        {
            if (Gold < amount) { shopMessage.text = "골드가 부족합니다."; return false; }
            Gold -= amount; GameSessionTracker.Instance.GoldSpent(amount, game.player.position); return true;
        }
        public void CloseForEnd() { ShopOpen = false; shopPanel.SetActive(false); }
        public void Refresh()
        {
            walletText.text = "GOLD " + Gold + "   /   탄환 " + PelletCount + " · 피해 " + Damage + "\n초당 " + (1 / FireInterval).ToString("0.0") + "회 발사";
            shopTitle.text = AwaitingCard ? "강화 카드 1장 선택 · 무료" : "다음 웨이브 준비 / " + Mathf.CeilToInt(ShopRemaining) + "s";
            bool can = CanShop();
            for (int i = 0; i < cardButtons.Length; i++)
            {
                var command = DefenseUpgradeCatalog.At(i);
                bool eligible = command != null && command.CanApply(Stats);
                cardButtons[i].interactable = can && !CardChosen && eligible;
                cardLabels[i].text = (i + 1) + "  " + (ShopOpen ? offerDescriptions[i] : command.Describe(Stats))
                    + (ShopOpen && selectedCard == i ? "\n선택 완료" : !eligible ? "\n최대 강화" : CardChosen ? "\n다음 보급에 선택 가능" : "\n선택");
            }
            healButton.interactable = can && Gold >= healPrice && game.PlayerHealth < 100;
            repairButton.interactable = can && !RepairedThisWave && Gold >= repairPrice && game.BeaconHealth < 100;
            healLabel.text = "4  회복 +" + healAmount + " / " + healPrice + "G";
            repairLabel.text = RepairedThisWave ? "이번 보급 수리 완료" : "5  비콘 +" + repairAmount + " / " + repairPrice + "G";
            if (game.nextWaveButton != null) game.nextWaveButton.interactable = can && CardChosen;
        }
    }
}
