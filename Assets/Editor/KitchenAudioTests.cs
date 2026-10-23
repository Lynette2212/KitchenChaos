using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using KitchenChaos.Audio;
using KitchenChaos.Core;
using KitchenChaos.Data;
using KitchenChaos.Gameplay;
using Object = UnityEngine.Object;

namespace KitchenChaos.EditorTools
{
    public static partial class KitchenSmokeTest
    {
        private const BindingFlags AudioMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static void VerifyAudio()
        {
            AudioManager audio = AudioManager.Instance;
            Expect(audio != null, "audio manager exists");
            if (audio == null) return;
            float savedVolume = audio.volume, savedMusicVolume = audio.musicVolume;
            UnityEngine.Random.State savedRandom = UnityEngine.Random.state;
            try
            {
                NewRun();
                audio.volume = 0.8f;
                audio.musicVolume = 0.25f;
                AudioInvoke(audio, "UpdateMusic", 2f);
                VerifyAudioBanks(audio);
                VerifyColdAudioRandom(audio);
                VerifyAudioMix(audio);
                VerifyAudioDispatch(audio);
                VerifyStoveAudio(audio);
                VerifyBlockedFootsteps();
            }
            finally
            {
                try { NewRun(); }
                finally
                {
                    audio.volume = savedVolume;
                    audio.musicVolume = savedMusicVolume;
                    UnityEngine.Random.state = savedRandom;
                    AudioInvoke(audio, "UpdateLoops", 2f);
                    AudioInvoke(audio, "UpdateMusic", 2f);
                }
            }
        }

        private static T AudioField<T>(object target, string name)
        {
            FieldInfo field = target.GetType().GetField(name, AudioMembers);
            if (field == null) throw new MissingFieldException(target.GetType().FullName, name);
            return (T)field.GetValue(target);
        }

        private static object AudioInvoke(object target, string name, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(name, AudioMembers);
            if (method == null) throw new MissingMethodException(target.GetType().FullName, name);
            return method.Invoke(target, arguments);
        }

        private static AudioClip[] AudioBankClips(AudioManager audio, SfxType type)
            => AudioField<AudioClip[]>(AudioInvoke(audio, "GetBank", type), "clips");

        private static bool IsImportedAudio(SfxType type)
            => type == SfxType.Footstep || type == SfxType.PlatePickup || type == SfxType.PlatePlace;

