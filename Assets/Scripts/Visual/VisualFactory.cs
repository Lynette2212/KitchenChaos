using System.Collections.Generic;
using UnityEngine;
using KitchenChaos.Data;

namespace KitchenChaos.Visual
{
    public static class VisualFactory
    {
        private static Shader litShader;
        private static readonly Dictionary<(Color, float, float), Material> materials = new Dictionary<(Color, float, float), Material>();
        private static readonly Dictionary<(string, Vector2), Material> surfaces = new Dictionary<(string, Vector2), Material>();
        private static readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();

        public static Shader LitShader
        {
            get
            {
                if (litShader == null) litShader = Shader.Find("Universal Render Pipeline/Lit");
                return litShader;
            }
        }

        public static Material MakeMaterial(Color color, float smoothness = 0.25f, float metallic = 0f)
        {
            var key = (color, smoothness, metallic);
            if (materials.TryGetValue(key, out Material existing) && existing != null) return existing;
            var mat = new Material(LitShader);
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", metallic);
            materials[key] = mat;
            return mat;
        }

        public static Material Surface(string name) => Surface(name, Vector2.one);

        public static Material Surface(string name, Vector2 tiling)
        {
            var key = (name, tiling);
            if (surfaces.TryGetValue(key, out Material existing) && existing != null) return existing;
            var material = new Material(Resources.Load<Material>("Art/Materials/" + name));
            material.name = name;
            material.SetTextureScale("_BaseMap", tiling);
            material.SetTextureScale("_BumpMap", tiling);
            surfaces[key] = material;
            return material;
        }

        public static void ReleaseMaterials()
        {
            foreach (Material material in materials.Values)
                if (material != null) Object.Destroy(material);
            foreach (Material material in surfaces.Values)
                if (material != null) Object.Destroy(material);
            materials.Clear();
            surfaces.Clear();
            prefabs.Clear();
        }

        public static Material BurnedMaterial => MakeMaterial(new Color(0.05f, 0.04f, 0.03f), 0.05f);

        public static GameObject CreateIngredientVisual(IngredientType type, IngredientState state)
        {
            GameObject root = InstantiateArt("Food/" + type + "_" + state);
            root.name = "Ingredient_" + type + "_" + state;
            return root;
        }

        public static GameObject CreateProp(string name) => InstantiateArt("Props/" + name);

        private static GameObject InstantiateArt(string name)
        {
            if (!prefabs.TryGetValue(name, out GameObject prefab) || prefab == null)
            {
                prefab = Resources.Load<GameObject>("Art/" + name);
                prefabs[name] = prefab;
            }
            return Object.Instantiate(prefab);
        }

        public static Texture2D MakeFlatIcon(Color color, int size = 64, string label = null)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x / (float)(size - 1) - 0.5f;
                    float dy = y / (float)(size - 1) - 0.5f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01((0.5f - d) * 4f);
                    tex.SetPixel(x, y, new Color(color.r, color.g, color.b, alpha));
                }
            }
            tex.Apply();
            tex.name = "icon_" + (string.IsNullOrEmpty(label) ? color.ToString() : label);
            return tex;
        }
    }
}
