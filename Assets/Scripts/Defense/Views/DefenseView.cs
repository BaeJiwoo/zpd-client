using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Zpd.Defense
{
    /// <summary>Scene-authored UI bindings and presentation. Does not change game state.</summary>
    public sealed class DefenseView : MonoBehaviour
    {
        [FormerlySerializedAs("readyPanel")]
        public GameObject game_object_ready_panel;

        [FormerlySerializedAs("pausePanel")]
        public GameObject game_object_pause_panel;

        [FormerlySerializedAs("helpPanel")]
        public GameObject game_object_help_panel;

        [FormerlySerializedAs("helpButton")]
        public Button btn_help;

        [FormerlySerializedAs("helpBackButton")]
        public Button btn_help_back;

        [FormerlySerializedAs("resultPanel")]
        public GameObject game_object_result_panel;

        [FormerlySerializedAs("healthText")]
        public Text txt_health;

        [FormerlySerializedAs("waveText")]
        public Text txt_wave;

        [FormerlySerializedAs("scoreText")]
        public Text txt_score;

        [FormerlySerializedAs("hintText")]
        public Text txt_hint;

        [FormerlySerializedAs("resultStats")]
        public Text txt_result_stats;

        [FormerlySerializedAs("startButton")]
        public Button btn_start;

        [FormerlySerializedAs("resumeButton")]
        public Button btn_resume;

        [FormerlySerializedAs("restartButton")]
        public Button btn_restart;

        [FormerlySerializedAs("entryMarkers")]
        public SpriteRenderer[] sprite_renderer_entry_markers = new SpriteRenderer[0];

        [FormerlySerializedAs("beaconHealthBar")]
        public Image img_beacon_health_bar;

        [FormerlySerializedAs("dashBar")]
        public Image img_dash_bar;

        [FormerlySerializedAs("dashText")]
        public Text txt_dash;

        [FormerlySerializedAs("nextWaveButton")]
        public Button btn_next_wave;

        [FormerlySerializedAs("rewardTitle")]
        public Text txt_reward_title;

        [FormerlySerializedAs("rewardDetail")]
        public Text txt_reward_detail;

        [FormerlySerializedAs("uploadStatus")]
        public Text txt_upload_status;

        [FormerlySerializedAs("retryButton")]
        public Button btn_retry;

        public void ShowState(DefenseState state)
        {
            game_object_ready_panel.SetActive(state == DefenseState.Ready);
            game_object_pause_panel.SetActive(state == DefenseState.Paused);
            game_object_result_panel.SetActive(state == DefenseState.Ended);
            game_object_help_panel.SetActive(false);
        }

        public void ShowHelp(bool show)
        {
            game_object_help_panel.SetActive(show);
            game_object_pause_panel.SetActive(!show);
            Select(show ? btn_help_back : btn_help);
        }

        public void ShowResult(DefenseModel model, string reason)
        {
            ShowState(DefenseState.Ended);
            txt_result_stats.text = (reason == "death" ? "YOU WERE DEFEATED" : "THE BEACON WAS DESTROYED") + "\n" + model.Kills + " KILLS   /   WAVE " + model.Wave + "   /   " + Mathf.FloorToInt(model.Elapsed) + " SECONDS";
            Select(btn_restart);
        }

        public void RenderRequests(string title, string detail, string upload, bool canRetry)
        {
            txt_reward_title.text = title;
            txt_reward_detail.text = detail;
            txt_upload_status.text = upload;
            btn_retry.interactable = canRetry;
        }

        public static void Select(Button button)
        {
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(button != null ? button.gameObject : null);
            }
        }

        public void RenderHud(DefenseModel model, int aliveCount, int threat, bool awaitingCard)
        {
            txt_health.text = "YOU " + model.PlayerHealth + " / 100     BEACON " + model.BeaconHealth + " / 100";
            txt_wave.text = "WAVE " + model.Wave + "   ENEMIES " + aliveCount + " / INCOMING " + model.Remaining;
            txt_score.text = Mathf.FloorToInt(model.Elapsed) + "s   /   THREAT " + Mathf.Max(1, threat);
            txt_hint.text = model.State == DefenseState.Ready
                ? ""
                : model.Phase == DefensePhase.Preparation
                ? awaitingCard
                ? "Choose one upgrade card"
                : "Next wave in " + Mathf.CeilToInt(model.PhaseRemaining) + "s"
                : model.Phase == DefensePhase.Warning
                ? "Incoming in " + Mathf.CeilToInt(model.PhaseRemaining) + "s"
                : "";

            if (img_beacon_health_bar != null)
            {
                img_beacon_health_bar.fillAmount = model.BeaconHealth / 100f;
                img_beacon_health_bar.color = model.BeaconHealth <= 30
                    ? new Color(1, 0.3f, 0.25f)
                    : new Color(0.35f, 0.9f, 0.75f);
            }

            if (img_dash_bar != null)
            {
                img_dash_bar.fillAmount = model.DashReady;
            }

            if (txt_dash != null)
            {
                txt_dash.text = model.DashReady >= 1
                    ? "DASH READY"
                    : "DASH " + Mathf.Max(0, model.NextDash - model.Elapsed).ToString("0.0") + "s";
            }

            if (btn_next_wave != null)
            {
                btn_next_wave.gameObject.SetActive(
                    model.State == DefenseState.Playing && model.Phase == DefensePhase.Preparation);
            }
        }

        public void RenderIndicators(DefenseModel model, DefenseGame.EnemySlot[] enemies)
        {
            bool playing = model.State == DefenseState.Playing;

            for (int i = 0; i < sprite_renderer_entry_markers.Length; i++)
            {
                var marker = sprite_renderer_entry_markers[i];

                if (marker == null)
                {
                    continue;
                }

                bool visible = playing && model.Phase != DefensePhase.Preparation && model.Remaining > 0 && i < model.EntranceCount;
                marker.gameObject.SetActive(visible);

                if (!visible)
                {
                    continue;
                }

                marker.transform.position = model.Entrances[i] * 0.97f;
                marker.color = new Color(
                    1,
                    0.35f,
                    0.12f,
                    model.Phase == DefensePhase.Warning
                    ? 0.65f + 0.3f * Mathf.Sin(model.Elapsed * 14)
                    : 0.45f);
            }

            foreach (var enemy in enemies)
            {
                bool active = enemy.transform_root.gameObject.activeSelf;
                var behavior = DefenseEnemyFactory.For(enemy.role);

                if (enemy.sprite_renderer_role_marker != null)
                {
                    enemy.sprite_renderer_role_marker.enabled = active;
                    enemy.sprite_renderer_role_marker.color = behavior.marker_color;
                    enemy.sprite_renderer_role_marker.transform.localRotation = Quaternion.Euler(0, 0, behavior.marker_angle_degrees);
                    enemy.sprite_renderer_role_marker.transform.localScale = behavior.marker_scale;
                }

                if (enemy.sprite_renderer_attack_marker != null)
                {
                    enemy.sprite_renderer_attack_marker.enabled = active && enemy.attack_windup_elapsed_seconds > 0;
                    enemy.sprite_renderer_attack_marker.color = new Color(1, 0.15f, 0.1f);
                    enemy.sprite_renderer_attack_marker.transform.localScale = new Vector3(
                        8 * Mathf.Clamp01(enemy.attack_windup_elapsed_seconds / behavior.attack_strategy.WindupSeconds),
                        1,
                        1);
                }

                if (enemy.sprite_renderer_aim_marker != null)
                {
                    enemy.sprite_renderer_aim_marker.enabled = active && enemy.attack_windup_elapsed_seconds > 0 && behavior.attack_strategy.IsRanged;
                    enemy.sprite_renderer_aim_marker.color = new Color(1, 0.2f, 0.65f, 0.4f);
                    float length = behavior.attack_strategy.Range;
                    enemy.sprite_renderer_aim_marker.transform.localPosition = enemy.aim_direction * length * 0.5f;
                    enemy.sprite_renderer_aim_marker.transform.localRotation = Quaternion.Euler(
                        0,
                        0,
                        Mathf.Atan2(enemy.aim_direction.y, enemy.aim_direction.x) * Mathf.Rad2Deg);
                    enemy.sprite_renderer_aim_marker.transform.localScale = new Vector3(
                        length / enemy.sprite_renderer_aim_marker.sprite.bounds.size.x,
                        0.045f / enemy.sprite_renderer_aim_marker.sprite.bounds.size.y,
                        1);
                }

                if (enemy.sprite_renderer_health_bar != null)
                {
                    enemy.sprite_renderer_health_bar.enabled = active && (enemy.role != DefenseEnemyRole.Hunter || model.Elapsed < enemy.health_visible_until_seconds);
                    enemy.sprite_renderer_health_bar.color = new Color(0.4f, 1, 0.5f);
                    enemy.sprite_renderer_health_bar.transform.localScale = new Vector3(
                        8 * Mathf.Clamp01((float)enemy.health / Mathf.Max(1, enemy.max_health)),
                        0.7f,
                        1);
                }
            }

            if (btn_next_wave != null)
            {
                btn_next_wave.gameObject.SetActive(playing && model.Phase == DefensePhase.Preparation);
            }
        }
    }
}
