using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Zpd.Networking;

namespace Zpd.Lobby
{
    [RequireComponent(typeof(LobbyView))]
    public sealed class LobbyController : MonoBehaviour
    {
        [SerializeField]
        private LobbyView view;

        [Header("Account API")]
        [SerializeField, Min(1)]
        private int apiTimeoutSeconds = 15;

        public LobbyView View => view != null ? view : (view = GetComponent<LobbyView>());
        public LobbyModel Model { get; private set; } = new LobbyModel();

        public LegacyLobbyController social;
        public event Action MultiPlayRequested;
        private ILobbyService service;
        private CancellationTokenSource lifetime;
        private int generation, profileRequest, inventoryRequest;
        private bool initialized;
        private bool serviceConfigured;

        private void Awake()
        {
            View.Initialize();
            initialized = true;
            RenderAll();
        }

        private void Start()
        {
            if (!serviceConfigured)
            {
                RestoreSession();
            }
        }

        private void RestoreSession()
        {
            ConfigureService(
                AuthManager.Instance.IsSignedIn
                ? new LobbyApiService(AuthManager.Instance.Current, apiTimeoutSeconds)
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
            lifetime?.Dispose();
            lifetime = new CancellationTokenSource();
            View.ItemSelected += InspectItem;
            AuthManager.Instance.Changed += RestoreSession;

            if (initialized && (!serviceConfigured || service is LobbyApiService))
            {
                RestoreSession();
            }
            else if (initialized && service != null)
            {
                _ = RefreshAsync();
            }
        }

        private void OnDisable()
        {
            generation++;
            lifetime?.Cancel();
            lifetime?.Dispose();
            lifetime = null;
            AuthManager.Instance.Changed -= RestoreSession;
            View.ItemSelected -= InspectItem;
            Model = new LobbyModel();

            if (initialized)
            {
                View.CloseSection();
                RenderAll();
            }
        }

        private void Update()
        {
            AuthManager.Instance.CheckExpiry();
            View.SyncInteraction(social != null && social.backdrop.activeSelf);

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                CloseTopmost();
            }
        }

        /// <summary>Call on Unity's main thread after authentication, and with null on logout.</summary>
        public void ConfigureService(ILobbyService authenticatedService)
        {
            serviceConfigured = true;
            generation++;
            lifetime?.Cancel();
            lifetime?.Dispose();
            lifetime = new CancellationTokenSource();
            service = authenticatedService;
            Model = new LobbyModel();
            View.CloseSection();
            View.ShowStatus(service == null ? "Account service not connected." : "Select a menu to view account data.");

            if (social != null)
            {
                social.ClosePanel();
                social.heartAutomation.ResetForAccount();
                social.characterPicker.ResetServerState();

                foreach (var row in social.friends.GetComponentsInChildren<LobbySocialSlot>(true))
                {
                    row.Clear();
                }
            }

            RenderAll();

            if (isActiveAndEnabled && service != null)
            {
                _ = RefreshAsync();
            }
        }

        public Task RefreshAsync() => Task.WhenAll(RefreshProfileAsync(), RefreshInventoryAsync());

        public async Task RefreshProfileAsync()
        {
            if (service == null || !isActiveAndEnabled)
            {
                return;
            }

            var api = service;
            int account = generation, request = ++profileRequest;
            var token = lifetime.Token;
            Model.BeginProfile();
            View.RenderProfile(Model);

            try
            {
                var response = await api.GetProfileAsync(token);

                if (!Current(account, token) || request != profileRequest)
                {
                    return;
                }

                Model.ApplyProfile(response);
                RenderProfile();
            }
            catch (Exception error)
            {
                if (!Current(account, token) || request != profileRequest)
                {
                    return;
                }

                Model.FailProfile(Message(error));
                View.RenderProfile(Model);
            }
        }

        public async Task RefreshInventoryAsync()
        {
            if (service == null || !isActiveAndEnabled || Model.IsUsingItem)
            {
                return;
            }

            var api = service;
            int account = generation, request = ++inventoryRequest;
            var token = lifetime.Token;
            Model.BeginInventory();
            View.RenderInventory(Model);

            try
            {
                var response = await api.GetInventoryAsync(token);

                if (!Current(account, token) || request != inventoryRequest)
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
                if (!Current(account, token) || request != inventoryRequest)
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
            if (service == null || !isActiveAndEnabled || !Model.BeginUse())
            {
                return;
            }

            var api = service;
            int account = generation;
            var token = lifetime.Token;
            string id = Model.OperationItemId, operation = Model.OperationId;
            ++inventoryRequest; // Reads issued before a mutation cannot overwrite its result.
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

        private bool Current(int account, CancellationToken token) => this != null && isActiveAndEnabled && !token.IsCancellationRequested && account == generation;

        private static string Message(Exception error) => error is LobbyServiceException
            ? error.Message
            : error is OperationCanceledException
            ? "Request timed out. Please retry."
            : "Unable to load confirmed data. Please retry.";

        private void RenderProfile()
        {
            View.RenderProfile(Model);
            var p = Model.Profile;

            if (social != null)
            {
                social.characterPicker.ResetServerState();
            }

            if (social != null && p != null && !string.IsNullOrEmpty(p.CharacterId) && !string.IsNullOrEmpty(p.CharacterArtKey))
            {
                social.characterPicker.BindOwnership(p.CharacterArtKey, p.CharacterId, true);
                social.characterPicker.ApplyConfirmedCharacter(p.CharacterId);
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
                View.ShowItem(Model, service != null);
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
            social.OpenFriends();
            View.SyncInteraction(social.backdrop.activeSelf);
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

            social.EnterSoloDefense();
        }

        // Compatibility for existing editor builders and authored tests. UI serialization belongs to View.

        public LobbyItemCard cardTemplate { get => View.cardTemplate; set => View.cardTemplate = value; }
        public Text nickname { get => View.nickname; set => View.nickname = value; }
        public Text level { get => View.level; set => View.level = value; }
        public Text record { get => View.record; set => View.record = value; }
        public Text history { get => View.history; set => View.history = value; }
        public Text inventoryStatus { get => View.inventoryStatus; set => View.inventoryStatus = value; }
        public Text status { get => View.status; set => View.status = value; }
        public Text emptyState { get => View.emptyState; set => View.emptyState = value; }
        public Text modalTitle { get => View.modalTitle; set => View.modalTitle = value; }
        public Text modalDescription { get => View.modalDescription; set => View.modalDescription = value; }
        public Text modalQuantity { get => View.modalQuantity; set => View.modalQuantity = value; }
        public Button useButton { get => View.useButton; set => View.useButton = value; }
        public Button soloButton { get => View.soloButton; set => View.soloButton = value; }
        public CanvasGroup home { get => View.home; set => View.home = value; }
        public CanvasGroup sections { get => View.sections; set => View.sections = value; }
        public Slider quantitySlider { get => View.quantitySlider; set => View.quantitySlider = value; }
        public GameObject profilePanel { get => View.profilePanel; set => View.profilePanel = value; }
        public GameObject inventoryPanel { get => View.inventoryPanel; set => View.inventoryPanel = value; }
        public GameObject modal { get => View.modal; set => View.modal = value; }
        public GameObject quantityRoot { get => View.quantityRoot; set => View.quantityRoot = value; }
        public Transform content { get => View.content; set => View.content = value; }
        public Button[] filters { get => View.filters; set => View.filters = value; }
    }
}
