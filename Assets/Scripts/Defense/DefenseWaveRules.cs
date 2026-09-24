using System;
using UnityEngine;

namespace Zpd.Defense
{
    public enum DefensePhase { Warning, Combat, Preparation }
    public enum DefenseEnemyRole { Hunter, Siege, Shooter }

    [Serializable]
    public sealed class DefenseWaveRules
    {
        [Min(1)] public int firstWaveCount = 9;
        [Min(0)] public int enemiesPerWave = 3;
        [Range(1, 40)] public int maxEnemies = 40;
        [Min(1)] public float preparationSeconds = 8;
        [Min(0.5f)] public float warningSeconds = 1.5f;
        [Min(0.2f)] public float spawnInterval = 0.85f;
        [Min(2)] public int siegeFromWave = 2;
        [Min(2)] public int twoEntrancesFromWave = 3;
        [Min(2)] public int siegeEvery = 3;
        [Min(2)] public int shooterFromWave = 2;
        [Min(2)] public int shooterEvery = 4;

        public int Count(int wave) => Mathf.Clamp(firstWaveCount + (Mathf.Max(1, wave) - 1) * enemiesPerWave, 1, maxEnemies);
        public int Threat(int wave) => 1 + (Mathf.Max(1, wave) - 1) / 2;
        public DefenseEnemyRole Role(int wave, int spawned) => wave >= shooterFromWave && spawned % Mathf.Max(2, shooterEvery) == 1
            ? DefenseEnemyRole.Shooter : wave >= siegeFromWave && spawned % Mathf.Max(2, siegeEvery) == 0 ? DefenseEnemyRole.Siege : DefenseEnemyRole.Hunter;
        public float Interval(int wave, float travelDistance) => Mathf.Max(0.3f, spawnInterval - (wave - 1) * 0.035f)
            * Mathf.Clamp(11.3f / Mathf.Max(1, travelDistance), 1, 2);
    }
}
