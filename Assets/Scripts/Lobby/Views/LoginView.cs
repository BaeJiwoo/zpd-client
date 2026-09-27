using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;

namespace Zpd.Lobby
{
    public sealed class LoginView : MonoBehaviour
    {
        [FormerlySerializedAs("playerId")]
        public InputField loginId;
        public InputField password;
        public Button login;
        public Text status;

        public bool IsConfigured => loginId != null && password != null &&
            loginId != password && login != null && status != null;

        public bool TryBindControls()
        {
            // Scene refreshes can lose bindings while leaving the authored controls intact.
            loginId = Resolve(loginId, "Login Card/ID");
            loginId = Resolve(loginId, "Login Card/Player ID");
            password = Resolve(password, "Login Card/Password");
            login = Resolve(login, "Login Card/Login");
            status = Resolve(status, "Login Card/Status");
            return IsConfigured;
        }

        private T Resolve<T>(T current, string path) where T : Component
        {
            return current != null ? current : transform.Find(path)?.GetComponent<T>();
        }

        public void Render(bool busy, string message)
        {
            loginId.interactable = !busy;
            password.interactable = !busy;
            login.interactable = !busy;
            status.text = message;
        }

        public void Focus()
        {
            loginId.Select();
            loginId.ActivateInputField();
        }

        public void FocusPassword()
        {
            password.Select();
            password.ActivateInputField();
        }

        public void ClearPassword()
        {
            if (password != null)
            {
                password.text = string.Empty;
            }
        }
    }
}
