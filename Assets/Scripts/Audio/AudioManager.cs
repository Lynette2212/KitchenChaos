using System;
using System.Collections.Generic;
using UnityEngine;
using KitchenChaos.Core;

namespace KitchenChaos.Audio
{
    public enum SfxType
    {
        Pickup = 0, Drop = 1, Chop = 2, Sizzle = 3, Cooked = 4, Burned = 5,
        DeliverSuccess = 6, DeliverFail = 7, OrderExpire = 8, ButtonClick = 9,
        GameOver = 10, Dash = 11, Tick = 12, Warning = 13, Combo = 14,
        DayComplete = 15, Victory = 16, Bell = 17, ChopDone = 18,
        Footstep = 19, PlatePickup = 20, PlatePlace = 21, Trash = 22
    }

    // Bounded transient voices, independently owned stove loops, and one imported music bed.
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }
        [Range(0f, 1f)] public float volume = 0.8f;
        [Range(0f, 1f)] public float musicVolume = 0.25f;
        public AudioSource MusicSource => music;
        public IReadOnlyList<AudioSource> SfxSources => exposedVoices;

        private const int VoiceCount = 13; // Eight Foley, four cues, one protected outcome voice.
        private const int FoleyVoiceCount = 8;
        private const float VoiceRelease = 0.045f;
        private AudioSource[] voices;
        private IReadOnlyList<AudioSource> exposedVoices;
        private readonly SfxType[] voiceTypes = new SfxType[VoiceCount];
        private readonly bool[] worldVoices = new bool[VoiceCount];
        private readonly float[] voiceFadeUntil = new float[VoiceCount];
        private readonly Dictionary<SfxType, ClipBank> banks = new Dictionary<SfxType, ClipBank>();
        private readonly Dictionary<SfxType, float> lastPlayed = new Dictionary<SfxType, float>();
        private readonly List<AudioClip> generatedClips = new List<AudioClip>();
        private readonly List<LoopVoice> loops = new List<LoopVoice>();
        private AudioSource music;
        private AudioLowPassFilter musicFilter;
        private GameManager manager;
        private bool musicLoadAttempted;
        private float musicGain;
        private float duckGain = 1f;
        private float duckTarget = 1f;
        private float duckUntil;
        private float outcomeUntil;
        private int loopSequence;

        private sealed class ClipBank
        {
            public AudioClip[] clips;
            public System.Random random;
            public int previous = -1;
            public AudioClip Next()
            {
                int index = previous < 0 ? random.Next(clips.Length)
                    : (previous + 1 + random.Next(clips.Length - 1)) % clips.Length;
                previous = index;
                return clips[index];
            }
        }

        private sealed class LoopVoice
        {
            public AudioSource source;
            public float level;
            public float gain;
            public bool paused;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            voices = new AudioSource[VoiceCount];
            for (int i = 0; i < VoiceCount; i++)
                voices[i] = CreateSource("SfxVoice_" + i, transform);
            exposedVoices = Array.AsReadOnly(voices);
            music = CreateSource("CafeMusic", transform);
            music.loop = true;
            music.priority = 160;
            music.volume = 0f;
            // The filter lives on the music object, never on the Foley/UI sources.
            musicFilter = music.gameObject.AddComponent<AudioLowPassFilter>();
            musicFilter.cutoffFrequency = 22000f;
            musicFilter.lowpassResonanceQ = 1f;
            BindManager();
        }

        private void Start()
        {
            if (Instance != this) return;
            BindManager();
            EnsureMusicClip();
            // Warm outside interactions; synthesis and variant selection never consume Unity.Random.
            foreach (SfxType type in Enum.GetValues(typeof(SfxType))) GetBank(type);
        }

        private static AudioSource CreateSource(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            AudioSource source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.dopplerLevel = 0f;
            source.minDistance = 1f;
            source.maxDistance = 10f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.spatialBlend = 0f;
            source.reverbZoneMix = 0f;
            return source;
        }

        public void Play(SfxType type) => Play(type, 1f);
        public void Play(SfxType type, float pitch) => PlayInternal(type, Vector3.zero, false, pitch);
        public void PlayAt(SfxType type, Vector3 position) => PlayAt(type, position, 1f);
        public void PlayAt(SfxType type, Vector3 position, float pitch)
            => PlayInternal(type, position, !IsGlobalCue(type), pitch);

        private void PlayInternal(SfxType type, Vector3 position, bool spatial, float pitch)
        {
            if (!isActiveAndEnabled || voices == null || SafeVolume(volume) <= 0f) return;
            if (!IsValid(type) || !Finite(pitch) || (spatial && (!Finite(position.x) || !Finite(position.y) || !Finite(position.z)))) return;
            BindManager();
            bool world = spatial || AudioSynthesis.IsFoley(type);
            if (world && manager != null && !manager.IsPlaying) return;
            float now = Time.unscaledTime;
            bool outcome = IsOutcome(type);
            if (!outcome && type != SfxType.ButtonClick && now < outcomeUntil) return;
            if (lastPlayed.TryGetValue(type, out float previous) && now - previous < MinimumInterval(type)) return;

            int start = outcome ? VoiceCount - 1 : AudioSynthesis.IsFoley(type) ? 0 : FoleyVoiceCount;
            int end = outcome ? VoiceCount : start == 0 ? FoleyVoiceCount : VoiceCount - 1;
            int free = -1;
            int overlapping = 0;
            // Never steal an audible tail mid-wave. If the bounded pool is full, drop the new sound.
            for (int i = start; i < end; i++)
            {
                if (!voices[i].isPlaying) { if (free < 0) free = i; }
                else if (voiceTypes[i] == type) overlapping++;
            }
            if (free < 0 || overlapping >= (type == SfxType.ButtonClick || type == SfxType.Tick ? 1 : 2)) return;
            if (outcome)
            {
                for (int i = 0; i < VoiceCount - 1; i++)
                    if (voices[i].isPlaying) voiceFadeUntil[i] = now + VoiceRelease;
                outcomeUntil = now + 0.8f;
            }
            AudioSource voice = voices[free];
            voiceTypes[free] = type;
            worldVoices[free] = world;
            voiceFadeUntil[free] = 0f;
            voice.transform.position = spatial ? position : transform.position;
            voice.spatialBlend = spatial ? 0.9f : 0f;
            // Tonal cues are intentionally in C major / A minor, including the delivery bell.
            voice.pitch = AudioSynthesis.IsFoley(type) || type == SfxType.Tick ? Mathf.Clamp(pitch, 0.75f, 1.3f) : 1f;
            voice.volume = SafeVolume(volume) * CueGain(type);
            voice.priority = world ? 150 : 64;
            voice.clip = GetBank(type).Next();
            voice.Play();
            lastPlayed[type] = now;
            DuckFor(type, now);
        }

        public AudioSource CreateLoopSource(Transform owner, SfxType type)
        {
            if (!isActiveAndEnabled || owner == null || type != SfxType.Sizzle) return null;
            BindManager();
            AudioSource source = CreateSource("SizzleLoop", owner);
            source.transform.localPosition = Vector3.up * 0.65f;
            source.clip = GetBank(type).Next();
            source.loop = true;
            source.spatialBlend = 1f;
            source.priority = 180;
            source.volume = 0f;
            source.pitch = 1f;
            // Identical pans do not start correlated loops on the same sample.
            source.timeSamples = (++loopSequence * 17389) % source.clip.samples;
            loops.Add(new LoopVoice { source = source });
            return source;
        }

        // Level is the pan's contents, not gm.IsPlaying: pausing must not lose the contents state.
        public void SetLoopLevel(AudioSource source, float level)
        {
            for (int i = 0; i < loops.Count; i++)
            {
                LoopVoice loop = loops[i];
                if (loop.source != source) continue;
                loop.level = SafeVolume(level);
                if (loop.level <= 0f) StopLoop(loop);
                return;
            }
        }

        public void ReleaseLoopSource(AudioSource source)
        {
            for (int i = loops.Count - 1; i >= 0; i--)
            {
                if (loops[i].source != source) continue;
                StopLoop(loops[i]);
                if (source != null) Destroy(source.gameObject);
                loops.RemoveAt(i);
                return;
            }
        }

        private void BindManager()
        {
            if (manager == GameManager.Instance) return;
            UnbindManager();
            manager = GameManager.Instance;
            if (manager == null) return;
            manager.StateChanged += OnStateChanged;
            manager.DayStarting += OnDayStarting;
            OnStateChanged(manager.State);
        }

        private void UnbindManager()
        {
            if (manager == null) return;
            manager.StateChanged -= OnStateChanged;
            manager.DayStarting -= OnDayStarting;
            manager = null;
        }

        private void OnDayStarting()
        {
            foreach (LoopVoice loop in loops) { loop.level = 0f; StopLoop(loop); }
            foreach (AudioSource voice in voices) if (voice != null) voice.Stop();
            lastPlayed.Clear();
            outcomeUntil = duckUntil = 0f;
            duckTarget = 1f;
        }

        private void OnStateChanged(GameState state)
        {
            if (state == GameState.Playing) return;
            for (int i = 0; i < voices.Length; i++)
            {
                if (!worldVoices[i]) continue;
                if (state == GameState.Paused || state == GameState.Ready) voices[i].Stop();
                else if (voices[i].isPlaying) voiceFadeUntil[i] = Time.unscaledTime + VoiceRelease;
            }
            foreach (LoopVoice loop in loops)
            {
                if (state == GameState.Paused && loop.level > 0f && loop.source != null)
                {
                    if (loop.source.isPlaying) { loop.source.Pause(); loop.paused = true; }
                    loop.source.volume = 0f;
                    loop.gain = 0f;
                }
                else StopLoop(loop);
            }
        }

        private void Update()
        {
            if (Instance != this) return;
            BindManager();
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < voices.Length; i++)
            {
                float release = voiceFadeUntil[i] > 0f ? Mathf.Clamp01((voiceFadeUntil[i] - Time.unscaledTime) / VoiceRelease) : 1f;
                voices[i].volume = SafeVolume(volume) * CueGain(voiceTypes[i]) * release;
                if (release <= 0f) { voices[i].Stop(); voiceFadeUntil[i] = 0f; }
            }
            UpdateLoops(dt);
            UpdateMusic(dt);
        }

        private void UpdateLoops(float dt)
        {
            for (int i = loops.Count - 1; i >= 0; i--)
            {
                LoopVoice loop = loops[i];
                AudioSource source = loop.source;
                if (source == null) { loops.RemoveAt(i); continue; }
                if (loop.level <= 0f || !source.gameObject.activeInHierarchy) { StopLoop(loop); continue; }
                if (manager == null || (!manager.IsPlaying && manager.State != GameState.Paused)) { StopLoop(loop); continue; }
                if (!manager.IsPlaying) continue;
                if (loop.paused) { source.UnPause(); loop.paused = false; }
                if (!source.isPlaying) source.Play();
                loop.gain = Smooth(loop.gain, loop.level, 0.18f, dt);
                source.volume = SafeVolume(volume) * loop.gain * 0.55f;
            }
        }

        private static void StopLoop(LoopVoice loop)
        {
            if (loop.source != null) { loop.source.Stop(); loop.source.volume = 0f; }
            loop.gain = 0f;
            loop.paused = false;
        }

        private void EnsureMusicClip()
        {
            if (musicLoadAttempted) return;
            musicLoadAttempted = true;
            music.clip = Resources.Load<AudioClip>("Audio/Music/CafeLoop");
            if (music.clip == null)
                Debug.LogWarning("CafeLoop is missing from Resources/Audio/Music; music stays silent (no synthesized fallback).", this);
        }

        private void UpdateMusic(float dt)
        {
            if (music == null) return;
            music.pitch = 1f;
            if (manager == null) { music.volume = 0f; return; }
            EnsureMusicClip();
            bool ending = manager.IsGameOver;
            bool paused = manager.State == GameState.Paused;
            float target = ending ? 0f : paused ? 0.32f : manager.State == GameState.DayComplete ? 0.42f : 1f;
            if (manager.IsPlaying && manager.Timer != null && manager.Timer.TimeRemaining <= 20f) target *= 0.88f;
            musicGain = Smooth(musicGain, target, ending ? 0.28f : 0.65f, dt);
            if (Time.unscaledTime >= duckUntil) duckTarget = 1f;
            duckGain = Smooth(duckGain, duckTarget, duckTarget < duckGain ? 0.035f : 0.4f, dt);
            music.volume = SafeVolume(musicVolume) * musicGain * duckGain;
            musicFilter.cutoffFrequency = Smooth(musicFilter.cutoffFrequency, paused ? 1600f : 22000f, 0.2f, dt);
            if (!ending && music.clip != null && !music.isPlaying) music.Play();
            else if (ending && musicGain < 0.001f && music.isPlaying) music.Stop();
        }

        private void DuckFor(SfxType type, float now)
        {
            float level, hold;
            if (IsOutcome(type)) { level = 0.22f; hold = 1.3f; }
            else if (type == SfxType.DeliverSuccess || type == SfxType.DeliverFail || type == SfxType.OrderExpire)
                { level = 0.5f; hold = 0.45f; }
            else if (type == SfxType.Warning || type == SfxType.Burned) { level = 0.6f; hold = 0.3f; }
            else if (type == SfxType.Cooked || type == SfxType.Bell) { level = 0.76f; hold = 0.25f; }
            else return;
            duckTarget = now < duckUntil ? Mathf.Min(duckTarget, level) : level;
            duckUntil = Mathf.Max(duckUntil, now + hold);
        }

        private ClipBank GetBank(SfxType type)
        {
            if (banks.TryGetValue(type, out ClipBank bank)) return bank;
            bank = new ClipBank { random = new System.Random(9413 + (int)type * 701) };
            if (type == SfxType.Footstep || type == SfxType.PlatePickup || type == SfxType.PlatePlace)
            {
                bank.clips = Resources.LoadAll<AudioClip>("Audio/Sfx/" + type);
                Array.Sort(bank.clips, (a, b) => string.CompareOrdinal(a.name, b.name));
            }
            else
            {
                bank.clips = new AudioClip[4];
                for (int i = 0; i < bank.clips.Length; i++)
                {
                    bank.clips[i] = AudioSynthesis.Build(type, i);
                    generatedClips.Add(bank.clips[i]);
                }
            }
            banks.Add(type, bank);
            return bank;
        }

        private static bool IsValid(SfxType type) => (int)type >= 0 && type <= SfxType.Trash;
        private static bool IsOutcome(SfxType type) => type == SfxType.GameOver || type == SfxType.Victory || type == SfxType.DayComplete;
        private static bool IsGlobalCue(SfxType type) => IsOutcome(type) || type == SfxType.DeliverSuccess
            || type == SfxType.DeliverFail || type == SfxType.OrderExpire || type == SfxType.ButtonClick
            || type == SfxType.Tick || type == SfxType.Combo || type == SfxType.Bell;
        private static float MinimumInterval(SfxType type) => type == SfxType.Warning ? 0.5f
            : type == SfxType.Footstep ? 0.16f : type == SfxType.Chop ? 0.065f : 0.04f;
        private static float CueGain(SfxType type) => type == SfxType.Footstep ? 0.48f
            : type == SfxType.ButtonClick ? 0.55f : type == SfxType.Combo || type == SfxType.Bell ? 0.65f : 0.85f;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static float SafeVolume(float value) => Finite(value) ? Mathf.Clamp01(value) : 0f;
        private static float Smooth(float current, float target, float seconds, float dt)
            => Mathf.Lerp(current, target, 1f - Mathf.Exp(-Mathf.Max(0f, dt) / seconds));

        private void OnDisable()
        {
            UnbindManager();
            if (music != null) music.Stop();
            if (voices != null) foreach (AudioSource voice in voices) if (voice != null) voice.Stop();
            foreach (LoopVoice loop in loops) StopLoop(loop);
            musicGain = 0f;
        }

        private void OnDestroy()
        {
            UnbindManager();
            foreach (LoopVoice loop in loops)
                if (loop.source != null) { loop.source.Stop(); loop.source.clip = null; Destroy(loop.source.gameObject); }
            loops.Clear();
            if (voices != null) foreach (AudioSource voice in voices)
                if (voice != null) { voice.Stop(); voice.clip = null; Destroy(voice.gameObject); }
            if (music != null) { music.Stop(); music.clip = null; Destroy(music.gameObject); }
            // Only clips created by AudioSynthesis belong to us; never destroy imported CafeLoop.
            foreach (AudioClip clip in generatedClips) if (clip != null) Destroy(clip);
            generatedClips.Clear();
            banks.Clear();
            if (Instance == this) Instance = null;
        }
    }
}
