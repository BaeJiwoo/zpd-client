using System;

namespace Zpd.Networking.DTO
{
    [Serializable]
    public sealed class DefenseRewardResponse
    {
        public string status;
        public string runId;
        public string settlementId;
        public int earnedExperience = -1;
    }
}
