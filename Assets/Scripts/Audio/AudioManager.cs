using System.Collections.Generic;
using UnityEngine;
using KitchenChaos.Core;

namespace KitchenChaos.Audio
{
    public enum SfxType
    {
        Pickup,
        Drop,
        Chop,
        Sizzle,
        Cooked,
        Burned,
        DeliverSuccess,
        DeliverFail,
        OrderExpire,
        ButtonClick,
        GameOver,
        Dash,
        Tick,
        Warning,
        Combo,
        DayComplete,
        Victory,
        Bell,
        ChopDone
    }

    /// <summary>
    /// 极简音效播放器：音效片段在运行时用短噪声/正弦波合成，
    /// 因此工程无需任何外部 wav 素材即可出声。也会尝试读取 Resources/Audio 下烘焙好的片段覆盖。
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Range(0f, 1f)] public float volume = 0.8f;
        [Range(0f, 1f)] public float musicVolume = 0.22f;

        private const int VoiceCount = 10;
        private AudioSource[] voices;
        private AudioSource music;
        private AudioClip musicClip;
        private bool musicPaused;
        private int nextVoice;
        private readonly Dictionary<SfxType, AudioClip> clips = new Dictionary<SfxType, AudioClip>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            voices = new AudioSource[VoiceCount];
            for (int i = 0; i < VoiceCount; i++)
            {
                AudioSource voice = gameObject.AddComponent<AudioSource>();
                voice.playOnAwake = false;
                voice.spatialBlend = 0f;
                voices[i] = voice;
            }
            music = gameObject.AddComponent<AudioSource>();
            music.playOnAwake = false;
            music.loop = true;
            music.spatialBlend = 0f;
            music.volume = musicVolume;
        }

        public void Play(SfxType type) => Play(type, 1f);

        public void Play(SfxType type, float pitch)
        {
            if (voices == null || voices.Length == 0) return;
            if (!clips.TryGetValue(type, out AudioClip clip))
            {
                clip = BuildClip(type);
                clips[type] = clip;
            }
            if (clip == null) return;
            AudioSource voice = voices[nextVoice];
            nextVoice = (nextVoice + 1) % voices.Length;
            voice.pitch = Mathf.Clamp(pitch * Random.Range(0.94f, 1.06f), 0.45f, 2.5f);
            voice.PlayOneShot(clip, volume);
        }

        private void Update()
        {
            if (music == null) return;
            GameManager gm = GameManager.Instance;
            if (gm == null) return;
            bool wantsMusic = gm.State == GameState.Ready || gm.State == GameState.Playing
                || gm.State == GameState.Paused || gm.State == GameState.DayComplete;
            if (!wantsMusic)
            {
                if (music.isPlaying) music.Stop();
                musicPaused = false;
                return;
            }
            if (gm.State == GameState.Paused)
            {
                if (music.isPlaying) { music.Pause(); musicPaused = true; }
                return;
            }
            if (!music.isPlaying)
            {
                if (musicPaused && music.clip != null)
                {
                    music.UnPause();
                    musicPaused = false;
                }
                else
                {
                    EnsureMusicClip();
                    if (musicClip != null) { music.clip = musicClip; music.Play(); }
                }
            }
            bool urgent = gm.State == GameState.Playing && gm.Timer != null && gm.Timer.TimeRemaining <= 20f;
            music.pitch = Mathf.MoveTowards(music.pitch, urgent ? 1.1f : 1f, Time.unscaledDeltaTime * 0.6f);
        }

        private void EnsureMusicClip()
        {
            if (musicClip != null) return;
            musicClip = BuildMusic();
        }

        private void OnDestroy()
        {
            foreach (AudioClip clip in clips.Values)
                if (clip != null) Destroy(clip);
            clips.Clear();
            if (musicClip != null) Destroy(musicClip);
            musicClip = null;
            if (Instance == this) Instance = null;
        }

        private AudioClip BuildClip(SfxType type)
        {
            switch (type)
            {
                case SfxType.Pickup: return Tone(520f, 0.09f, 0.45f, 0.4f);
                case SfxType.Drop: return Tone(340f, 0.1f, 0.5f, 0.5f);
                case SfxType.Chop: return Chop();
                case SfxType.Sizzle: return Noise(0.26f, 0.22f, 0.55f);
                case SfxType.Cooked: return Chord(new[] { 660f, 880f, 1100f }, 0.28f, 0.4f);
                case SfxType.Burned: return Burned();
                case SfxType.DeliverSuccess: return Arpeggio(new[] { 523.25f, 659.25f, 783.99f, 1046.5f }, 0.055f, 0.4f);
                case SfxType.DeliverFail: return Tone(160f, 0.26f, 0.55f, 0.15f);
                case SfxType.OrderExpire: return Tone(210f, 0.34f, 0.42f, 0.2f);
                case SfxType.ButtonClick: return Tone(640f, 0.045f, 0.32f, 0.25f);
                case SfxType.GameOver: return Chord(new[] { 392f, 330f, 262f }, 0.6f, 0.45f);
                case SfxType.Dash: return Whoosh();
                case SfxType.Tick: return Tone(1180f, 0.035f, 0.3f, 0.6f);
                case SfxType.Warning: return Warning();
                case SfxType.Combo: return Arpeggio(new[] { 783.99f, 1046.5f, 1318.5f }, 0.05f, 0.32f);
                case SfxType.DayComplete: return Arpeggio(new[] { 523.25f, 659.25f, 783.99f, 1046.5f, 1318.5f }, 0.085f, 0.38f);
                case SfxType.Victory: return Victory();
                case SfxType.Bell: return Bell();
                case SfxType.ChopDone: return Arpeggio(new[] { 698.46f, 932.33f }, 0.055f, 0.3f);
                default: return null;
            }
        }

        private static AudioClip Tone(float frequency, float seconds, float gain, float brightness = 0.1f)
        {
            int sampleRate = 44100;
            int samples = Mathf.CeilToInt(sampleRate * seconds);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)sampleRate;
                float x = t / seconds;
                float envelope = Mathf.Min(1f, t / 0.006f) * Mathf.Pow(1f - x, 2.2f);
                float wave = Mathf.Sin(2f * Mathf.PI * frequency * t);
                wave += Mathf.Sin(2f * Mathf.PI * frequency * 2f * t) * brightness;
                data[i] = wave / (1f + brightness) * envelope * gain;
            }
            return MakeClip("tone_" + frequency, data, sampleRate);
        }

        private static AudioClip Chord(float[] frequencies, float seconds, float gain)
        {
            int sampleRate = 44100;
            int samples = Mathf.CeilToInt(sampleRate * seconds);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)sampleRate;
                float envelope = Mathf.Min(1f, t / 0.008f) * Mathf.Pow(1f - t / seconds, 2f);
                float sum = 0f;
                for (int f = 0; f < frequencies.Length; f++)
                    sum += Mathf.Sin(2f * Mathf.PI * frequencies[f] * t) + Mathf.Sin(2f * Mathf.PI * frequencies[f] * 2f * t) * 0.15f;
                data[i] = sum / frequencies.Length * envelope * gain;
            }
            return MakeClip("chord", data, sampleRate);
        }

        private static AudioClip Arpeggio(float[] frequencies, float step, float gain)
        {
            int sampleRate = 44100;
            int samples = Mathf.CeilToInt(sampleRate * (step * frequencies.Length + 0.22f));
            float[] data = new float[samples];
            for (int n = 0; n < frequencies.Length; n++)
            {
                int start = Mathf.RoundToInt(n * step * sampleRate);
                int length = Mathf.Min(samples - start, Mathf.RoundToInt(sampleRate * 0.3f));
                for (int i = 0; i < length; i++)
                {
                    float t = i / (float)sampleRate;
                    float envelope = Mathf.Min(1f, t / 0.006f) * Mathf.Pow(1f - i / (float)length, 2.4f);
                    float wave = Mathf.Sin(2f * Mathf.PI * frequencies[n] * t) + Mathf.Sin(2f * Mathf.PI * frequencies[n] * 2f * t) * 0.2f;
                    data[start + i] += wave * envelope * gain * 0.7f;
                }
            }
            return MakeClip("arp", data, sampleRate);
        }

        private static AudioClip Chop()
        {
            int sampleRate = 44100;
            int samples = Mathf.CeilToInt(sampleRate * 0.09f);
            float[] data = new float[samples];
            float last = 0f;
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)sampleRate;
                float envelope = Mathf.Pow(1f - t / 0.09f, 2.5f);
                float white = Random.value * 2f - 1f;
                last = Mathf.Lerp(last, white, 0.6f);
                data[i] = (last * 0.7f + Mathf.Sin(2f * Mathf.PI * 130f * t) * 0.4f) * envelope * 0.55f;
            }
            return MakeClip("chop", data, sampleRate);
        }

        private static AudioClip Burned()
        {
            int sampleRate = 44100;
            int samples = Mathf.CeilToInt(sampleRate * 0.45f);
            float[] data = new float[samples];
            float last = 0f;
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)sampleRate;
                float envelope = Mathf.Min(1f, t / 0.02f) * Mathf.Pow(1f - t / 0.45f, 1.6f);
                last = Mathf.Lerp(last, Random.value * 2f - 1f, 0.25f);
                float hiss = last * 0.35f;
                float thud = Mathf.Sin(2f * Mathf.PI * (130f - t * 60f) * t) * 0.6f;
                data[i] = (thud + hiss) * envelope * 0.55f;
            }
            return MakeClip("burned", data, sampleRate);
        }

        private static AudioClip Whoosh()
        {
            int sampleRate = 44100;
            int samples = Mathf.CeilToInt(sampleRate * 0.22f);
            float[] data = new float[samples];
            float last = 0f;
            for (int i = 0; i < samples; i++)
            {
                float x = i / (float)samples;
                float envelope = Mathf.Sin(x * Mathf.PI) * (1f - x * 0.4f);
                float white = Random.value * 2f - 1f;
                last = Mathf.Lerp(last, white, 0.12f + x * 0.25f);
                data[i] = last * envelope * 0.6f;
            }
            return MakeClip("whoosh", data, sampleRate);
        }

        private static AudioClip Warning()
        {
            int sampleRate = 44100;
            float seconds = 0.18f;
            int samples = Mathf.CeilToInt(sampleRate * seconds);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)sampleRate;
                float attack = Mathf.Min(1f, t / 0.01f);
                float release = Mathf.Min(1f, (seconds - t) / 0.03f);
                data[i] = Mathf.Sin(2f * Mathf.PI * 740f * t) * attack * release * 0.22f;
            }
            return MakeClip("warning", data, sampleRate);
        }

        private static AudioClip Bell()
        {
            int sampleRate = 44100;
            int samples = Mathf.CeilToInt(sampleRate * 0.6f);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)sampleRate;
                float envelope = Mathf.Min(1f, t / 0.004f) * Mathf.Exp(-t * 5.2f);
                float wave = Mathf.Sin(2f * Mathf.PI * 1046.5f * t) * 0.6f
                    + Mathf.Sin(2f * Mathf.PI * 1567.98f * t) * 0.28f
                    + Mathf.Sin(2f * Mathf.PI * 2093f * t) * 0.16f;
                data[i] = wave * envelope * 0.42f;
            }
            return MakeClip("bell", data, sampleRate);
        }

        private static AudioClip Victory()
        {
            int sampleRate = 44100;
            float step = 0.13f;
            float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };
            int samples = Mathf.CeilToInt(sampleRate * (step * notes.Length + 0.9f));
            float[] data = new float[samples];
            for (int n = 0; n < notes.Length; n++)
            {
                int start = Mathf.RoundToInt(n * step * sampleRate);
                bool last = n == notes.Length - 1;
                int length = samples - start;
                for (int i = 0; i < length; i++)
                {
                    float t = i / (float)sampleRate;
                    float envelope = Mathf.Min(1f, t / 0.01f) * Mathf.Exp(-t * (last ? 1.5f : 4.5f));
                    float chord = last ? Mathf.Sin(2f * Mathf.PI * notes[n] * 0.5f * t) * 0.3f + Mathf.Sin(2f * Mathf.PI * notes[n] * 0.75f * t) * 0.25f : 0f;
                    float wave = Mathf.Sin(2f * Mathf.PI * notes[n] * t) + Mathf.Sin(2f * Mathf.PI * notes[n] * 2f * t) * 0.18f;
                    data[start + i] += (wave * 0.5f + chord) * envelope * 0.4f;
                }
            }
            return MakeClip("victory", data, sampleRate);
        }

        // 8 小节轻爵士琶音循环，低音量铺底，不抢操作音效。
        private static AudioClip BuildMusic()
        {
            const int sampleRate = 22050;
            const float bpm = 112f;
            float beat = 60f / bpm;
            int[][] chords =
            {
                new[] { 60, 64, 67 }, new[] { 57, 60, 64 }, new[] { 53, 57, 60 }, new[] { 55, 59, 62 },
                new[] { 60, 64, 67 }, new[] { 57, 60, 64 }, new[] { 53, 57, 60 }, new[] { 55, 59, 62 }
            };
            int stepsPerChord = 8;
            float step = beat * 0.5f;
            int total = Mathf.RoundToInt(sampleRate * chordLength(chords.Length, beat));
            float[] data = new float[total];
            int[] pattern = { 0, 1, 2, 1, 2, 0, 1, 2 };
            for (int bar = 0; bar < chords.Length; bar++)
            {
                int[] chord = chords[bar];
                for (int s = 0; s < stepsPerChord; s++)
                {
                    int start = Mathf.RoundToInt((bar * stepsPerChord + s) * step * sampleRate);
                    int length = Mathf.RoundToInt(step * 1.7f * sampleRate);
                    float freq = MidiToFreq(chord[pattern[s]] + 12);
                    AddPluck(data, sampleRate, start, length, freq, 0.1f, 0.75f);
                    if (s == 0 || s == 4)
                    {
                        int bassStart = Mathf.RoundToInt((bar * stepsPerChord + s) * step * sampleRate);
                        AddPluck(data, sampleRate, bassStart, Mathf.RoundToInt(step * 3.2f * sampleRate), MidiToFreq(chord[0] - 12), 0.16f, 0.55f);
                    }
                }
            }
            float peak = 0.0001f;
            for (int i = 0; i < total; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
            float norm = 0.8f / peak;
            int fade = Mathf.Min(1024, total / 4);
            for (int i = 0; i < total; i++)
            {
                float edge = 1f;
                if (i < fade) edge = i / (float)fade;
                else if (i > total - fade) edge = (total - i) / (float)fade;
                data[i] *= norm * edge;
            }
            return MakeClip("kitchen_music", data, sampleRate);
        }

        private static float chordLength(int bars, float beat) => bars * beat * 4f;

        private static void AddPluck(float[] data, int sampleRate, int start, int length, float frequency, float gain, float decay)
        {
            length = Mathf.Min(length, data.Length - start);
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)sampleRate;
                float envelope = Mathf.Min(1f, t / 0.012f) * Mathf.Exp(-t * decay * 4f);
                float wave = Mathf.Sin(2f * Mathf.PI * frequency * t)
                    + Mathf.Sin(2f * Mathf.PI * frequency * 2f * t) * 0.22f
                    + Mathf.Sin(2f * Mathf.PI * frequency * 3f * t) * 0.07f;
                data[start + i] += wave * envelope * gain;
            }
        }

        private static float MidiToFreq(int midi) => 440f * Mathf.Pow(2f, (midi - 69) / 12f);

        private static AudioClip MakeClip(string name, float[] data, int sampleRate)
        {
            AudioClip clip = AudioClip.Create(name, data.Length, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip Noise(float seconds, float gain, float blend)
        {
            int sampleRate = 44100;
            int samples = Mathf.CeilToInt(sampleRate * seconds);
            float[] data = new float[samples];
            float last = 0f;
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)sampleRate;
                float envelope = Mathf.Min(1f, t / 0.02f) * Mathf.Pow(1f - t / seconds, 1.8f);
                float white = Random.value * 2f - 1f;
                last = Mathf.Lerp(last, white, blend);
                data[i] = last * envelope * gain;
            }
            return MakeClip("noise", data, sampleRate);
        }
    }
}
