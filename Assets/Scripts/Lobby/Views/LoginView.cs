using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;

namespace Zpd.Lobby
{
    public sealed class LoginView : MonoBehaviour
    {
        [FormerlySerializedAs("loginId")]
        [FormerlySerializedAs("playerId")]
        public InputField input_login_id;

        [FormerlySerializedAs("password")]
        public InputField input_password;

        [FormerlySerializedAs("login")]
        public Button btn_login;

        [FormerlySerializedAs("status")]
        public Text txt_status;

        public bool IsConfigured => input_login_id != null && input_password != null &&
            input_login_id != input_password && btn_login != null && txt_status != null;

        public bool TryBindControls()
        {
            // Scene refreshes can lose bindings while leaving the authored controls intact.
            input_login_id = Resolve(input_login_id, "Login Card/ID");
            input_login_id = Resolve(input_login_id, "Login Card/Player ID");
            input_password = Resolve(input_password, "Login Card/Password");
            btn_login = Resolve(btn_login, "Login Card/Login");
            txt_status = Resolve(txt_status, "Login Card/Status");
            return IsConfigured;
        }

        private T Resolve<T>(T current, string path) where T : Component
        {
            return current != null ? current : transform.Find(path)?.GetComponent<T>();
        }

        public void Render(bool busy, string message)
        {
            input_login_id.interactable = !busy;
            input_password.interactable = !busy;
            btn_login.interactable = !busy;
            txt_status.text = message;
        }

        public void Focus()
        {
            input_login_id.Select();
            input_login_id.ActivateInputField();
        }

        public void FocusPassword()
        {
            input_password.Select();
            input_password.ActivateInputField();
        }

        public void ClearPassword()
        {
            if (input_password != null)
            {
                input_password.text = string.Empty;
            }
        }
    }
}
