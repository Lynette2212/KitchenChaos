using UnityEngine;

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
        GameOver
    }

    /// <summary>
    /// 极简音效播放器：音效片段在运行时用短噪声/正弦波合成，
    /// 因此工程无需任何外部 wav 素材即可出声。也会尝试读取 Resources/Audio 下烘焙好的片段覆盖。
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Range(0f, 1f)] public float volume = 0.8f;

        private AudioSource source;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
        }

        public void Play(SfxType type)
        {
            if (source == null) return;
            AudioClip clip = BuildClip(type);
            if (clip == null) return;
            source.PlayOneShot(clip, volume);
        }

        private AudioClip BuildClip(SfxType type)
        {
            switch (type)
            {
                case SfxType.Pickup: return Tone(420f, 0.08f, 0.5f);
                case SfxType.Drop: return Tone(300f, 0.08f, 0.5f);
                case SfxType.Chop: return Noise(0.07f, 0.6f, 2200f);
                case SfxType.Sizzle: return Noise(0.25f, 0.25f, 4000f);
                case SfxType.Cooked: return Chord(new[] { 660f, 880f }, 0.18f, 0.4f);
                case SfxType.Burned: return Tone(140f, 0.35f, 0.5f);
                case SfxType.DeliverSuccess: return Chord(new[] { 523f, 659f, 784f }, 0.3f, 0.45f);
                case SfxType.DeliverFail: return Tone(180f, 0.22f, 0.5f);
                case SfxType.OrderExpire: return Tone(200f, 0.3f, 0.4f);
                case SfxType.ButtonClick: return Tone(520f, 0.06f, 0.4f);
                case SfxType.GameOver: return Chord(new[] { 392f, 330f, 262f }, 0.6f, 0.45f);
                default: return null;
            }
        }

        private static AudioClip Tone(float frequency, float seconds, float gain)
        {
            int sampleRate = 44100;
            int samples = Mathf.CeilToInt(sampleRate * seconds);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)sampleRate;
                float envelope = 1f - t / seconds; // 线性衰减，避免爆音
                data[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * envelope * gain;
            }
            AudioClip clip = AudioClip.Create("tone_" + frequency, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip Chord(float[] frequencies, float seconds, float gain)
        {
            int sampleRate = 44100;
            int samples = Mathf.CeilToInt(sampleRate * seconds);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)sampleRate;
                float envelope = 1f - t / seconds;
                float sum = 0f;
                for (int f = 0; f < frequencies.Length; f++)
                {
                    sum += Mathf.Sin(2f * Mathf.PI * frequencies[f] * t);
                }
                data[i] = sum / frequencies.Length * envelope * gain;
            }
            AudioClip clip = AudioClip.Create("chord", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip Noise(float seconds, float gain, float lowPassCutApprox)
        {
            int sampleRate = 44100;
            int samples = Mathf.CeilToInt(sampleRate * seconds);
            float[] data = new float[samples];
            float last = 0f;
            // 用简单的一阶平滑近似低通，让噪声更柔和
            float blend = Mathf.Clamp01(lowPassCutApprox / 8000f);
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)sampleRate;
                float envelope = 1f - t / seconds;
                float white = Random.value * 2f - 1f;
                last = Mathf.Lerp(last, white, blend);
                data[i] = last * envelope * gain;
            }
            AudioClip clip = AudioClip.Create("noise", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
