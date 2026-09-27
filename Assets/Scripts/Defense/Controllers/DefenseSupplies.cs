using System;
using UnityEngine;
using UnityEngine.UI;
using Zpd.Gameplay;

namespace Zpd.Defense
{
    [RequireComponent(typeof(DefenseSuppliesView))]
    public sealed class DefenseSupplies : MonoBehaviour
    {
        private DefenseSuppliesView view;

        public DefenseSuppliesView View => view != null ? view : (view = GetComponent<DefenseSuppliesView>());

        [Serializable]
        public sealed class GoldSlot
        {
            public Transform root;

            [NonSerialized]
            public int amount;
        }

        public DefenseGame game;
        public GoldSlot[] drops;

        public GameObject shopPanel { get => View.shopPanel; set => View.shopPanel = value; }
        public Text walletText { get => View.walletText; set => View.walletText = value; }
        public Text shopTitle { get => View.shopTitle; set => View.shopTitle = value; }
        public Text shopMessage { get => View.shopMessage; set => View.shopMessage = value; }
        public Button[] cardButtons { get => View.cardButtons; set => View.cardButtons = value; }
        public Text[] cardLabels { get => View.cardLabels; set => View.cardLabels = value; }
        public Button healButton { get => View.healButton; set => View.healButton = value; }
        public Button repairButton { get => View.repairButton; set => View.repairButton = value; }
        public Text healLabel { get => View.healLabel; set => View.healLabel = value; }
        public Text repairLabel { get => View.repairLabel; set => View.repairLabel = value; }
        public SpriteRenderer weaponRenderer { get => View.weaponRenderer; set => View.weaponRenderer = value; }
        public Sprite baseWeaponSprite { get => View.baseWeaponSprite; set => View.baseWeaponSprite = value; }

        [Min(1)]
        public int goldPerKill = 8;

        [Min(1)]
        public int healPrice = 15, healAmount = 35, repairPrice = 25, repairAmount = 15;

        [Min(0.1f)]
        public float pickupRadius = 1.4f;

        public DefenseSuppliesModel Model { get; } = new DefenseSuppliesModel();
        public DefenseCombatStats Stats => Model.Stats;
        public int Gold => Model.Gold;
        public bool ShopOpen => Model.ShopOpen;
        public float ShopRemaining => ShopOpen ? game.PhaseRemaining : 0;
        public bool RepairedThisWave => Model.RepairedThisWave;
        public bool CardChosen => Model.CardChosen;
        public bool AwaitingCard => ShopOpen && !CardChosen;
        public float FireInterval => Stats.FireInterval;
        public int PelletCount => Stats.ProjectileCount;
        public int Damage => Stats.Damage;

        public void ResetRun()
        {
            Model.Reset();

            foreach (var slot in drops)
            {
                slot.amount = 0;
                slot.root.gameObject.SetActive(false);
            }

            View.ShowShop(false);
            View.ResetWeapon();

            if (GameSessionTracker.Instance != null && GameSessionTracker.Instance.IsRunning)
            {
                GameSessionTracker.Instance.SetWeapon("SIGNAL_BLASTER");
            }

            Refresh();
        }

        public void EnemyKilled(Vector2 position)
        {
            GoldSlot free = null, nearest = drops[0];
            float distance = float.MaxValue;

            foreach (var slot in drops)
            {
                if (!slot.root.gameObject.activeSelf)
                {
                    free = slot;
                    break;
                }

                float d = ((Vector2)slot.root.position - position).sqrMagnitude;

                if (d < distance)
                {
                    distance = d;
                    nearest = slot;
                }
            }

            var target = free ?? nearest;

            if (free != null)
            {
                target.amount = 0;
                target.root.position = position;
                target.root.gameObject.SetActive(true);
            }

            target.amount += goldPerKill;
            GameSessionTracker.Instance.Record("gold_dropped", goldPerKill, position);
        }

        public void Tick(float dt)
        {
            if (game.State != DefenseState.Playing)
            {
                return;
            }

            foreach (var slot in drops)
            {
                if (!slot.root.gameObject.activeSelf)
                {
                    continue;
                }

                float d = Vector2.Distance(slot.root.position, game.player.position);

                if (d <= pickupRadius)
                {
                    Model.Collect(slot.amount);
                    GameSessionTracker.Instance.GoldCollected(slot.amount, game.player.position);
                    slot.amount = 0;
                    slot.root.gameObject.SetActive(false);
                    game.audioEffects.Play(DefenseCue.Pickup);
                    game.PickupFeedback(game.player.position);
                }
                else if (d < pickupRadius + 1)
                {
                    slot.root.position = Vector2.MoveTowards(slot.root.position, game.player.position, dt * 5);
                }
            }

            Refresh();
        }

        public void OpenShop()
        {
            if (ShopOpen || game.State != DefenseState.Playing || game.Phase != DefensePhase.Preparation)
            {
                return;
            }

            Model.Open();
            View.ShowShop(true);
            View.ShowMessage("");
            game.audioEffects.Play(DefenseCue.Shop);
            GameSessionTracker.Instance.Record("shop_opened", game.Wave, game.player.position);
            Refresh();
        }

        public void CloseShop()
        {
            if (ShopOpen)
            {
                GameSessionTracker.Instance.Record("shop_closed", game.Wave, game.player.position);
            }

            Model.Close();
            View.ShowShop(false);
            Refresh();
        }

        public void ChooseProjectiles() => ChooseCard(0);

        public void ChooseDamage() => ChooseCard(1);

        public void ChooseFireRate() => ChooseCard(2);

        public bool ChooseCard(int index)
        {
            if (!Model.TryChooseCard(index, game.Model))
            {
                return false;
            }

            var command = DefenseUpgradeCatalog.At(index);
            GameSessionTracker.Instance.Record("upgrade_" + command.Id, game.Wave, game.player.position);
            game.audioEffects.Play(DefenseCue.Purchase);
            game.PickupFeedback(game.player.position);
            View.ShowMessage("Upgrade applied for this run.");
            Refresh();
            return true;
        }

        public void BuyHeal()
        {
            if (!CanShop() || game.PlayerHealth >= 100 || !Spend(healPrice))
            {
                return;
            }

            int restored = game.Heal(healAmount);
            GameSessionTracker.Instance.Record("healed", restored, game.player.position);
            game.audioEffects.Play(DefenseCue.Purchase);
            View.ShowMessage("+" + restored + " HEALTH");
            Refresh();
        }

        public void BuyRepair()
        {
            if (!CanShop() || RepairedThisWave || game.BeaconHealth >= 100 || !Spend(repairPrice))
            {
                return;
            }

            int restored = game.RepairBeacon(repairAmount);
            Model.MarkRepaired();
            GameSessionTracker.Instance.Record("beacon_repaired", restored, game.beacon.position);
            game.audioEffects.Play(DefenseCue.Purchase);
            View.ShowMessage("+" + restored + " BEACON");
            Refresh();
        }

        private bool CanShop() => Model.CanShop(game.Model);

        private bool Spend(int amount)
        {
            if (!Model.TrySpend(amount))
            {
                View.ShowMessage("Not enough gold.");
                return false;
            }

            GameSessionTracker.Instance.GoldSpent(amount, game.player.position);
            return true;
        }

        public void CloseForEnd()
        {
            Model.Close();
            View.ShowShop(false);
        }

        public void Refresh() => View.Render(
            Model,
            game.Model,
            healPrice,
            healAmount,
            repairPrice,
            repairAmount,
            game.nextWaveButton);
    }
}
