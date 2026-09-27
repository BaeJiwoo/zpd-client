using System;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Zpd.Gameplay;

namespace Zpd.Defense
{
    [RequireComponent(typeof(DefenseSuppliesView))]
    public sealed class DefenseSupplies : MonoBehaviour
    {
        private DefenseSuppliesView defense_supplies_view;

        public DefenseSuppliesView View => defense_supplies_view != null ? defense_supplies_view : (defense_supplies_view = GetComponent<DefenseSuppliesView>());

        [Serializable]
        public sealed class GoldSlot
        {
            [FormerlySerializedAs("root")]
            public Transform transform_root;

            [NonSerialized]
            public int gold_amount;
        }

        [FormerlySerializedAs("game")]
        public DefenseGame defense_game;

        [FormerlySerializedAs("drops")]
        public GoldSlot[] gold_drop_slots;

        public GameObject game_object_shop_panel { get => View.game_object_shop_panel; set => View.game_object_shop_panel = value; }
        public Text txt_wallet { get => View.txt_wallet; set => View.txt_wallet = value; }
        public Text txt_shop_title { get => View.txt_shop_title; set => View.txt_shop_title = value; }
        public Text txt_shop_message { get => View.txt_shop_message; set => View.txt_shop_message = value; }
        public Button[] btn_upgrade_cards { get => View.btn_upgrade_cards; set => View.btn_upgrade_cards = value; }
        public Text[] txt_upgrade_cards { get => View.txt_upgrade_cards; set => View.txt_upgrade_cards = value; }
        public Button btn_heal { get => View.btn_heal; set => View.btn_heal = value; }
        public Button btn_repair { get => View.btn_repair; set => View.btn_repair = value; }
        public Text txt_heal { get => View.txt_heal; set => View.txt_heal = value; }
        public Text txt_repair { get => View.txt_repair; set => View.txt_repair = value; }
        public SpriteRenderer sprite_renderer_weapon { get => View.sprite_renderer_weapon; set => View.sprite_renderer_weapon = value; }
        public Sprite sprite_base_weapon { get => View.sprite_base_weapon; set => View.sprite_base_weapon = value; }

        [FormerlySerializedAs("goldPerKill")]
        [Min(1)]
        public int gold_per_kill = 8;

        [FormerlySerializedAs("healPrice")]
        [Min(1)]
        public int heal_price = 15;

        [FormerlySerializedAs("healAmount")]
        [Min(1)]
        public int heal_amount = 35;

        [FormerlySerializedAs("repairPrice")]
        [Min(1)]
        public int repair_price = 25;

        [FormerlySerializedAs("repairAmount")]
        [Min(1)]
        public int repair_amount = 15;

        [FormerlySerializedAs("pickupRadius")]
        [Min(0.1f)]
        public float pickup_radius = 1.4f;

        public DefenseSuppliesModel Model { get; } = new DefenseSuppliesModel();
        public DefenseCombatStats Stats => Model.Stats;
        public int Gold => Model.Gold;
        public bool ShopOpen => Model.ShopOpen;
        public float ShopRemaining => ShopOpen ? defense_game.PhaseRemaining : 0;
        public bool RepairedThisWave => Model.RepairedThisWave;
        public bool CardChosen => Model.CardChosen;
        public bool AwaitingCard => ShopOpen && !CardChosen;
        public float FireInterval => Stats.FireInterval;
        public int PelletCount => Stats.ProjectileCount;
        public int Damage => Stats.Damage;