        private static void VerifyAudioBanks(AudioManager audio)
        {
            var imported = new HashSet<AudioClip>();
            var synthesized = new HashSet<AudioClip>();
            UnityEngine.Random.State before = UnityEngine.Random.state;
            var types = (SfxType[])Enum.GetValues(typeof(SfxType));
            Expect(types.Length == 23, "audio has three imported and twenty synthesized sound types");
            foreach (SfxType type in types)
            {
                object bank = AudioInvoke(audio, "GetBank", type);
                AudioClip[] clips = AudioField<AudioClip[]>(bank, "clips");
                Expect(ReferenceEquals(bank, AudioInvoke(audio, "GetBank", type)), type + " bank is cached");
                Expect(clips != null && clips.Length == 4 && new HashSet<AudioClip>(clips).Count == 4
                    && Array.TrueForAll(clips, clip => clip != null), type + " has four distinct valid variants");
                if (clips == null || clips.Length != 4 || Array.Exists(clips, clip => clip == null)) continue;

                if (IsImportedAudio(type))
                {
                    AudioClip[] resources = Resources.LoadAll<AudioClip>("Audio/Sfx/" + type);
                    Expect(resources.Length == 4 && new HashSet<AudioClip>(resources).SetEquals(clips),
                        type + " bank references exactly the four imported Resources WAVs (not clones/fallbacks)");
                    foreach (AudioClip clip in resources)
                    {
                        string path = AssetDatabase.GetAssetPath(clip);
                        Expect(path.StartsWith("Assets/Resources/Audio/Sfx/" + type + "/", StringComparison.Ordinal)
                            && path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase)
                            && AssetImporter.GetAtPath(path) is AudioImporter, type + " variant is a real imported WAV: " + clip.name);
                        imported.Add(clip);
                    }
                }
                else
                {
                    for (int variant = 0; variant < clips.Length; variant++)
                    {
                        AudioClip clip = clips[variant];
                        synthesized.Add(clip);
                        Expect(string.IsNullOrEmpty(AssetDatabase.GetAssetPath(clip)), type + "/" + variant + " is runtime synthesized, not an imported asset");
                        VerifyAudioPcm(clip, type, variant);
                    }
                }

                int previousIndex = AudioField<int>(bank, "previous");
                AudioClip previous = previousIndex >= 0 && previousIndex < clips.Length ? clips[previousIndex] : null;
                bool membersOnly = true, noRepeats = true;
                for (int draw = 0; draw < 32; draw++)
                {
                    AudioClip next = (AudioClip)AudioInvoke(bank, "Next");
                    membersOnly &= next != null && Array.IndexOf(clips, next) >= 0;
                    noRepeats &= next != previous;
                    previous = next;
                }
                Expect(membersOnly && noRepeats, type + " Next selects bank references without consecutive repeats over 32 draws");
            }
            Expect(UnityEngine.Random.state.Equals(before), "cached banks and variant selection preserve UnityEngine.Random state");
            List<AudioClip> generated = AudioField<List<AudioClip>>(audio, "generatedClips");
            Expect(imported.Count == 12 && synthesized.Count == 80, "live banks contain twelve imported and eighty synthesized clips");
            Expect(generated.Count == 80 && new HashSet<AudioClip>(generated).Count == 80
                && synthesized.SetEquals(generated), "generatedClips owns exactly the eighty synthesized bank clips, without duplicates");
            Expect(!generated.Exists(clip => imported.Contains(clip) || clip == audio.MusicSource.clip
                || !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(clip))), "generatedClips never owns imported Foley or CafeLoop");
        }

        private static void VerifyAudioPcm(AudioClip clip, SfxType type, int variant)
        {
            string label = type + "/" + variant;
            bool mono = type == SfxType.Pickup || type == SfxType.Drop || type == SfxType.Chop
                || type == SfxType.Sizzle || type == SfxType.Dash || type == SfxType.Trash || type == SfxType.Burned;
            bool format = clip.frequency == 44100 && clip.channels == (mono ? 1 : 2) && clip.samples > 0 && clip.length <= 5f;
            Expect(format, label + " has bounded 44.1kHz PCM with the expected mono/stereo layout");
            if (!format) return;
            float[] pcm = new float[clip.samples * clip.channels];
            bool readable = clip.GetData(pcm, 0);
            Expect(readable, label + " exposes synthesized PCM without an audio device");
            if (!readable) return;
            bool finite = true;
            float peak = 0f;
            double energy = 0d;
            foreach (float sample in pcm)
            {
                if (float.IsNaN(sample) || float.IsInfinity(sample)) { finite = false; continue; }
                peak = Mathf.Max(peak, Mathf.Abs(sample));
                energy += (double)sample * sample;
            }
            Expect(finite && Math.Sqrt(energy / pcm.Length) > 0.0001d && peak <= 0.61f,
                label + " PCM is finite, non-silent (RMS > 0.0001), peak <= 0.61; actual peak=" + peak);
            if (type == SfxType.Sizzle)
            {
                int window = Mathf.Min(4410, pcm.Length / 3);
                Expect(Mathf.Abs(clip.length - 4.6f) < 0.002f, label + " is a 4.6 second sizzle loop");
                Expect(AudioRms(pcm, 0, window) > 0.001d
                    && AudioRms(pcm, pcm.Length / 2, window) > 0.001d
                    && AudioRms(pcm, pcm.Length - window, window) > 0.001d,
                    label + " sizzle has non-silent head, middle and tail windows, not a faded one-shot");
            }
            else
            {
                bool quietEdges = true;
                int edgeSamples = Mathf.Min(8, clip.samples) * clip.channels;
                for (int i = 0; i < edgeSamples; i++)
                    quietEdges &= Mathf.Abs(pcm[i]) <= 0.001f && Mathf.Abs(pcm[pcm.Length - 1 - i]) <= 0.001f;
                Expect(quietEdges, label + " non-loop PCM starts and ends near zero on every channel");
            }
        }

        private static double AudioRms(float[] pcm, int start, int count)
        {
            double energy = 0d;
            for (int i = start; i < start + count; i++) energy += (double)pcm[i] * pcm[i];
            return Math.Sqrt(energy / count);
        }

        private static void VerifyColdAudioRandom(AudioManager live)
        {
            // Start already warmed the live banks. An inactive component exercises cold GetBank
            // without Awake/Start, duplicate-singleton replacement, or production API changes.
            var owner = new GameObject("AudioSmokeColdBanks");
            owner.SetActive(false);
            AudioManager cold = null;
            UnityEngine.Random.State before = UnityEngine.Random.state;
            try
            {
                cold = owner.AddComponent<AudioManager>();
                Expect(AudioManager.Instance == live && cold.MusicSource == null, "inactive cold-bank fixture does not replace the live audio manager");
                foreach (SfxType type in Enum.GetValues(typeof(SfxType)))
                {
                    object bank = AudioInvoke(cold, "GetBank", type);
                    AudioInvoke(bank, "Next");
                }
                Expect(UnityEngine.Random.state.Equals(before), "cold GetBank resource loading, synthesis and Next preserve UnityEngine.Random state");
                Expect(AudioField<List<AudioClip>>(cold, "generatedClips").Count == 80, "cold initialization owns only eighty synthesized clips");
            }
            finally
            {
                UnityEngine.Random.state = before;
                // Never-active components need explicit cleanup; never destroy imported clips.
                if (cold != null)
                {
                    List<AudioClip> generated = AudioField<List<AudioClip>>(cold, "generatedClips");
                    foreach (AudioClip clip in generated)
                        if (clip != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(clip))) Object.Destroy(clip);
                    generated.Clear();
                }
                Object.Destroy(owner);
            }
        }

        private static object AudioLoop(AudioManager audio, AudioSource source)
        {
            foreach (object loop in AudioField<IList>(audio, "loops"))
                if (AudioField<AudioSource>(loop, "source") == source) return loop;
            return null;
        }

        private static float AudioLoopLevel(AudioManager audio, AudioSource source)
        {
            object loop = source != null ? AudioLoop(audio, source) : null;
            return loop != null ? AudioField<float>(loop, "level") : float.NaN;
        }

        private static bool AudioLoopIdle(AudioManager audio, AudioSource source)
        {
            object loop = source != null ? AudioLoop(audio, source) : null;
            return loop != null && AudioField<float>(loop, "level") == 0f && AudioField<float>(loop, "gain") == 0f
                && !AudioField<bool>(loop, "paused") && source.volume == 0f;
        }

        private static void VerifyAudioMix(AudioManager audio)
        {
            AudioSource music = audio.MusicSource;
            AudioClip cafe = Resources.Load<AudioClip>("Audio/Music/CafeLoop");
            Expect(music != null && cafe != null && music.clip == cafe
                && AssetDatabase.GetAssetPath(cafe) == "Assets/Resources/Audio/Music/CafeLoop.wav", "BGM references the real CafeLoop WAV, without a synthesized fallback");
            Expect(cafe != null && cafe.channels == 2 && Mathf.Abs(cafe.length - 55.385f) < 0.05f, "CafeLoop is stereo and approximately 55.385 seconds long");
            Expect(music != null && music.loop && music.pitch == 1f && music.spatialBlend == 0f && !music.playOnAwake, "BGM is an explicitly started 2D loop at fixed pitch one");
            if (music == null) return;
            var unique = new HashSet<AudioSource>();
            bool cleanVoices = audio.SfxSources != null && audio.SfxSources.Count == 13;
            if (audio.SfxSources != null)
                foreach (AudioSource source in audio.SfxSources)
                    cleanVoices &= source != null && unique.Add(source) && source != music && !source.loop && !source.playOnAwake
                        && source.GetComponent<AudioLowPassFilter>() == null;
            Expect(cleanVoices, "thirteen distinct transient SFX sources are separate from music and have no music low-pass filter");
            AudioLowPassFilter filter = music.GetComponent<AudioLowPassFilter>();
            Expect(filter != null && filter.enabled, "music owns its enabled low-pass filter");
            if (filter == null) return;

            int originalLoops = AudioField<IList>(audio, "loops").Count;
            var owner = new GameObject("AudioSmokeLoopOwner");
            AudioSource probe = null;
            try
            {
                Expect(audio.CreateLoopSource(null, SfxType.Sizzle) == null
                    && audio.CreateLoopSource(owner.transform, SfxType.Chop) == null
                    && AudioField<IList>(audio, "loops").Count == originalLoops, "loop creation rejects null owners and non-sizzle types without registering voices");
                probe = audio.CreateLoopSource(owner.transform, SfxType.Sizzle);
                Expect(probe != null && AudioField<IList>(audio, "loops").Count == originalLoops + 1, "loop creation registers an independently owned source");
                if (probe == null) return;
                Expect(probe.transform.parent == owner.transform && probe.loop && probe.spatialBlend == 1f
                    && probe.pitch == 1f && probe.volume == 0f && !probe.playOnAwake && !unique.Contains(probe)
                    && probe != music && Array.IndexOf(AudioBankClips(audio, SfxType.Sizzle), probe.clip) >= 0,
                    "owned sizzle source is a silent, spatial, fixed-pitch loop referencing its bank");
                foreach (float invalid in new[] { -1f, float.NaN, float.PositiveInfinity })
                {
                    audio.SetLoopLevel(probe, invalid);
                    Expect(AudioLoopIdle(audio, probe), "invalid loop level becomes silent zero: " + invalid);
                }
                audio.SetLoopLevel(probe, 2f);
                Expect(AudioLoopLevel(audio, probe) == 1f, "loop contents level clamps at one");
                audio.musicVolume = 0f;
                AudioInvoke(audio, "UpdateMusic", 2f);
                AudioInvoke(audio, "UpdateLoops", 2f);
                Expect(music.volume == 0f && probe.volume > 0f, "musicVolume zero silences BGM without muting SFX loops");
                audio.musicVolume = 0.25f;
                audio.volume = 0f;
                AudioInvoke(audio, "UpdateMusic", 2f);
                AudioInvoke(audio, "UpdateLoops", 2f);
                Expect(probe.volume == 0f && AudioLoopLevel(audio, probe) == 1f && music.volume > 0f,
                    "SFX volume zero silences loops, retains contents and leaves music independent");
                audio.volume = 0.8f;
                AudioInvoke(audio, "UpdateLoops", 2f);
                float playingMusic = music.volume;
                gm.TogglePause();
                Expect(probe.volume == 0f && AudioLoopLevel(audio, probe) == 1f, "pause immediately mutes an owned loop without discarding its contents");
                AudioInvoke(audio, "UpdateLoops", 2f);
                music.pitch = 0.8f;
                AudioInvoke(audio, "UpdateMusic", 2f);
                Expect(probe.volume == 0f && music.volume > 0f && music.volume < playingMusic
                    && Mathf.Abs(filter.cutoffFrequency - 1600f) < 10f && music.pitch == 1f,
                    "unscaled pause update lowers BGM gain and cutoff, mutes loops and restores pitch one");
                bool sfxUnfiltered = probe.GetComponent<AudioLowPassFilter>() == null;
                foreach (AudioSource source in audio.SfxSources)
                    sfxUnfiltered &= source.GetComponent<AudioLowPassFilter>() == null;
                Expect(sfxUnfiltered && filter.gameObject == music.gameObject,
                    "pause low-pass belongs only to music, not transient SFX or the spatial loop");
                gm.TogglePause();
                AudioInvoke(audio, "UpdateMusic", 2f);
                AudioInvoke(audio, "UpdateLoops", 2f);
                Expect(filter.cutoffFrequency > 21990f && music.pitch == 1f && probe.volume > 0f
                    && AudioLoopLevel(audio, probe) == 1f, "resume restores music cutoff and retained loop contents");
                audio.ReleaseLoopSource(probe);
                Expect(AudioLoop(audio, probe) == null && probe.volume == 0f
                    && AudioField<IList>(audio, "loops").Count == originalLoops, "release immediately stops and unregisters only the owned loop");
                audio.ReleaseLoopSource(probe);
                Expect(AudioField<IList>(audio, "loops").Count == originalLoops, "releasing a loop twice is harmless");
            }
            finally
            {
                if (probe != null) audio.ReleaseLoopSource(probe);
                owner.SetActive(false);
                Object.Destroy(owner);
                audio.volume = 0.8f;
                audio.musicVolume = 0.25f;
                NewRun();
            }
        }

        private static void ClearAudioTestVoices(AudioManager audio)
        {
            // Stop explicitly: synchronous batch tests cannot wait for hardware playback/tails.
            foreach (AudioSource source in audio.SfxSources) { source.Stop(); source.clip = null; }
        }

        private static AudioSource ExpectAudioVoice(AudioManager audio, SfxType type, string label)
        {
            AudioClip[] clips = AudioBankClips(audio, type);
            AudioSource found = null;
            int assigned = 0;
            foreach (AudioSource source in audio.SfxSources)
            {
                if (source.clip == null) continue;
                assigned++;
                if (Array.IndexOf(clips, source.clip) >= 0) found = source;
            }
            Expect(assigned == 1 && found != null, label + " assigns exactly one source a real bank clip");
            return found;
        }

        private static void VerifyAudioDispatch(AudioManager audio)
        {
            NewRun();
            ClearAudioTestVoices(audio);
            Vector3 position = player.transform.position + Vector3.right;
            audio.PlayAt(SfxType.Footstep, position, 5f);
            AudioSource voice = ExpectAudioVoice(audio, SfxType.Footstep, "world footstep");
            Expect(voice != null && voice.spatialBlend > 0f && voice.transform.position == position && voice.pitch == 1.3f,
                "PlayAt spatializes imported footsteps and clamps Foley pitch");
            ClearAudioTestVoices(audio);
            audio.PlayAt(SfxType.Cooked, position, 0.8f);
            voice = ExpectAudioVoice(audio, SfxType.Cooked, "cooked cue");
            Expect(voice != null && voice.pitch == 1f, "tonal cooked cue ignores caller detuning");
            ClearAudioTestVoices(audio);
            gm.TogglePause();
            audio.Play(SfxType.ButtonClick, 0.8f);
            voice = ExpectAudioVoice(audio, SfxType.ButtonClick, "paused UI click");
            Expect(voice != null && voice.spatialBlend == 0f && voice.pitch == 1f, "Play keeps UI cues 2D, in tune and available while paused");
            ClearAudioTestVoices(audio);
            audio.PlayAt(SfxType.Pickup, position);
            Expect(Array.TrueForAll(AudioField<AudioSource[]>(audio, "voices"), source => source.clip == null), "pause rejects world Foley dispatch");
            NewRun();
            audio.Play((SfxType)(-1));
            audio.Play(SfxType.ButtonClick, float.NaN);
            audio.PlayAt(SfxType.Drop, new Vector3(float.PositiveInfinity, 0f, 0f));
            audio.volume = 0f;
            audio.Play(SfxType.ButtonClick);
            audio.PlayAt(SfxType.Pickup, position);
            Expect(Array.TrueForAll(AudioField<AudioSource[]>(audio, "voices"), source => source.clip == null), "invalid requests and zero SFX volume assign no transient clips");
            audio.volume = 0.8f;

            NewRun();
            ClearAudioTestVoices(audio);
            Expect(UseStation(plates) && player.HasPlate, "audio integration takes a real plate through the crosshair");
            ExpectAudioVoice(audio, SfxType.PlatePickup, "plate counter pickup");
            ClearAudioTestVoices(audio);
            Expect(UseStation(prep) && prep.HasPlate && !player.HandsBusy, "audio integration stores the real plate on prep");
            ExpectAudioVoice(audio, SfxType.PlatePlace, "prep plate placement");
            NewRun();
        }

        private static void VerifyStoveAudio(AudioManager audio)
        {
            NewRun();
            StoveCounter other = null;
            foreach (StoveCounter candidate in Object.FindObjectsByType<StoveCounter>(FindObjectsSortMode.None))
                if (candidate != stove) { other = candidate; break; }
            Expect(other != null, "audio integration has a second real stove for simultaneous independent loops");
            if (other == null) return;
            Expect(UseStation(Container(IngredientType.MeatPatty)) && UseStation(stove), "placing real raw meat arms the first stove loop synchronously");
            Expect(UseStation(Container(IngredientType.MeatPatty)) && UseStation(other), "placing real raw meat arms the second stove loop synchronously");
            AudioSource first = stove.SizzleSource, second = other.SizzleSource;
            Expect(first != null && second != null && first != second && first.transform.parent == stove.transform
                && second.transform.parent == other.transform && first.loop && second.loop
                && first.spatialBlend == 1f && second.spatialBlend == 1f && first.pitch == 1f && second.pitch == 1f,
                "two real stoves own distinct spatial sizzle loops at pitch one");
            if (first == null || second == null) return;
            AudioClip[] sizzles = AudioBankClips(audio, SfxType.Sizzle);
            Expect(Array.IndexOf(sizzles, first.clip) >= 0 && Array.IndexOf(sizzles, second.clip) >= 0
                && Array.IndexOf(AudioField<AudioSource[]>(audio, "voices"), first) < 0
                && Array.IndexOf(AudioField<AudioSource[]>(audio, "voices"), second) < 0
                && first != audio.MusicSource && second != audio.MusicSource,
                "real stove loops use sizzle bank references, never transient voices or the music source");
            Expect(AudioLoopLevel(audio, first) == 1f && AudioLoopLevel(audio, second) == 1f, "both raw pans retain full contents level");
            AudioInvoke(audio, "UpdateLoops", 1f);
            Expect(first.volume > 0f && second.volume > 0f, "both pan gains rise through the real loop update");
            gm.TogglePause();
            Expect(first.volume == 0f && second.volume == 0f && AudioLoopLevel(audio, first) == 1f
                && AudioLoopLevel(audio, second) == 1f, "pause immediately mutes both real stoves while preserving contents levels");
            stove.AdvanceProcessing(100f);
            AudioInvoke(stove, "SyncSizzle");
            AudioInvoke(other, "SyncSizzle");
            AudioInvoke(audio, "UpdateLoops", 1f);
            Expect(stove.PlacedIngredient != null && stove.PlacedIngredient.state == IngredientState.Raw
                && AudioLoopLevel(audio, first) == 1f && AudioLoopLevel(audio, second) == 1f
                && first.volume == 0f && second.volume == 0f, "paused processing and SyncSizzle cannot erase contents or unmute pans");
            gm.TogglePause();
            AudioInvoke(audio, "UpdateLoops", 1f);
            Expect(first.volume > 0f && second.volume > 0f, "resuming real pans restores their configured gain without playback-state assertions");
            float cookTime = IngredientCatalog.Get(IngredientType.MeatPatty).cookTime / gm.CookingSpeedMultiplier;
            stove.AdvanceProcessing(cookTime + 0.01f);
            Expect(stove.IsCooked && Mathf.Abs(AudioLoopLevel(audio, first) - 0.62f) < 0.0001f
                && AudioLoopLevel(audio, second) == 1f, "cooking synchronously lowers only that pan's level to 0.62");
            Expect(UseStation(plates) && UseStation(stove) && player.Plate != null && player.Plate.Count == 1,
                "cooked meat can be taken directly onto a real plate");
            Expect(stove.PlacedIngredient == null && AudioLoopIdle(audio, first)
                && AudioLoopLevel(audio, second) == 1f, "plating clears one loop immediately without changing the other pan");
            Expect(UseStation(prep), "store cooked plate to free hands for raw retrieval");
            gm.TogglePause();
            AudioInvoke(stove, "SyncSizzle");
            gm.TogglePause();
            AudioInvoke(stove, "SyncSizzle");
            AudioInvoke(audio, "UpdateLoops", 1f);
            Expect(AudioLoopIdle(audio, first) && AudioLoopLevel(audio, second) == 1f && second.volume > 0f,
                "empty stove pause/resume cannot revive old contents, gain or paused state");

            Expect(UseStation(Container(IngredientType.MeatPatty)) && UseStation(stove), "refill the emptied stove with fresh raw meat");
            Expect(stove.SizzleSource == first && AudioLoopLevel(audio, first) == 1f, "refill reuses its own loop and restores raw level");
            Expect(UseStation(stove) && player.HasItem && AudioLoopIdle(audio, first), "empty-handed retrieval also clears the loop synchronously");
            Expect(UseStation(stove) && AudioLoopLevel(audio, first) == 1f, "putting raw meat back rearms its contents level");
            stove.ResetStation();
            Expect(stove.PlacedIngredient == null && AudioLoopIdle(audio, first), "ResetStation clears a previously active raw loop");
            other.AdvanceProcessing(cookTime + 0.01f);
            Expect(other.IsCooked && Mathf.Abs(AudioLoopLevel(audio, second) - 0.62f) < 0.0001f, "second pan independently reaches cooked level");
            other.AdvanceProcessing(other.SecondsUntilBurn + 0.01f);
            Expect(other.IsBurned && AudioLoopIdle(audio, second), "burning synchronously clears sizzle level, gain and volume");
            gm.TogglePause();
            gm.TogglePause();
            AudioInvoke(stove, "SyncSizzle");
            AudioInvoke(other, "SyncSizzle");
            AudioInvoke(audio, "UpdateLoops", 1f);
            Expect(AudioLoopIdle(audio, first) && AudioLoopIdle(audio, second), "reset and burned pans stay silent after pause/resume");
            NewRun();
            Expect(AudioLoopIdle(audio, first) && AudioLoopIdle(audio, second), "new run leaves both retained stove sources with zero contents level");
        }

        private static void VerifyBlockedFootsteps()
        {
            NewRun();
            CharacterController controller = player.GetComponent<CharacterController>();
            BoxCollider obstacle = prep.GetComponent<BoxCollider>();
            Expect(controller != null && controller.enabled && obstacle != null && obstacle.enabled && !obstacle.isTrigger,
                "footstep regression uses the real CharacterController and solid prep counter");
            if (controller == null || obstacle == null) return;
            float side = Mathf.Sign(obstacle.bounds.center.x);
            Vector3 toward = Vector3.right * side;
            Vector3 approach = obstacle.bounds.center - toward * (obstacle.bounds.extents.x + controller.radius + 0.65f);
            approach.y = 0.05f;
            MovePlayer(approach);
            player.ViewCamera.GetComponent<CameraFollow>().SetViewAngles(side > 0f ? 90f : 270f, 0f);
            // Settle vertical CC depenetration before any distance baseline; no keyboard mocks.
            for (int i = 0; i < 8; i++) player.AdvanceMovement(Vector2.zero, false, 0.02f);
            float freeBefore = AudioField<float>(player, "footstepDistance");
            Vector3 freePosition = player.transform.position;
            player.AdvanceMovement(Vector2.up, false, 0.02f);
            float travel = Vector3.ProjectOnPlane(player.transform.position - freePosition, Vector3.up).magnitude;
            Expect(controller.isGrounded && travel > 0.02f
                && Mathf.Abs(AudioField<float>(player, "footstepDistance") - freeBefore - travel) < 0.002f,
                "positive control: grounded free walking accumulates actual horizontal displacement");
            for (int i = 0; i < 32; i++) player.AdvanceMovement(Vector2.up, false, 0.02f);
            RaycastHit hit;
            Expect(controller.isGrounded && (controller.collisionFlags & CollisionFlags.Sides) != 0
                && obstacle.Raycast(new Ray(controller.bounds.center, toward), out hit, controller.radius + controller.skinWidth + 0.15f),
                "player is stably grounded against the actual counter, not merely stationary in empty space");
            float blockedBefore = AudioField<float>(player, "footstepDistance");
            bool distanceUnchanged = true, physicallyBlocked = true;
            // Check every step: a final-only comparison could hide stride modulo wraparound.
            for (int i = 0; i < 24; i++)
            {
                Vector3 before = player.transform.position;
                player.AdvanceMovement(Vector2.up, false, 0.02f);
                physicallyBlocked &= controller.isGrounded && (controller.collisionFlags & CollisionFlags.Sides) != 0
                    && Vector3.ProjectOnPlane(player.transform.position - before, Vector3.up).magnitude < 0.001f;
                distanceUnchanged &= Mathf.Abs(AudioField<float>(player, "footstepDistance") - blockedBefore) < 0.0001f;
            }
            Expect(physicallyBlocked && distanceUnchanged, "24 grounded moves into a real blocker never grow footstepDistance after CC stabilization");
        }
    }
}
