using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Zpd.Defense
{
    public enum DefensePhase
    {
        Warning,
        Combat,
        Preparation
    }

    public enum DefenseEnemyRole
    {
        Hunter,
        Siege,
        Shooter
    }

    [Serializable]
    public sealed class DefenseWaveRules
    {
        [FormerlySerializedAs("firstWaveCount")]
        [Min(1)]
        public int first_wave_enemy_count = 9;

        [FormerlySerializedAs("enemiesPerWave")]
        [Min(0)]
        public int additional_enemies_per_wave = 3;

        [FormerlySerializedAs("maxEnemies")]
        [Range(1, 40)]
        public int max_enemies_per_wave = 40;

        [FormerlySerializedAs("preparationSeconds")]
        [Min(1)]
        public float preparation_seconds = 8;

        [FormerlySerializedAs("warningSeconds")]
        [Min(0.5f)]
        public float warning_seconds = 1.5f;

        [FormerlySerializedAs("spawnInterval")]
        [Min(0.2f)]
        public float spawn_interval_seconds = 0.85f;

        [FormerlySerializedAs("siegeFromWave")]
        [Min(2)]
        public int siege_from_wave = 2;

        [FormerlySerializedAs("twoEntrancesFromWave")]
        [Min(2)]
        public int two_entrances_from_wave = 3;

        [FormerlySerializedAs("siegeEvery")]
        [Min(2)]
        public int siege_spawn_interval_count = 3;

        [FormerlySerializedAs("shooterFromWave")]
        [Min(2)]
        public int shooter_from_wave = 2;

        [FormerlySerializedAs("shooterEvery")]
        [Min(2)]
        public int shooter_spawn_interval_count = 4;

        public int Count(int wave) => Mathf.Clamp(
            first_wave_enemy_count + (Mathf.Max(1, wave) - 1) * additional_enemies_per_wave,
            1,
            max_enemies_per_wave);

        public int Threat(int wave) => 1 + (Mathf.Max(1, wave) - 1) / 2;

        public DefenseEnemyRole Role(int wave, int spawned) => wave >= shooter_from_wave && spawned % Mathf.Max(2, shooter_spawn_interval_count) == 1
            ? DefenseEnemyRole.Shooter
            : wave >= siege_from_wave && spawned % Mathf.Max(2, siege_spawn_interval_count) == 0
            ? DefenseEnemyRole.Siege
            : DefenseEnemyRole.Hunter;

        public float Interval(int wave, float travelDistance) => Mathf.Max(0.3f, spawn_interval_seconds - (wave - 1) * 0.035f) * Mathf.Clamp(11.3f / Mathf.Max(1, travelDistance), 1, 2);
    }
}
