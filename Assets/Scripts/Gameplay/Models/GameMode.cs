namespace Zpd.Gameplay
{
    public enum GameMode
    {
        DedicatedBattle,
        SoloDefense
    }

    public static class GameModeNames
    {
        public static string ToApiValue(GameMode mode) => mode == GameMode.DedicatedBattle
            ? "dedicated_battle"
            : "solo_defense";
    }
}
