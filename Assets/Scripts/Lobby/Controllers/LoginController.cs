using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.InputSystem;
using Zpd.Gameplay;
using Zpd.Networking;

namespace Zpd.Lobby
{
    [RequireComponent(typeof(LoginView))]
    public sealed class LoginController : MonoBehaviour
    {
        [FormerlySerializedAs("apiRoot")]
        [Header("Login API")]
        public string api_root = ApiClient.DefaultRoot;

        [FormerlySerializedAs("timeoutSeconds")]
        [Min(1)]
        public int timeout_seconds = 15;

        public LoginView View => GetComponent<LoginView>();
        public bool IsBusy { get; private set; }

        private CancellationTokenSource cts_lifetime;

        private void OnEnable()
        {
            cts_lifetime = new CancellationTokenSource();
            IsBusy = false;

            if (View != null && View.TryBindControls())
            {
                View.Render(false, "Enter your ID and password.");
            }
        }

        private void OnDisable()
        {
            cts_lifetime?.Cancel();
            cts_lifetime?.Dispose();
            cts_lifetime = null;
            IsBusy = false;

            if (View != null)
            {
                View.ClearPassword();
            }
        }

        private void Start()
        {
            if (!EnsureViewReady())
            {
                return;
            }

            View.Render(false, "Enter your ID and password.");
            View.Focus();
        }

        private void Update()
        {
            if (!EnsureViewReady())
            {
                return;
            }

            var keyboard = Keyboard.current;

            if (keyboard != null && keyboard.tabKey.wasPressedThisFrame && !IsBusy)
            {
                if (View.input_login_id.isFocused)
                {
                    View.FocusPassword();
                }
                else
                {
                    View.Focus();
                }
            }

            if (keyboard != null &&
                (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame))
            {
                Login();
            }
        }

        public void Login()
        {
            _ = LoginAsync();
        }

        public async Task LoginAsync()
        {
            if (IsBusy || !isActiveAndEnabled || !EnsureViewReady())
            {
                return;
            }

            if (!AuthValidation.TryNormalizeLoginId(View.input_login_id.text, out string loginId))
            {
                View.Render(false, ApiErrorMessages.Get(ApiErrorCode.InvalidLoginId));
                View.Focus();
                return;
            }

            string password = View.input_password.text;

            if (!AuthValidation.IsValidPassword(password))
            {
                View.Render(false, ApiErrorMessages.Get(ApiErrorCode.InvalidPassword));
                View.FocusPassword();
                return;
            }

            if (!SceneNavigation.CanLoad(SceneNavigation.Lobby))
            {
                View.Render(false, "Unable to open the lobby. Check the scene configuration.");
                return;
            }

            IsBusy = true;
            CancellationToken cancellation = cts_lifetime.Token;
            View.Render(true, "Signing in...");
            View.ClearPassword();

            try
            {
                var result = await AuthManager.Instance.LoginAsync(
                    api_root, loginId, password, cancellation, timeout_seconds);

                if (this == null || !isActiveAndEnabled || cancellation.IsCancellationRequested)
                {
                    return;
                }

                if (!result.IsSuccess)
                {
                    View.Render(false, result.Error);
                    View.Focus();
                    return;
                }

                View.Render(true, "Opening the lobby...");

                if (!SceneNavigation.Load(SceneNavigation.Lobby))
                {
                    AuthManager.Instance.Invalidate(result.Response);
                    View.Render(false, "Unable to open the lobby. Please try again.");
                }
            }
            catch (Exception error)
            {
                if (this == null || !isActiveAndEnabled || cancellation.IsCancellationRequested)
                {
                    return;
                }

                string message = error is ArgumentException
                    ? "Check the login API URL configuration."
                    : "Unable to sign in. Please try again.";

                View.Render(false, message);
                View.Focus();
            }
            finally
            {
                if (this != null && !cancellation.IsCancellationRequested)
                {
                    IsBusy = false;
                }
            }
        }

        private bool EnsureViewReady()
        {
            if (View != null && (View.IsConfigured || View.TryBindControls()))
            {
                return true;
            }

            if (View != null && View.btn_login != null)
            {
                View.btn_login.interactable = false;
            }

            if (View != null && View.txt_status != null)
            {
                View.txt_status.text = "The login screen is incomplete. Please reopen it.";
            }

            Debug.LogError("Login screen bindings are missing. Assign ID, Password, Login and Status in LoginView.", this);
            enabled = false;
            return false;
        }
    }
}
