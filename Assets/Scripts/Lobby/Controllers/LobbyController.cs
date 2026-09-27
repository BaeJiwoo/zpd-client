using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Zpd.Networking;

namespace Zpd.Lobby
{
    [RequireComponent(typeof(LobbyView))]
    public sealed class LobbyController : MonoBehaviour
    {
        [FormerlySerializedAs("view")]
        [SerializeField]
        private LobbyView lobby_view;

        [FormerlySerializedAs("apiTimeoutSeconds")]
        [Header("Account API")]
        [SerializeField, Min(1)]
        private int api_timeout_seconds = 15;

        public LobbyView View => lobby_view != null ? lobby_view : (lobby_view = GetComponent<LobbyView>());
        public LobbyModel Model { get; private set; } = new LobbyModel();

        [FormerlySerializedAs("social")]
        public LegacyLobbyController legacy_lobby_controller;
        public event Action MultiPlayRequested;
        private ILobbyService lobby_service;
        private CancellationTokenSource cts_lifetime;
        private int account_generation;

        private int profile_request_version;

        private int inventory_request_version;
        private bool is_initialized;
        private bool is_service_configured;

        private void Awake()
        {
            View.Initialize();
            is_initialized = true;
            RenderAll();
        }

        private void Start()
        {
            if (!is_service_configured)
            {
                RestoreSession();
            }
        }

        private void RestoreSession()
        {
            ConfigureService(
                AuthManager.Instance.IsSignedIn
                ? new LobbyApiService(AuthManager.Instance.Current, api_timeout_seconds)
                : null);

            if (!AuthManager.Instance.IsSignedIn)
            {
                View.ShowStatus("Sign in to continue. Select Sign out to return to login.");
            }
        }

        public void ChangePlayer()
        {
            if (!Zpd.Gameplay.SceneNavigation.CanLoad(Zpd.Gameplay.SceneNavigation.Login))
            {
                View.ShowStatus("Unable to open the login screen.");
                return;
            }

            AuthManager.Instance.Logout();
            Zpd.Gameplay.SceneNavigation.Load(Zpd.Gameplay.SceneNavigation.Login);
        }

        /// <summary>Use a server-issued session after authentication, including in future production login adapters.</summary>
        public void ConnectApi(AccountSession session) => AuthManager.Instance.SetSession(session);

        private void OnEnable()
        {
            cts_lifetime?.Dispose();
            cts_lifetime = new CancellationTokenSource();
            View.ItemSelected += InspectItem;
            AuthManager.Instance.Changed += RestoreSession;

            if (is_initialized && (!is_service_configured || lobby_service is LobbyApiService))
            {
                RestoreSession();
            }
            else if (is_initialized && lobby_service != null)
            {
                _ = RefreshAsync();
            }
        }

        private void OnDisable()
        {
            account_generation++;
            cts_lifetime?.Cancel();
            cts_lifetime?.Dispose();
            cts_lifetime = null;
            AuthManager.Instance.Changed -= RestoreSession;
            View.ItemSelected -= InspectItem;
            Model = new LobbyModel();

            if (is_initialized)
            {
                View.CloseSection();
                RenderAll();
            }
        }

        private void Update()
        {
            AuthManager.Instance.CheckExpiry();
            View.SyncInteraction(legacy_lobby_controller != null && legacy_lobby_controller.game_object_backdrop.activeSelf);

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                CloseTopmost();
            }
        }

        /// <summary>Call on Unity's main thread after authentication, and with null on logout.</summary>
        public void ConfigureService(ILobbyService authenticatedService)
        {
            is_service_configured = true;
            account_generation++;
            cts_lifetime?.Cancel();
            cts_lifetime?.Dispose();
            cts_lifetime = new CancellationTokenSource();
            lobby_service = authenticatedService;
            Model = new LobbyModel();
            View.CloseSection();
            View.ShowStatus(lobby_service == null ? "Account service not connected." : "Select a menu to view account data.");

            if (legacy_lobby_controller != null)
            {
                legacy_lobby_controller.ClosePanel();
                legacy_lobby_controller.lobby_heart_automation.ResetForAccount();
                legacy_lobby_controller.lobby_character_picker.ResetServerState();

                foreach (var row in legacy_lobby_controller.lobby_panel_friends.GetComponentsInChildren<LobbySocialSlot>(true))
                {
                    row.Clear();
                }
            }

            RenderAll();

            if (isActiveAndEnabled && lobby_service != null)
            {
                _ = RefreshAsync();
            }
        }

