using System;
using System.Threading;
using System.Threading.Tasks;
using Zpd.Networking;
using static Zpd.Networking.DTO.LobbyApiDtos;

namespace Zpd.Lobby
{
    /// <summary>Maps typed HTTP results to the lobby's existing service contract.</summary>
    public sealed class LobbyApiService : ILobbyService
    {
        private readonly ApiClient _api;

        public LobbyApiService(AccountSession authenticatedSession, int timeoutSeconds = 15)
        {
            _api = AuthManager.Instance.CreateClient(authenticatedSession, timeoutSeconds);
        }

        public async Task<LobbyProfileData> GetProfileAsync(CancellationToken cancellation)
        {
            var result = await _api.GetAsync<ProfileEnvelope>("/me", cancellation, authenticated: true);

            if (!result.IsSuccess)
            {
                throw LobbyServiceException.FromResult(result);
            }

            return LobbyResponseParser.ParseProfile(result.Response.data);
        }

        public async Task<LobbyInventoryData> GetInventoryAsync(CancellationToken cancellation)
        {
            var result = await _api.GetAsync<InventoryEnvelope>("/me/inventory", cancellation, authenticated: true);

            if (!result.IsSuccess)
            {
                throw LobbyServiceException.FromResult(result);
            }

            return LobbyResponseParser.ParseInventory(result.Response.data);
        }

        public async Task<LobbyUseItemResult> UseItemAsync(
            string itemId,
            string operationId,
            CancellationToken cancellation)
        {
            if (string.IsNullOrWhiteSpace(itemId) || string.IsNullOrWhiteSpace(operationId))
            {
                throw new ArgumentException("Item and operation IDs are required.");
            }

            string path = "/me/inventory/" + Uri.EscapeDataString(itemId) + "/use";
            var result = await _api.PostAsync<UseRequest, UseEnvelope>(
                path,
                new UseRequest { quantity = 1 },
                cancellation,
                authenticated: true,
                operationId: operationId);

            if (!result.IsSuccess)
            {
                throw LobbyServiceException.FromResult(result, ApiErrorCode.InvalidItemUseResponse);
            }

            return LobbyResponseParser.ParseItemUse(result.Response.data);
        }
    }
}
