using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Zpd.Defense
{
    /// <summary>Scene-authored UI bindings and presentation. Does not change game state.</summary>
    public sealed class DefenseView : MonoBehaviour
    {
        public GameObject readyPanel;
        public GameObject pausePanel;
        public GameObject helpPanel;
        public Button helpButton;
        public Button helpBackButton;
        public GameObject resultPanel;
        public Text healthText;
        public Text waveText;
        public Text scoreText;
        public Text hintText;
        public Text resultStats;
        public Button startButton;
        public Button resumeButton;
        public Button restartButton;
        public SpriteRenderer[] entryMarkers = new SpriteRenderer[0];
        public Image beaconHealthBar;
        public Image dashBar;
        public Text dashText;
        public Button nextWaveButton;
        public Text rewardTitle, rewardDetail, uploadStatus;
        public Button retryButton;

        public void ShowState(DefenseState state)
        {
            readyPanel.SetActive(state == DefenseState.Ready);
            pausePanel.SetActive(state == DefenseState.Paused);
            resultPanel.SetActive(state == DefenseState.Ended);
            helpPanel.SetActive(false);
        }

        public void ShowHelp(bool show)
        {
            helpPanel.SetActive(show);
            pausePanel.SetActive(!show);
            Select(show ? helpBackButton : helpButton);
        }

        public void ShowResult(DefenseModel model, string reason)
        {
            ShowState(DefenseState.Ended);
            resultStats.text = (reason == "death" ? "YOU WERE DEFEATED" : "THE BEACON WAS DESTROYED") + "\n" + model.Kills + " KILLS   /   WAVE " + model.Wave + "   /   " + Mathf.FloorToInt(model.Elapsed) + " SECONDS";
            Select(restartButton);
        }

        public void RenderRequests(string title, string detail, string upload, bool canRetry)
        {
            rewardTitle.text = title;
            rewardDetail.text = detail;
            uploadStatus.text = upload;
            retryButton.interactable = canRetry;
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
            healthText.text = "YOU " + model.PlayerHealth + " / 100     BEACON " + model.BeaconHealth + " / 100";
            waveText.text = "WAVE " + model.Wave + "   ENEMIES " + aliveCount + " / INCOMING " + model.Remaining;
            scoreText.text = Mathf.FloorToInt(model.Elapsed) + "s   /   THREAT " + Mathf.Max(1, threat);
            hintText.text = model.State == DefenseState.Ready
                ? ""
                : model.Phase == DefensePhase.Preparation
                ? awaitingCard
                ? "Choose one upgrade card"
                : "Next wave in " + Mathf.CeilToInt(model.PhaseRemaining) + "s"
                : model.Phase == DefensePhase.Warning
                ? "Incoming in " + Mathf.CeilToInt(model.PhaseRemaining) + "s"
                : "";

            if (beaconHealthBar != null)
            {
                beaconHealthBar.fillAmount = model.BeaconHealth / 100f;
                beaconHealthBar.color = model.BeaconHealth <= 30
                    ? new Color(1, 0.3f, 0.25f)
                    : new Color(0.35f, 0.9f, 0.75f);
            }

            if (dashBar != null)
            {
                dashBar.fillAmount = model.DashReady;
            }

            if (dashText != null)
            {
                dashText.text = model.DashReady >= 1
                    ? "DASH READY"
                    : "DASH " + Mathf.Max(0, model.NextDash - model.Elapsed).ToString("0.0") + "s";
            }

            if (nextWaveButton != null)
            {
                nextWaveButton.gameObject.SetActive(
                    model.State == DefenseState.Playing && model.Phase == DefensePhase.Preparation);
            }
        }

        public void RenderIndicators(DefenseModel model, DefenseGame.EnemySlot[] enemies)
        {
            bool playing = model.State == DefenseState.Playing;

            for (int i = 0; i < entryMarkers.Length; i++)
            {
                var marker = entryMarkers[i];

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
                bool active = enemy.root.gameObject.activeSelf;
                var behavior = DefenseEnemyFactory.For(enemy.role);

                if (enemy.roleMarker != null)
                {
                    enemy.roleMarker.enabled = active;
                    enemy.roleMarker.color = behavior.MarkerColor;
                    enemy.roleMarker.transform.localRotation = Quaternion.Euler(0, 0, behavior.MarkerAngle);
                    enemy.roleMarker.transform.localScale = behavior.MarkerScale;
                }

                if (enemy.attackMarker != null)
                {
                    enemy.attackMarker.enabled = active && enemy.windup > 0;
                    enemy.attackMarker.color = new Color(1, 0.15f, 0.1f);
                    enemy.attackMarker.transform.localScale = new Vector3(
                        8 * Mathf.Clamp01(enemy.windup / behavior.Attack.WindupSeconds),
                        1,
                        1);
                }

                if (enemy.aimMarker != null)
                {
                    enemy.aimMarker.enabled = active && enemy.windup > 0 && behavior.Attack.IsRanged;
                    enemy.aimMarker.color = new Color(1, 0.2f, 0.65f, 0.4f);
                    float length = behavior.Attack.Range;
                    enemy.aimMarker.transform.localPosition = enemy.aimDirection * length * 0.5f;
                    enemy.aimMarker.transform.localRotation = Quaternion.Euler(
                        0,
                        0,
                        Mathf.Atan2(enemy.aimDirection.y, enemy.aimDirection.x) * Mathf.Rad2Deg);
                    enemy.aimMarker.transform.localScale = new Vector3(
                        length / enemy.aimMarker.sprite.bounds.size.x,
                        0.045f / enemy.aimMarker.sprite.bounds.size.y,
                        1);
                }

                if (enemy.healthBar != null)
                {
                    enemy.healthBar.enabled = active && (enemy.role != DefenseEnemyRole.Hunter || model.Elapsed < enemy.showHealthUntil);
                    enemy.healthBar.color = new Color(0.4f, 1, 0.5f);
                    enemy.healthBar.transform.localScale = new Vector3(
                        8 * Mathf.Clamp01((float)enemy.health / Mathf.Max(1, enemy.maxHealth)),
                        0.7f,
                        1);
                }
            }

            if (nextWaveButton != null)
            {
                nextWaveButton.gameObject.SetActive(playing && model.Phase == DefensePhase.Preparation);
            }
        }
    }
}