        public Task RefreshAsync() => Task.WhenAll(RefreshProfileAsync(), RefreshInventoryAsync());

        public async Task RefreshProfileAsync()
        {
            if (lobby_service == null || !isActiveAndEnabled)
            {
                return;
            }

            var api = lobby_service;
            int account = account_generation, request = ++profile_request_version;
            var token = cts_lifetime.Token;
            Model.BeginProfile();
            View.RenderProfile(Model);

            try
            {
                var response = await api.GetProfileAsync(token);

                if (!Current(account, token) || request != profile_request_version)
                {
                    return;
                }

                Model.ApplyProfile(response);
                RenderProfile();
            }
            catch (Exception error)
            {
                if (!Current(account, token) || request != profile_request_version)
                {
                    return;
                }

                Model.FailProfile(Message(error));
                View.RenderProfile(Model);
            }
        }

        public async Task RefreshInventoryAsync()
        {
            if (lobby_service == null || !isActiveAndEnabled || Model.IsUsingItem)
            {
                return;
            }

            var api = lobby_service;
            int account = account_generation, request = ++inventory_request_version;
            var token = cts_lifetime.Token;
            Model.BeginInventory();
            View.RenderInventory(Model);

            try
            {
                var response = await api.GetInventoryAsync(token);

                if (!Current(account, token) || request != inventory_request_version)
                {
                    return;
                }

                Model.ApplyInventory(response);
                View.RenderInventory(Model);

                if (View.HasModal && Model.SelectedItem != null)
                {
                    View.ShowItem(Model, true);
                }
                else if (View.HasModal && View.HasSection)
                {
                    View.CloseModal();
                }
            }
            catch (Exception error)
            {
                if (!Current(account, token) || request != inventory_request_version)
                {
                    return;
                }

                Model.FailInventory(Message(error));
                View.RenderInventory(Model);
            }
        }

        public void UseItem()
        {
            _ = UseSelectedItemAsync();
        }

        public async Task UseSelectedItemAsync()
        {
            if (lobby_service == null || !isActiveAndEnabled || !Model.BeginUse())
            {
                return;
            }

            var api = lobby_service;
            int account = account_generation;
            var token = cts_lifetime.Token;
            string id = Model.OperationItemId, operation = Model.OperationId;
            ++inventory_request_version; // Reads issued before a mutation cannot overwrite its result.
            View.ShowItem(Model, true);

            try
            {
                var response = await api.UseItemAsync(id, operation, token);

                if (!Current(account, token))
                {
                    return;
                }

                if (response == null || response.inventory == null)
                {
                    throw new InvalidOperationException("Missing confirmed item result.");
                }

                if (response.profile != null)
                {
                    _ = new LobbyProfileSnapshot(response.profile);
                } // Validate before applying either resource.

                if (!Model.ApplyInventory(response.inventory))
                {
                    throw new InvalidOperationException("Outdated item result. Retry the same operation.");
                }

                if (response.profile != null)
                {
                    Model.ApplyProfile(response.profile);
                }

                Model.CompleteUse();
                RenderAll();
                View.ShowStatus("Item use confirmed.");

                // Never reopen a popup that the player closed while waiting.

                if (View.HasModal && View.HasSection)
                {
                    View.ShowItem(Model, true);
                }
            }
            catch (Exception error)
            {
                if (!Current(account, token))
                {
                    return;
                }

                Model.FailUse(Message(error), !(error is LobbyServiceException known) || known.OutcomeUnknown);
                View.ShowStatus(Model.UseError);

                if (View.HasModal && Model.SelectedItem != null)
                {
                    View.ShowItem(Model, true);
                }
            }
        }

        private bool Current(int account, CancellationToken token) => this != null && isActiveAndEnabled && !token.IsCancellationRequested && account == account_generation;

        private static string Message(Exception error) => error is LobbyServiceException
            ? error.Message
            : error is OperationCanceledException
            ? "Request timed out. Please retry."
            : "Unable to load confirmed data. Please retry.";

        private void RenderProfile()
        {
            View.RenderProfile(Model);
            var p = Model.Profile;

            if (legacy_lobby_controller != null)
            {
                legacy_lobby_controller.lobby_character_picker.ResetServerState();
            }

            if (legacy_lobby_controller != null && p != null && !string.IsNullOrEmpty(p.CharacterId) && !string.IsNullOrEmpty(p.CharacterArtKey))
            {
                legacy_lobby_controller.lobby_character_picker.BindOwnership(p.CharacterArtKey, p.CharacterId, true);
                legacy_lobby_controller.lobby_character_picker.ApplyConfirmedCharacter(p.CharacterId);
            }
        }

