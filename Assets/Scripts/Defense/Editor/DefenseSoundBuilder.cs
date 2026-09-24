using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Zpd.Defense.Editor
{
    /// <summary>Original deterministic arcade synthesis, baked as PCM WAV assets in the editor.</summary>
    public static class DefenseSoundBuilder
    {
        public static AudioClip[] CreateClips()
        {
            const string folder = "Assets/Resources/Audio/Defense";
            Directory.CreateDirectory(folder);
            var clips = new AudioClip[Enum.GetValues(typeof(DefenseCue)).Length];
            for (int cue = 0; cue < clips.Length; cue++)
            {
                string path = folder + "/" + ((DefenseCue)cue).ToString().ToLowerInvariant() + ".wav";
                if (!File.Exists(path)) Write(path, (DefenseCue)cue);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (AudioImporter)AssetImporter.GetAtPath(path);
                importer.forceToMono = true;
                var settings = importer.defaultSampleSettings;
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.PCM;
                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
                clips[cue] = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            }
            return clips;
        }
        private static void Write(string path, DefenseCue cue)
        {
            const int rate = 22050;
            float duration = cue == DefenseCue.Defeat ? 0.8f : cue == DefenseCue.Shop ? 0.55f :
                cue == DefenseCue.Purchase ? 0.32f : cue == DefenseCue.Warning ? 0.28f : cue == DefenseCue.Scatter ? 0.22f : 0.13f;
            int count = (int)(duration * rate);
            var random = new System.Random(412 + (int)cue);
            double phase = 0;
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + count * 2);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
                writer.Write((short)1); writer.Write((short)1); writer.Write(rate); writer.Write(rate * 2);
                writer.Write((short)2); writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(count * 2);
                for (int i = 0; i < count; i++)
                {
                    double t = (double)i / rate, u = t / duration;
                    double frequency, noise = 0, gain = 0.65;
                    switch (cue)
                    {
                        case DefenseCue.Shot: frequency = 280 - 180 * u; noise = 0.45; break;
                        case DefenseCue.Scatter: frequency = 130 - 80 * u; noise = 0.7; break;
                        case DefenseCue.Hit: frequency = 650 - 350 * u; noise = 0.45; gain = 0.45; break;
                        case DefenseCue.Kill: frequency = 380 + 250 * u; break;
                        case DefenseCue.Pickup: frequency = u < 0.45 ? 1046 : 1568; gain = 0.42; break;
                        case DefenseCue.Shop: frequency = u < 0.33 ? 523 : u < 0.66 ? 659 : 784; break;
                        case DefenseCue.Purchase: frequency = u < 0.5 ? 784 : 1046; break;
                        case DefenseCue.Hurt: frequency = 190 - 95 * u; noise = 0.35; break;
                        case DefenseCue.Defeat: frequency = u < 0.33 ? 330 : u < 0.66 ? 261 : 196; break;
                        case DefenseCue.Warning: frequency = 880; gain = Math.Sin(t * Math.PI * 16) > 0 ? 0.45 : 0; break;
                        default: frequency = 200 + 800 * u; noise = 0.3; gain = 0.35; break;
                    }
                    phase += frequency * Math.PI * 2 / rate;
                    double tone = Math.Sin(phase) * 0.8 + Math.Sin(phase * 2) * 0.2;
                    double envelope = Math.Min(1, t / 0.005) * Math.Pow(1 - u, 1.5);
                    double sample = (tone * (1 - noise) + (random.NextDouble() * 2 - 1) * noise) * envelope * gain;
                    writer.Write((short)(sample * short.MaxValue));
                }
            }
        }
    }
}
