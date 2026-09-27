using System;

namespace Zpd.Gameplay
{
    [Serializable]
    public sealed class GameRunSnapshot
    {
        public int schemaVersion = 1;
        public string runId;
        public string ownerPlayerId;
        public string accountApiRoot;
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
}
