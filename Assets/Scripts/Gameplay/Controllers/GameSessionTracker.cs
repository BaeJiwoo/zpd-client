using Zpd.Networking;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Zpd.Gameplay
{
    /// <summary>Scene-authored singleton. Tracks both modes; client observations are never reward authority.</summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class GameSessionTracker : MonoBehaviour
    {
        public static GameSessionTracker Instance { get; private set; }
        public bool IsRunning { get; private set; }
        public string CompletedJson { get; private set; }
        public string CurrentRunId => current_run?.runId;
        public string LastPersistenceError { get; private set; }
        public static string PendingDirectory => Path.Combine(Application.persistentDataPath, "pending-game-results");

        private GameRunSnapshot current_run;
        private readonly List<GameplayLogEvent> log_events = new List<GameplayLogEvent>();
        private int last_event_sequence;
        private float next_position_sample_at_seconds;
        private const int MaxEvents = 2048;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void OnApplicationQuit()
        {
            if (Instance == this && IsRunning)
            {
                Finish("application_quit", current_run.playerHealth, current_run.beaconHealth);
            }
        }

        public string Begin(GameMode mode, string battleId = null, string participationId = null)
        {
            if (IsRunning)
            {
                throw new InvalidOperationException("Finish the previous game session first.");
            }

            if (mode == GameMode.DedicatedBattle && (string.IsNullOrWhiteSpace(battleId) || string.IsNullOrWhiteSpace(participationId)))
            {
                throw new ArgumentException("Dedicated battles require server-issued battle and participation IDs.");
            }

            log_events.Clear();
            last_event_sequence = 0;
            next_position_sample_at_seconds = 0;
            CompletedJson = null;
            LastPersistenceError = null;
            current_run = new GameRunSnapshot
            {
                runId = Guid.NewGuid().ToString("N"),
                gameMode = GameModeNames.ToApiValue(mode),
                ownerPlayerId = Zpd.Networking.AuthManager.Instance.PlayerId,
                accountApiRoot = Zpd.Networking.AuthManager.Instance.Current?.ApiRoot,
                battleId = battleId,
                participationId = participationId,
                startedAt = DateTime.UtcNow.ToString("O"),
                clientVersion = Application.version,
                playerHealth = 100,
                beaconHealth = mode == GameMode.SoloDefense ? 100 : 0
            };
            IsRunning = true;
            Record("run_started", 0, Vector2.zero);
            return current_run.runId;
        }

        public void Advance(float dt, Vector2 position, int playerHealth, int beaconHealth)
        {
            if (!IsRunning)
            {
                return;
            }

            current_run.playedSeconds += Mathf.Max(0, dt);
            current_run.playerHealth = playerHealth;
            current_run.beaconHealth = beaconHealth;

            if (current_run.playedSeconds >= next_position_sample_at_seconds)
            {
                Record("position", 0, position);
                next_position_sample_at_seconds = current_run.playedSeconds + 1;
            }
        }

        public void WaveStarted(int wave)
        {
            if (!IsRunning)
            {
                return;
            }

            current_run.reachedWave = wave;
            Record("wave_started", wave, Vector2.zero);
        }

        public void Shot(Vector2 pos)
        {
            if (!IsRunning)
            {
                return;
            }

            current_run.shotsFired++;
            Record("shot", 1, pos);
        }

        public void Hit(Vector2 pos)
        {
            if (!IsRunning)
            {
                return;
            }

            current_run.hits++;
            Record("hit", 1, pos);
        }

        public void Kill(Vector2 pos)
        {
            if (!IsRunning)
            {
                return;
            }

            current_run.kills++;
            Record("kill", 1, pos);
        }

        public void GoldCollected(int amount, Vector2 pos)
        {
            if (!IsRunning)
            {
                return;
            }

            current_run.goldCollected += amount;
            Record("gold_collected", amount, pos);
        }

        public void GoldSpent(int amount, Vector2 pos)
        {
            if (!IsRunning)
            {
                return;
            }

            current_run.goldSpent += amount;
            Record("gold_spent", amount, pos);
        }

        public void SetWeapon(string weapon)
        {
            if (IsRunning)
            {
                current_run.equippedWeapon = weapon;
            }
        }

        public void Damage(bool beacon, int amount, Vector2 pos)
        {
            if (!IsRunning)
            {
                return;
            }

            if (beacon)
            {
                current_run.beaconDamage += amount;
            }
            else
            {
                current_run.damageTaken += amount;
            }

            Record(beacon ? "beacon_damage" : "player_damage", amount, pos);
        }

        public void Record(string type, int value, Vector2 pos)
        {
            if (!IsRunning)
            {
                return;
            }

            last_event_sequence++;

            // Keep full summary counters even when a long run exhausts the detailed-event budget.

            if (log_events.Count >= MaxEvents && type != "run_ended")
            {
                current_run.droppedLogEvents++;
                return;
            }

            log_events.Add(
                new GameplayLogEvent { sequence = last_event_sequence, elapsedSeconds = current_run.playedSeconds, type = type, value = value, x = pos.x, y = pos.y });
        }

        public GameRunSnapshot Finish(string reason, int playerHealth, int beaconHealth)
        {
            if (current_run == null)
            {
                throw new InvalidOperationException("No game session exists.");
            }

            if (IsRunning)
            {
                Record("run_ended", 0, Vector2.zero);
                current_run.endReason = reason;
                current_run.endedAt = DateTime.UtcNow.ToString("O");
                current_run.playerHealth = playerHealth;
                current_run.beaconHealth = beaconHealth;
                current_run.events = log_events.ToArray();
                IsRunning = false;
                CompletedJson = JsonUtility.ToJson(current_run);

                try
                {
                    Directory.CreateDirectory(PendingDirectory);
                    string path = Path.Combine(PendingDirectory, current_run.runId + ".json");
                    File.WriteAllText(path + ".tmp", CompletedJson);
                    File.Move(path + ".tmp", path);
                }
                catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
                {
                    LastPersistenceError = error.Message;
                    Debug.LogWarning("[Game Result] Local backup failed: " + error.Message);
                }
            }

            // Return a copy so callers cannot mutate the frozen retry payload.
            return JsonUtility.FromJson<GameRunSnapshot>(CompletedJson);
        }

        public static void MarkStored(string runId)
        {
            if (!Guid.TryParseExact(runId, "N", out _))
            {
                return;
            }

            string path = Path.Combine(PendingDirectory, runId + ".json");

            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                Debug.LogWarning("[Game Result] Could not remove acknowledged backup: " + error.Message);
            }
        }
    }
}
