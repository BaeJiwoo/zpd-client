using System;

namespace Zpd.Networking.DTO
{
    internal static class ApiErrorDtos
    {
        [Serializable]
        public sealed class ErrorEnvelope
        {
            public ErrorData error;
        }

        [Serializable]
        public sealed class ErrorData
        {
            public string code;
        }
    }
}
