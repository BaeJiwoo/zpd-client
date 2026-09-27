using System;

namespace Zpd.Defense
{
    [Serializable]
    public sealed class DefenseRunReport
    {
        public string runId, ownerPlayerId, accountApiRoot;
        public string mode = "solo_defense";
        public string reason;
        public int claimedKills, reachedWave;
        public float survivalSeconds;
    }
}
