using UnityEngine;
using UnityEngine.Serialization;

namespace Zpd.Defense
{
    public enum DefenseCue
    {
        Shot,
        Scatter,
        Hit,
        Kill,
        Pickup,
        Shop,
        Purchase,
        Hurt,
        Defeat,
        Warning,
        Dash
    }

    /// <summary>Plays editor-authored WAV assets on pre-placed sources; no runtime audio object creation.</summary>
    public sealed class DefenseAudio : MonoBehaviour
    {
        [FormerlySerializedAs("combat")]
        public AudioSource audio_source_combat;

        [FormerlySerializedAs("feedback")]
        public AudioSource audio_source_feedback;

        [FormerlySerializedAs("clips")]
        public AudioClip[] audio_clip_cues;

        [Range(0, 1)]
        public float volume = 0.65f;

        public bool Muted { get; private set; }

        private readonly float[] next_cue_at_seconds = new float[11];

        public void Play(DefenseCue cue)
        {
            int index = (int)cue;

            if (Muted || index >= audio_clip_cues.Length || audio_clip_cues[index] == null || Time.unscaledTime < next_cue_at_seconds[index])
            {
                return;
            }

            next_cue_at_seconds[index] = Time.unscaledTime + (cue == DefenseCue.Hit || cue == DefenseCue.Kill ? 0.075f : 0.035f);
            var source = cue == DefenseCue.Shot || cue == DefenseCue.Scatter || cue == DefenseCue.Hit
                ? audio_source_combat
                : audio_source_feedback;
            source.PlayOneShot(audio_clip_cues[index], volume * (cue == DefenseCue.Hit ? 0.35f : 0.75f));
        }

        public void ToggleMute()
        {
            Muted = !Muted;

            if (Muted)
            {
                Stop();
            }
        }

        public void Stop()
        {
            audio_source_combat.Stop();
            audio_source_feedback.Stop();
        }
    }
}
