using UnityEngine;

namespace Zpd.Defense
{
    public enum DefenseCue { Shot, Scatter, Hit, Kill, Pickup, Shop, Purchase, Hurt, Defeat, Warning, Dash }

    /// <summary>Plays editor-authored WAV assets on pre-placed sources; no runtime audio object creation.</summary>
    public sealed class DefenseAudio : MonoBehaviour
    {
        public AudioSource combat;
        public AudioSource feedback;
        public AudioClip[] clips;
        [Range(0, 1)] public float volume = 0.65f;
        public bool Muted { get; private set; }
        private readonly float[] nextAllowed = new float[11];

        public void Play(DefenseCue cue)
        {
            int index = (int)cue;
            if (Muted || index >= clips.Length || clips[index] == null || Time.unscaledTime < nextAllowed[index]) return;
            nextAllowed[index] = Time.unscaledTime + (cue == DefenseCue.Hit || cue == DefenseCue.Kill ? 0.075f : 0.035f);
            var source = cue == DefenseCue.Shot || cue == DefenseCue.Scatter || cue == DefenseCue.Hit ? combat : feedback;
            source.PlayOneShot(clips[index], volume * (cue == DefenseCue.Hit ? 0.35f : 0.75f));
        }
        public void ToggleMute() { Muted = !Muted; if (Muted) Stop(); }
        public void Stop() { combat.Stop(); feedback.Stop(); }
    }
}
