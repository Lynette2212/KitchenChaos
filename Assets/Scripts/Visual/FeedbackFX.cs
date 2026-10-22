using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace KitchenChaos.Visual
{
    // Pooled, collider-free juice: particles, elastic punches, camera shake and world popups.
    public class FeedbackFX : MonoBehaviour
    {
        public const int PoolSize = 192;
        public static FeedbackFX Instance { get; private set; }

        public struct PopupItem
        {
            public Vector3 worldPos;
            public string text;
            public Color color;
            public bool big;
            public float age;
            public float life;
        }

        private sealed class Particle
        {
            public Transform transform;
            public MeshRenderer renderer;
            public MeshFilter filter;
            public Vector3 velocity;
            public Vector3 spin;
            public float gravity, drag, size, life, maxLife;
            public bool grow, active;
        }

        private sealed class PunchItem
        {
            public Transform target;
            public Vector3 baseScale;
            public float amount, duration, elapsed;
        }

        private readonly Particle[] pool = new Particle[PoolSize];
        private readonly List<PunchItem> punches = new List<PunchItem>();
        private readonly List<PopupItem> popups = new List<PopupItem>();
        private MaterialPropertyBlock block;
        private Material material;
        private Mesh cubeMesh;
        private Mesh sphereMesh;
        private int cursor;
        private float shakeStrength, shakeDuration, shakeRemaining, shakeSeed;

        public static Vector3 ShakeOffset { get; private set; }
        public IReadOnlyList<PopupItem> Popups => popups;
        public int ActiveParticles { get; private set; }
        public int ActivePunches => punches.Count;
        public float ShakeRemaining => shakeRemaining;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            block = new MaterialPropertyBlock();
            material = new Material(VisualFactory.LitShader) { name = "FeedbackParticle", hideFlags = HideFlags.DontSave };
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.15f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.15f);
            GameObject template = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cubeMesh = template.GetComponent<MeshFilter>().sharedMesh;
            DestroyImmediate(template);
            template = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphereMesh = template.GetComponent<MeshFilter>().sharedMesh;
            DestroyImmediate(template);
            for (int i = 0; i < PoolSize; i++)
            {
                GameObject go = new GameObject("FX");
                go.transform.SetParent(transform, false);
                MeshFilter filter = go.AddComponent<MeshFilter>();
                filter.sharedMesh = cubeMesh;
                MeshRenderer renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                go.SetActive(false);
                pool[i] = new Particle { transform = go.transform, renderer = renderer, filter = filter };
            }
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            RestorePunches();
            Instance = null;
            ShakeOffset = Vector3.zero;
            if (material != null) Destroy(material);
        }

        public static void Burst(Vector3 position, Color color, int count, float speed = 3f, float size = 0.12f, float gravity = 9f, float life = 0.55f)
        {
            if (Instance == null) return;
            for (int i = 0; i < count; i++)
            {
                Vector3 dir = Random.onUnitSphere;
                dir.y = Mathf.Abs(dir.y) * 1.2f + 0.35f;
                Color tint = Color.Lerp(color, Color.white, Random.Range(0f, 0.25f));
                Instance.Spawn(position, dir.normalized * speed * Random.Range(0.55f, 1.15f), tint,
                    size * Random.Range(0.7f, 1.3f), gravity, 1.5f, life * Random.Range(0.75f, 1.2f), false);
            }
        }

        public static void Confetti(Vector3 position, int count)
        {
            if (Instance == null) return;
            Color[] palette = { new Color(1f, 0.69f, 0.39f), new Color(0.49f, 0.84f, 0.73f), new Color(1f, 0.85f, 0.3f), new Color(1f, 0.5f, 0.47f), new Color(0.55f, 0.7f, 1f) };
            for (int i = 0; i < count; i++)
            {
                Vector2 flat = Random.insideUnitCircle * 2.2f;
                Instance.Spawn(position, new Vector3(flat.x, Random.Range(4f, 6.5f), flat.y), palette[i % palette.Length],
                    Random.Range(0.08f, 0.15f), 9f, 2.2f, Random.Range(0.8f, 1.2f), false);
            }
        }

        public static void Puff(Vector3 position, Color color, int count = 1, float size = 0.22f, float rise = 0.9f, float life = 0.9f)
        {
            if (Instance == null) return;
            for (int i = 0; i < count; i++)
            {
                Vector2 jitter = Random.insideUnitCircle * 0.25f;
                Vector3 velocity = new Vector3(jitter.x, rise * Random.Range(0.7f, 1.2f), jitter.y);
                Instance.Spawn(position + new Vector3(jitter.x, 0f, jitter.y) * 0.4f, velocity, color,
                    size * Random.Range(0.8f, 1.2f), 0f, 1.2f, life * Random.Range(0.8f, 1.2f), true);
            }
        }

        public static void Punch(Transform target, float amount = 0.3f, float duration = 0.35f)
        {
            if (Instance == null || target == null) return;
            foreach (PunchItem item in Instance.punches)
            {
                if (item.target != target) continue;
                item.amount = Mathf.Max(item.amount * 0.5f, amount);
                item.duration = duration;
                item.elapsed = 0f;
                return;
            }
            Instance.punches.Add(new PunchItem { target = target, baseScale = target.localScale, amount = amount, duration = Mathf.Max(0.05f, duration) });
        }

        public static void Shake(float strength, float duration)
        {
            if (Instance == null) return;
            if (strength >= Instance.shakeStrength * (Instance.shakeRemaining / Mathf.Max(0.01f, Instance.shakeDuration)))
            {
                Instance.shakeStrength = strength;
                Instance.shakeDuration = Instance.shakeRemaining = Mathf.Max(0.01f, duration);
                Instance.shakeSeed = Random.Range(0f, 100f);
            }
        }

        public static void Popup(Vector3 worldPos, string text, Color color, bool big = false)
        {
            if (Instance == null || string.IsNullOrEmpty(text)) return;
            // 连续出餐时同一位置的飘字直接替换，避免新旧两行叠成重影。
            for (int i = Instance.popups.Count - 1; i >= 0; i--)
            {
                PopupItem existing = Instance.popups[i];
                if (existing.text != text || (existing.worldPos - worldPos).sqrMagnitude > 0.36f) continue;
                Instance.popups.RemoveAt(i);
            }
            Instance.popups.Add(new PopupItem { worldPos = worldPos, text = text, color = color, big = big, life = big ? 1.4f : 1.1f });
            if (Instance.popups.Count > 12) Instance.popups.RemoveAt(0);
        }

        public static void Clear()
        {
            if (Instance == null) return;
            foreach (Particle p in Instance.pool)
            {
                p.active = false;
                p.transform.gameObject.SetActive(false);
            }
            Instance.ActiveParticles = 0;
            Instance.RestorePunches();
            Instance.popups.Clear();
            Instance.shakeRemaining = 0f;
            ShakeOffset = Vector3.zero;
        }

        private void Spawn(Vector3 position, Vector3 velocity, Color color, float size, float gravity, float drag, float life, bool grow)
        {
            Particle p = pool[cursor];
            cursor = (cursor + 1) % PoolSize;
            if (!p.active) ActiveParticles++;
            p.active = true;
            p.velocity = velocity;
            p.spin = Random.insideUnitSphere * 540f;
            p.gravity = gravity;
            p.drag = drag;
            p.size = size;
            p.life = p.maxLife = Mathf.Max(0.05f, life);
            p.grow = grow;
            p.filter.sharedMesh = grow ? sphereMesh : cubeMesh;
            p.transform.position = position;
            p.transform.rotation = Random.rotation;
            p.transform.localScale = Vector3.one * size;
            block.Clear();
            block.SetColor("_Color", color);
            block.SetColor("_BaseColor", color);
            p.renderer.SetPropertyBlock(block);
            p.transform.gameObject.SetActive(true);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt > 0f) UpdateParticles(dt);
            UpdatePunches(dt);
            for (int i = popups.Count - 1; i >= 0; i--)
            {
                PopupItem item = popups[i];
                item.age += Time.unscaledDeltaTime;
                if (item.age >= item.life) popups.RemoveAt(i);
                else popups[i] = item;
            }
            if (shakeRemaining > 0f)
            {
                shakeRemaining = Mathf.Max(0f, shakeRemaining - Time.unscaledDeltaTime);
                float k = shakeStrength * shakeRemaining / shakeDuration;
                float t = Time.unscaledTime * 28f;
                ShakeOffset = new Vector3(Mathf.PerlinNoise(shakeSeed, t) - 0.5f, Mathf.PerlinNoise(shakeSeed + 7f, t) - 0.5f, Mathf.PerlinNoise(shakeSeed + 13f, t) - 0.5f) * 2f * k;
            }
            else ShakeOffset = Vector3.zero;
        }

        private void UpdateParticles(float dt)
        {
            foreach (Particle p in pool)
            {
                if (!p.active) continue;
                p.life -= dt;
                if (p.life <= 0f)
                {
                    p.active = false;
                    ActiveParticles--;
                    p.transform.gameObject.SetActive(false);
                    continue;
                }
                p.velocity.y -= p.gravity * dt;
                p.velocity *= Mathf.Max(0f, 1f - p.drag * dt);
                Vector3 pos = p.transform.position + p.velocity * dt;
                if (pos.y < 0.02f && p.gravity > 0f)
                {
                    pos.y = 0.02f;
                    p.velocity = new Vector3(p.velocity.x * 0.5f, Mathf.Abs(p.velocity.y) * 0.3f, p.velocity.z * 0.5f);
                }
                p.transform.position = pos;
                p.transform.Rotate(p.spin * dt, Space.Self);
                float t = p.life / p.maxLife;
                float scale = p.grow ? Mathf.Lerp(1.9f, 0.6f, t) * Mathf.Clamp01(t * 2.5f) : Mathf.Clamp01(t * 1.6f);
                p.transform.localScale = Vector3.one * (p.size * scale);
            }
        }

        private void UpdatePunches(float dt)
        {
            for (int i = punches.Count - 1; i >= 0; i--)
            {
                PunchItem item = punches[i];
                if (item.target == null) { punches.RemoveAt(i); continue; }
                item.elapsed += dt;
                float p = Mathf.Clamp01(item.elapsed / item.duration);
                if (p >= 1f)
                {
                    item.target.localScale = item.baseScale;
                    punches.RemoveAt(i);
                    continue;
                }
                float wave = item.amount * Mathf.Sin(p * Mathf.PI * 2.5f) * (1f - p);
                item.target.localScale = new Vector3(item.baseScale.x * (1f + wave), item.baseScale.y * (1f - wave * 0.6f), item.baseScale.z * (1f + wave));
            }
        }

        private void RestorePunches()
        {
            foreach (PunchItem item in punches)
                if (item.target != null) item.target.localScale = item.baseScale;
            punches.Clear();
        }
    }
}
