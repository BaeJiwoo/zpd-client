using UnityEngine;

namespace Zpd.Defense
{
    /// <summary>Run-local stats. Bounds also guarantee a complete volley fits the authored projectile pool.</summary>
    public sealed class DefenseCombatStats
    {
        public int ProjectileLevel { get; private set; }
        public int DamageLevel { get; private set; }
        public int FireRateLevel { get; private set; }
        public int ProjectileCount => 1 + ProjectileLevel;
        public int Damage => 1 + DamageLevel;
        public float FireInterval => 0.24f / (1 + FireRateLevel * 0.15f);
        public void Reset() { ProjectileLevel = DamageLevel = FireRateLevel = 0; }
        internal void AddProjectile() { ProjectileLevel = Mathf.Min(6, ProjectileLevel + 1); }
        internal void AddDamage() { DamageLevel = Mathf.Min(49, DamageLevel + 1); }
        internal void AddFireRate() { FireRateLevel = Mathf.Min(13, FireRateLevel + 1); }
    }

    public interface IDefenseUpgradeCommand
    {
        string Id { get; }
        bool CanApply(DefenseCombatStats stats);
        string Describe(DefenseCombatStats stats);
        void Apply(DefenseCombatStats stats);
    }

    public sealed class ExtraProjectileUpgrade : IDefenseUpgradeCommand
    {
        public string Id => "projectiles";
        public bool CanApply(DefenseCombatStats stats) => stats.ProjectileLevel < 6;
        public string Describe(DefenseCombatStats stats) => "다중 발사\n\n발사체 +1\n" + stats.ProjectileCount + "발 → " + Mathf.Min(7, stats.ProjectileCount + 1) + "발";
        public void Apply(DefenseCombatStats stats) { if (CanApply(stats)) stats.AddProjectile(); }
    }

    public sealed class DamageUpgrade : IDefenseUpgradeCommand
    {
        public string Id => "damage";
        public bool CanApply(DefenseCombatStats stats) => stats.DamageLevel < 49;
        public string Describe(DefenseCombatStats stats) => "강화 탄환\n\n탄환 피해 +1\n" + stats.Damage + " → " + Mathf.Min(50, stats.Damage + 1);
        public void Apply(DefenseCombatStats stats) { if (CanApply(stats)) stats.AddDamage(); }
    }

    public sealed class FireRateUpgrade : IDefenseUpgradeCommand
    {
        public string Id => "fire_rate";
        public bool CanApply(DefenseCombatStats stats) => stats.FireRateLevel < 13;
        public string Describe(DefenseCombatStats stats) => "속사 훈련\n\n기본 발사 속도 +15%\n초당 " + (1 / stats.FireInterval).ToString("0.0") + " → " + ((1 + Mathf.Min(13, stats.FireRateLevel + 1) * 0.15f) / 0.24f).ToString("0.0") + "회";
        public void Apply(DefenseCombatStats stats) { if (CanApply(stats)) stats.AddFireRate(); }
    }

    /// <summary>Immutable command catalog; state belongs exclusively to each run's stats.</summary>
    public static class DefenseUpgradeCatalog
    {
        public static readonly IDefenseUpgradeCommand Projectiles = new ExtraProjectileUpgrade();
        public static readonly IDefenseUpgradeCommand Damage = new DamageUpgrade();
        public static readonly IDefenseUpgradeCommand FireRate = new FireRateUpgrade();
        public static IDefenseUpgradeCommand At(int index) => index == 0 ? Projectiles : index == 1 ? Damage : index == 2 ? FireRate : null;
    }
}
