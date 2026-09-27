using UnityEngine;

namespace Zpd.Defense
{
    public interface IDefenseEnemyCombat
    {
        void ApplyEnemyDamage(bool playerTarget, int damage);

        bool TryFireEnemyProjectile(DefenseGame.EnemySlot enemy);
    }

    public interface IEnemyTargetStrategy
    {
        bool TargetsPlayer(Vector2 enemy, Vector2 player);
    }

    public interface IEnemyAttackStrategy
    {
        float Range { get; }

        float WindupSeconds { get; }

        float CooldownSeconds { get; }

        bool IsRanged { get; }

        bool Execute(IDefenseEnemyCombat combat, DefenseGame.EnemySlot enemy);
    }

    public sealed class NearbyPlayerTarget : IEnemyTargetStrategy
    {
        public bool TargetsPlayer(Vector2 enemy, Vector2 player) => Vector2.Distance(enemy, player) < 3.2f;
    }

    public sealed class BeaconTarget : IEnemyTargetStrategy
    {
        public bool TargetsPlayer(Vector2 enemy, Vector2 player) => false;
    }

    public sealed class PlayerTarget : IEnemyTargetStrategy
    {
        public bool TargetsPlayer(Vector2 enemy, Vector2 player) => true;
    }

    public sealed class MeleeAttackStrategy : IEnemyAttackStrategy
    {
        public float Range => 0.85f;
        public float WindupSeconds => 0.6f;
        public float CooldownSeconds => 0.85f;
        public bool IsRanged => false;

        public bool Execute(IDefenseEnemyCombat combat, DefenseGame.EnemySlot enemy)
        {
            combat.ApplyEnemyDamage(enemy.is_targeting_player, (enemy.is_targeting_player ? 10 : 5) + enemy.threat_level * 2);
            return true;
        }
    }

    public sealed class ProjectileAttackStrategy : IEnemyAttackStrategy
    {
        public float Range => 6.2f;
        public float WindupSeconds => 0.9f;
        public float CooldownSeconds => 1.7f;
        public bool IsRanged => true;

        public bool Execute(IDefenseEnemyCombat combat, DefenseGame.EnemySlot enemy) => combat.TryFireEnemyProjectile(enemy);
    }

    public sealed class DefenseEnemyBehavior
    {
        public readonly IEnemyTargetStrategy target_strategy;
        public readonly IEnemyAttackStrategy attack_strategy;
        public readonly float move_speed;
        public readonly int bonus_health;
        public readonly Color marker_color;
        public readonly Vector3 marker_scale;
        public readonly float marker_angle_degrees;

        public DefenseEnemyBehavior(
            IEnemyTargetStrategy target,
            IEnemyAttackStrategy attack,
            float speed,
            int health,
            Color color,
            Vector3 scale,
            float angle)
        {
            target_strategy = target;
            attack_strategy = attack;
            move_speed = speed;
            bonus_health = health;
            marker_color = color;
            marker_scale = scale;
            marker_angle_degrees = angle;
        }
    }

    /// <summary>Flyweight factory composes independent targeting and attack strategies without per-frame allocations.</summary>
    public static class DefenseEnemyFactory
    {
        private static readonly IEnemyAttackStrategy attack_melee = new MeleeAttackStrategy();
        private static readonly DefenseEnemyBehavior behavior_hunter = new DefenseEnemyBehavior(
            new NearbyPlayerTarget(),
            attack_melee,
            1.4f,
            1,
            new Color(0.5f, 0.9f, 1),
            new Vector3(4, 1, 1),
            0);
        private static readonly DefenseEnemyBehavior behavior_siege = new DefenseEnemyBehavior(
            new BeaconTarget(),
            attack_melee,
            1.05f,
            3,
            new Color(1, 0.6f, 0.1f),
            new Vector3(3, 3, 1),
            45);
        private static readonly DefenseEnemyBehavior behavior_shooter = new DefenseEnemyBehavior(
            new PlayerTarget(),
            new ProjectileAttackStrategy(),
            1.1f,
            2,
            new Color(0.95f, 0.35f, 1),
            new Vector3(1.5f, 4.5f, 1),
            0);

        public static DefenseEnemyBehavior For(DefenseEnemyRole role) => role == DefenseEnemyRole.Siege
            ? behavior_siege
            : role == DefenseEnemyRole.Shooter ? behavior_shooter : behavior_hunter;
    }
}
