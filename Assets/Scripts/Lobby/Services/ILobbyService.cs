using System.Threading;
using System.Threading.Tasks;

namespace Zpd.Lobby
{
    /// <summary>Inject one authenticated account's adapter. All successful results are server-authoritative.</summary>
    public interface ILobbyService
    {
        Task<LobbyProfileData> GetProfileAsync(CancellationToken cancellation);

        Task<LobbyInventoryData> GetInventoryAsync(CancellationToken cancellation);

        Task<LobbyUseItemResult> UseItemAsync(string itemId, string operationId, CancellationToken cancellation);
    }
}
