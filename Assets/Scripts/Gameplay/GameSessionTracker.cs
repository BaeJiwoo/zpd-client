using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Zpd.Gameplay
{
    public enum GameMode { DedicatedBattle, SoloDefense }

    public static class GameModeNames
    {
        public static string ToApiValue(GameMode mode) => mode == GameMode.DedicatedBattle ? "dedicated_battle" : "solo_defense";
    }

    [Serializable]
    public sealed class GameplayLogEvent
    {
        public int sequence;
        public float elapsedSeconds;
        public string type;
        public int value;
        public float x;
        public float y;
    }

    [Serializable]
    public sealed class GameRunSnapshot
    {
        public int schemaVersion = 1;
        public string runId;
        public string gameMode;
        public string source = "client_unverified";
        public string battleId;
        public string participationId;
        public string startedAt;
        public string endedAt;
        public string clientVersion;
        public string endReason;
        public float playedSeconds;
        public int kills;
        public int reachedWave;
        public int shotsFired;
        public int hits;
        public int damageTaken;
        public int beaconDamage;
        public int playerHealth;
        public int beaconHealth;
        public int goldCollected;
        public int goldSpent;
        public string equippedWeapon;
        public int droppedLogEvents;
        public GameplayLogEvent[] events;
    }

    /// <summary>Scene-authored singleton. Tracks both modes; client observations are never reward authority.</summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class GameSessionTracker : MonoBehaviour
    {
        public static GameSessionTracker Instance { get; private set; }
        public bool IsRunning { get; private set; }
        public string CompletedJson { get; private set; }
        public string CurrentRunId => run?.runId;
        public string LastPersistenceError { get; private set; }
        public static string PendingDirectory => Path.Combine(Application.persistentDataPath, "pending-game-results");
        private GameRunSnapshot run;
        private readonly List<GameplayLogEvent> log = new List<GameplayLogEvent>();
        private int sequence;
        private float nextPositionSample;
        private const int MaxEvents = 2048;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        private void OnDestroy() { if (Instance == this) Instance = null; }
        private void OnApplicationQuit()
        {
            if (Instance == this && IsRunning) Finish("application_quit", run.playerHealth, run.beaconHealth);
        }

        public string Begin(GameMode mode, string battleId = null, string participationId = null)
        {
            if (IsRunning) throw new InvalidOperationException("Finish the previous game session first.");
            if (mode == GameMode.DedicatedBattle && (string.IsNullOrWhiteSpace(battleId) || string.IsNullOrWhiteSpace(participationId)))
                throw new ArgumentException("Dedicated battles require server-issued battle and participation IDs.");
            log.Clear();
            sequence = 0;
            nextPositionSample = 0;
            CompletedJson = null;
            LastPersistenceError = null;
            run = new GameRunSnapshot { runId = Guid.NewGuid().ToString("N"), gameMode = GameModeNames.ToApiValue(mode),
                battleId = battleId, participationId = participationId, startedAt = DateTime.UtcNow.ToString("O"),
                clientVersion = Application.version, playerHealth = 100, beaconHealth = mode == GameMode.SoloDefense ? 100 : 0 };
            IsRunning = true;
            Record("run_started", 0, Vector2.zero);
            return run.runId;
        }

        public void Advance(float dt, Vector2 position, int playerHealth, int beaconHealth)
        {
            if (!IsRunning) return;
            run.playedSeconds += Mathf.Max(0, dt);
            run.playerHealth = playerHealth;
            run.beaconHealth = beaconHealth;
            if (run.playedSeconds >= nextPositionSample)
            {
                Record("position", 0, position);
                nextPositionSample = run.playedSeconds + 1;
            }
        }
        public void WaveStarted(int wave) { if (!IsRunning) return; run.reachedWave = wave; Record("wave_started", wave, Vector2.zero); }
        public void Shot(Vector2 pos) { if (!IsRunning) return; run.shotsFired++; Record("shot", 1, pos); }
        public void Hit(Vector2 pos) { if (!IsRunning) return; run.hits++; Record("hit", 1, pos); }
        public void Kill(Vector2 pos) { if (!IsRunning) return; run.kills++; Record("kill", 1, pos); }
        public void GoldCollected(int amount, Vector2 pos) { if (!IsRunning) return; run.goldCollected += amount; Record("gold_collected", amount, pos); }
        public void GoldSpent(int amount, Vector2 pos) { if (!IsRunning) return; run.goldSpent += amount; Record("gold_spent", amount, pos); }
        public void SetWeapon(string weapon) { if (IsRunning) run.equippedWeapon = weapon; }
        public void Damage(bool beacon, int amount, Vector2 pos)
        {
            if (!IsRunning) return;
            if (beacon) run.beaconDamage += amount; else run.damageTaken += amount;
            Record(beacon ? "beacon_damage" : "player_damage", amount, pos);
        }
        public void Record(string type, int value, Vector2 pos)
        {
            if (!IsRunning) return;
            sequence++;
            // Keep full summary counters even when a long run exhausts the detailed-event budget.
            if (log.Count >= MaxEvents && type != "run_ended") { run.droppedLogEvents++; return; }
            log.Add(new GameplayLogEvent { sequence = sequence, elapsedSeconds = run.playedSeconds,
                type = type, value = value, x = pos.x, y = pos.y });
        }

        public GameRunSnapshot Finish(string reason, int playerHealth, int beaconHealth)
        {
            if (run == null) throw new InvalidOperationException("No game session exists.");
            if (IsRunning)
            {
                Record("run_ended", 0, Vector2.zero);
                run.endReason = reason;
                run.endedAt = DateTime.UtcNow.ToString("O");
                run.playerHealth = playerHealth;
                run.beaconHealth = beaconHealth;
                run.events = log.ToArray();
                IsRunning = false;
                CompletedJson = JsonUtility.ToJson(run);
                try
                {
                    Directory.CreateDirectory(PendingDirectory);
                    string path = Path.Combine(PendingDirectory, run.runId + ".json");
                    File.WriteAllText(path + ".tmp", CompletedJson);
                    File.Move(path + ".tmp", path);
                }
                catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
                { LastPersistenceError = error.Message; Debug.LogWarning("[Game Result] Local backup failed: " + error.Message); }
            }
            // Return a copy so callers cannot mutate the frozen retry payload.
            return JsonUtility.FromJson<GameRunSnapshot>(CompletedJson);
        }

        public static void MarkStored(string runId)
        {
            if (!Guid.TryParseExact(runId, "N", out _)) return;
            string path = Path.Combine(PendingDirectory, runId + ".json");
            try { if (File.Exists(path)) File.Delete(path); }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            { Debug.LogWarning("[Game Result] Could not remove acknowledged backup: " + error.Message); }
        }
    }
}
