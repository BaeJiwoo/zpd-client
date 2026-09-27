namespace Zpd.Lobby
{
    public enum LobbySection
    {
        None,
        Profile,
        Friends,
        Inventory,
        Characters
    }

    /// <summary>Local navigation state. Server-owned profile and inventory are not fabricated.</summary>
    public sealed class LegacyLobbyModel
    {
        public LobbySection Section { get; private set; }
        public int SocialPage { get; private set; }
        public string SearchQuery { get; private set; } = "";
        public bool IsNavigating { get; internal set; }

        public void Toggle(LobbySection section) => Section = Section == section ? LobbySection.None : section;

        public void Close() => Section = LobbySection.None;

        public void SelectSocialPage(int index)
        {
            if (index >= 0 && index < 4)
            {
                SocialPage = index;
            }
        }

        public void SetSearch(string query) => SearchQuery = (query ?? "").Trim().Replace("\n", " ").Replace("\r", " ");
    }
}
