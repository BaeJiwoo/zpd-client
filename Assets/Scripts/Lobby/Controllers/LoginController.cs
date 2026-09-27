using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using Zpd.Gameplay;
using Zpd.Networking;

namespace Zpd.Lobby
{
    [RequireComponent(typeof(LoginView))]
    public sealed class LoginController : MonoBehaviour
    {
        [Header("Login API")]
        public string apiRoot = ApiClient.DefaultRoot;

        [Min(1)]
        public int timeoutSeconds = 15;

        public LoginView View => GetComponent<LoginView>();
        public bool IsBusy { get; private set; }

        private CancellationTokenSource _lifetime;

        private void OnEnable()
        {
            _lifetime = new CancellationTokenSource();
            IsBusy = false;

            if (View != null && View.TryBindControls())
            {
                View.Render(false, "Enter your ID and password.");
            }
        }

        private void OnDisable()
        {
            _lifetime?.Cancel();
            _lifetime?.Dispose();
            _lifetime = null;
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
                if (View.loginId.isFocused)
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

            if (!AuthValidation.TryNormalizeLoginId(View.loginId.text, out string loginId))
            {
                View.Render(false, ApiErrorMessages.Get(ApiErrorCode.InvalidLoginId));
                View.Focus();
                return;
            }

            string password = View.password.text;

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
            CancellationToken cancellation = _lifetime.Token;
            View.Render(true, "Signing in...");
            View.ClearPassword();

            try
            {
                var result = await AuthManager.Instance.LoginAsync(
                    apiRoot, loginId, password, cancellation, timeoutSeconds);

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

            if (View != null && View.login != null)
            {
                View.login.interactable = false;
            }

            if (View != null && View.status != null)
            {
                View.status.text = "The login screen is incomplete. Please reopen it.";
            }

            Debug.LogError("Login screen bindings are missing. Assign ID, Password, Login and Status in LoginView.", this);
            enabled = false;
            return false;
        }
    }
}