        public void ResetRun()
        {
            Model.Reset();

            foreach (var slot in gold_drop_slots)
            {
                slot.gold_amount = 0;
                slot.transform_root.gameObject.SetActive(false);
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
            GoldSlot free = null, nearest = gold_drop_slots[0];
            float distance = float.MaxValue;

            foreach (var slot in gold_drop_slots)
            {
                if (!slot.transform_root.gameObject.activeSelf)
                {
                    free = slot;
                    break;
                }

                float d = ((Vector2)slot.transform_root.position - position).sqrMagnitude;

                if (d < distance)
                {
                    distance = d;
                    nearest = slot;
                }
            }

            var target = free ?? nearest;

            if (free != null)
            {
                target.gold_amount = 0;
                target.transform_root.position = position;
                target.transform_root.gameObject.SetActive(true);
            }

            target.gold_amount += gold_per_kill;
            GameSessionTracker.Instance.Record("gold_dropped", gold_per_kill, position);
        }

        public void Tick(float dt)
        {
            if (defense_game.State != DefenseState.Playing)
            {
                return;
            }

            foreach (var slot in gold_drop_slots)
            {
                if (!slot.transform_root.gameObject.activeSelf)
                {
                    continue;
                }

                float d = Vector2.Distance(slot.transform_root.position, defense_game.transform_player.position);

                if (d <= pickup_radius)
                {
                    Model.Collect(slot.gold_amount);
                    GameSessionTracker.Instance.GoldCollected(slot.gold_amount, defense_game.transform_player.position);
                    slot.gold_amount = 0;
                    slot.transform_root.gameObject.SetActive(false);
                    defense_game.defense_audio.Play(DefenseCue.Pickup);
                    defense_game.PickupFeedback(defense_game.transform_player.position);
                }
                else if (d < pickup_radius + 1)
                {
                    slot.transform_root.position = Vector2.MoveTowards(slot.transform_root.position, defense_game.transform_player.position, dt * 5);
                }
            }

            Refresh();
        }

        public void OpenShop()
        {
            if (ShopOpen || defense_game.State != DefenseState.Playing || defense_game.Phase != DefensePhase.Preparation)
            {
                return;
            }

            Model.Open();
            View.ShowShop(true);
            View.ShowMessage("");
            defense_game.defense_audio.Play(DefenseCue.Shop);
            GameSessionTracker.Instance.Record("shop_opened", defense_game.Wave, defense_game.transform_player.position);
            Refresh();
        }

        public void CloseShop()
        {
            if (ShopOpen)
            {
                GameSessionTracker.Instance.Record("shop_closed", defense_game.Wave, defense_game.transform_player.position);
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
            if (!Model.TryChooseCard(index, defense_game.Model))
            {
                return false;
            }

            var command = DefenseUpgradeCatalog.At(index);
            GameSessionTracker.Instance.Record("upgrade_" + command.Id, defense_game.Wave, defense_game.transform_player.position);
            defense_game.defense_audio.Play(DefenseCue.Purchase);
            defense_game.PickupFeedback(defense_game.transform_player.position);
            View.ShowMessage("Upgrade applied for this run.");
            Refresh();
            return true;
        }

        public void BuyHeal()
        {
            if (!CanShop() || defense_game.PlayerHealth >= 100 || !Spend(heal_price))
            {
                return;
            }

            int restored = defense_game.Heal(heal_amount);
            GameSessionTracker.Instance.Record("healed", restored, defense_game.transform_player.position);
            defense_game.defense_audio.Play(DefenseCue.Purchase);
            View.ShowMessage("+" + restored + " HEALTH");
            Refresh();
        }

        public void BuyRepair()
        {
            if (!CanShop() || RepairedThisWave || defense_game.BeaconHealth >= 100 || !Spend(repair_price))
            {
                return;
            }

            int restored = defense_game.RepairBeacon(repair_amount);
            Model.MarkRepaired();
            GameSessionTracker.Instance.Record("beacon_repaired", restored, defense_game.transform_beacon.position);
            defense_game.defense_audio.Play(DefenseCue.Purchase);
            View.ShowMessage("+" + restored + " BEACON");
            Refresh();
        }

        private bool CanShop() => Model.CanShop(defense_game.Model);

        private bool Spend(int amount)
        {
            if (!Model.TrySpend(amount))
            {
                View.ShowMessage("Not enough gold.");
                return false;
            }

            GameSessionTracker.Instance.GoldSpent(amount, defense_game.transform_player.position);
            return true;
        }

        public void CloseForEnd()
        {
            Model.Close();
            View.ShowShop(false);
        }

        public void Refresh() => View.Render(
            Model,
            defense_game.Model,
            heal_price,
            heal_amount,
            repair_price,
            repair_amount,
            defense_game.btn_next_wave);
    }
}
