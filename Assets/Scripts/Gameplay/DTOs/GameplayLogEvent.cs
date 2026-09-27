using System;

namespace Zpd.Gameplay
{
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
}
