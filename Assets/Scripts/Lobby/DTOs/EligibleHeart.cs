using System;

namespace Zpd.Lobby
{
    [Serializable]
    public sealed class EligibleHeart
    {
        public string userId;
        // Receipt ID for receiving; stable server eligibility-cycle ID for sending.

        public string operationId;
        public bool allowed;
    }
}