        private void RenderAll()
        {
            RenderProfile();
            View.RenderInventory(Model);
        }

        public void OpenProfile()
        {
            Model.Open(LobbyWindow.Profile);
            View.ShowSection(Model.Window);
            _ = RefreshProfileAsync();
        }

        public void OpenInventory()
        {
            Model.Open(LobbyWindow.Inventory);
            View.ShowSection(Model.Window);
            _ = RefreshInventoryAsync();
        }

        public void ShowAll() => Filter(0);

        public void ShowConsumables() => Filter(1);

        public void ShowEquipment() => Filter(2);

        private void Filter(int value)
        {
            Model.SelectFilter(value);
            View.RenderInventory(Model);
        }

        public void InspectItem(string id)
        {
            Model.SelectItem(id);

            if (Model.SelectedItem != null)
            {
                View.ShowItem(Model, lobby_service != null);
            }
        }

        public void CloseModal()
        {
            Model.CloseItem();
            View.CloseModal();
        }

        public void CloseSection()
        {
            Model.Open(LobbyWindow.Home);
            View.CloseSection();
        }

        public void CloseTopmost()
        {
            if (View.HasModal)
            {
                CloseModal();
            }
            else if (View.HasSection)
            {
                CloseSection();
            }
        }

        public void OpenFriends()
        {
            CloseSection();
            legacy_lobby_controller.OpenFriends();
            View.SyncInteraction(legacy_lobby_controller.game_object_backdrop.activeSelf);
        }

        public void OpenMultiPlay()
        {
            CloseSection();

            if (MultiPlayRequested != null)
            {
                MultiPlayRequested.Invoke();
            }
            else
            {
                View.ShowMultiPlay();
            }
        }

        public void EnterSoloDefense()
        {
            if (!Zpd.Gameplay.SceneNavigation.CanLoad(Zpd.Gameplay.SceneNavigation.SoloDefense))
            {
                View.ShowStatus("Solo Defense is unavailable. Add its scene to the build.");
                return;
            }

            legacy_lobby_controller.EnterSoloDefense();
        }

        // Compatibility for existing editor builders and authored tests. UI serialization belongs to View.

        public LobbyItemCard lobby_item_card_template { get => View.lobby_item_card_template; set => View.lobby_item_card_template = value; }
        public Text txt_nickname { get => View.txt_nickname; set => View.txt_nickname = value; }
        public Text txt_level { get => View.txt_level; set => View.txt_level = value; }
        public Text txt_record { get => View.txt_record; set => View.txt_record = value; }
        public Text txt_history { get => View.txt_history; set => View.txt_history = value; }
        public Text txt_inventory_status { get => View.txt_inventory_status; set => View.txt_inventory_status = value; }
        public Text txt_status { get => View.txt_status; set => View.txt_status = value; }
        public Text txt_empty_state { get => View.txt_empty_state; set => View.txt_empty_state = value; }
        public Text txt_modal_title { get => View.txt_modal_title; set => View.txt_modal_title = value; }
        public Text txt_modal_description { get => View.txt_modal_description; set => View.txt_modal_description = value; }
        public Text txt_modal_quantity { get => View.txt_modal_quantity; set => View.txt_modal_quantity = value; }
        public Button btn_use_item { get => View.btn_use_item; set => View.btn_use_item = value; }
        public Button btn_solo_defense { get => View.btn_solo_defense; set => View.btn_solo_defense = value; }
        public CanvasGroup canvas_group_home { get => View.canvas_group_home; set => View.canvas_group_home = value; }
        public CanvasGroup canvas_group_sections { get => View.canvas_group_sections; set => View.canvas_group_sections = value; }
        public Slider slider_quantity { get => View.slider_quantity; set => View.slider_quantity = value; }
        public GameObject game_object_profile_panel { get => View.game_object_profile_panel; set => View.game_object_profile_panel = value; }
        public GameObject game_object_inventory_panel { get => View.game_object_inventory_panel; set => View.game_object_inventory_panel = value; }
        public GameObject game_object_modal { get => View.game_object_modal; set => View.game_object_modal = value; }
        public GameObject game_object_quantity_root { get => View.game_object_quantity_root; set => View.game_object_quantity_root = value; }
        public Transform transform_inventory_content { get => View.transform_inventory_content; set => View.transform_inventory_content = value; }
        public Button[] btn_inventory_filters { get => View.btn_inventory_filters; set => View.btn_inventory_filters = value; }
    }
}
