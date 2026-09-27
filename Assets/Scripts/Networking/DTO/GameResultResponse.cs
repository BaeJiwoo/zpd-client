using System;

namespace Zpd.Networking.DTO
{
    [Serializable]
    public sealed class GameResultResponse
    {
        public string status;
        public string runId;
        public string resultId;
    }
}
