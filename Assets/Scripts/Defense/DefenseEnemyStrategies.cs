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
            combat.ApplyEnemyDamage(enemy.targetingPlayer, (enemy.targetingPlayer ? 10 : 5) + enemy.level * 2);
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
        public readonly IEnemyTargetStrategy Target;
        public readonly IEnemyAttackStrategy Attack;
        public readonly float MoveSpeed;
        public readonly int BonusHealth;
        public readonly Color MarkerColor;
        public readonly Vector3 MarkerScale;
        public readonly float MarkerAngle;
        public DefenseEnemyBehavior(IEnemyTargetStrategy target, IEnemyAttackStrategy attack, float speed, int health, Color color, Vector3 scale, float angle)
        { Target = target; Attack = attack; MoveSpeed = speed; BonusHealth = health; MarkerColor = color; MarkerScale = scale; MarkerAngle = angle; }
    }

    /// <summary>Flyweight factory composes independent targeting and attack strategies without per-frame allocations.</summary>
    public static class DefenseEnemyFactory
    {
        private static readonly IEnemyAttackStrategy Melee = new MeleeAttackStrategy();
        private static readonly DefenseEnemyBehavior Hunter = new DefenseEnemyBehavior(new NearbyPlayerTarget(), Melee, 1.4f, 1, new Color(0.5f, 0.9f, 1), new Vector3(4, 1, 1), 0);
        private static readonly DefenseEnemyBehavior Siege = new DefenseEnemyBehavior(new BeaconTarget(), Melee, 1.05f, 3, new Color(1, 0.6f, 0.1f), new Vector3(3, 3, 1), 45);
        private static readonly DefenseEnemyBehavior Shooter = new DefenseEnemyBehavior(new PlayerTarget(), new ProjectileAttackStrategy(), 1.1f, 2, new Color(0.95f, 0.35f, 1), new Vector3(1.5f, 4.5f, 1), 0);
        public static DefenseEnemyBehavior For(DefenseEnemyRole role) => role == DefenseEnemyRole.Siege ? Siege : role == DefenseEnemyRole.Shooter ? Shooter : Hunter;
    }
}
