using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace KitchenChaos.EditorTools
{
    public static partial class KitchenArtBake
    {
        private const string ArtRoot = "Assets/Resources/Art";
        private const int TextureSize = 512;

        public static void Bake()
        {
            Directory.CreateDirectory(ArtRoot + "/Textures");
            Directory.CreateDirectory(ArtRoot + "/Materials");
            Directory.CreateDirectory(ArtRoot + "/Food");
            Directory.CreateDirectory(ArtRoot + "/Props");
            AssetDatabase.Refresh();
            BakeSurface("FloorCream", new Color(0.76f, 0.73f, 0.63f), 0.32f, 0f, Floor);
            BakeSurface("FloorTeal", new Color(0.19f, 0.34f, 0.33f), 0.32f, 0f, Floor);
            BakeSurface("WallTiles", new Color(0.80f, 0.87f, 0.79f), 0.55f, 0f, Tiles);
            BakeSurface("Oak", new Color(0.57f, 0.32f, 0.15f), 0.28f, 0f, Wood);
            BakeSurface("Terrazzo", new Color(0.83f, 0.80f, 0.70f), 0.45f, 0f, Terrazzo);
            BakeSurface("Steel", new Color(0.45f, 0.50f, 0.49f), 0.62f, 0.65f, Steel);
            BakeSurface("Cabinet", new Color(0.13f, 0.28f, 0.30f), 0.30f, 0f, Paint);
            BakeSurface("Grill", new Color(0.09f, 0.095f, 0.085f), 0.38f, 0.6f, Steel);
            BakeSurface("Ceramic", new Color(0.92f, 0.91f, 0.84f), 0.66f, 0f, Paint);
            BakeSurface("PattyRaw", new Color(0.57f, 0.22f, 0.20f), 0.25f, 0f, Meat);
            BakeSurface("PattyCooked", new Color(0.29f, 0.12f, 0.055f), 0.28f, 0f, GrilledMeat);
            BakeSurface("Onion", new Color(0.68f, 0.31f, 0.49f), 0.38f, 0f, Onion);
            BakeSurface("OnionCut", new Color(0.89f, 0.76f, 0.83f), 0.4f, 0f, OnionRings);
            BakeSurface("Lettuce", new Color(0.31f, 0.53f, 0.10f), 0.22f, 0f, Leaf);
            BakeFoodAndProps();
            AssetDatabase.SaveAssets();
            Debug.Log("KITCHEN ART BAKE: complete");
        }

        private static float Grain(float x, float y, float scale) => Mathf.PerlinNoise(x * scale + 13.7f, y * scale + 6.1f);

        private static Color Floor(float u, float v, Color color)
        {
            float edge = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
            if (edge < 0.009f) return new Color(0.33f, 0.36f, 0.33f);
            float variation = (Grain(u, v, 100f) - 0.5f) * 0.065f;
            float bevel = Mathf.Clamp01((edge - 0.009f) * 140f);
            return color * (0.88f + bevel * 0.12f) + new Color(variation, variation, variation);
        }

        private static Color Tiles(float u, float v, Color color)
        {
            int row = Mathf.FloorToInt(v * 8f);
            float x = Mathf.Repeat(u * 4f + (row % 2) * 0.5f, 1f);
            float y = Mathf.Repeat(v * 8f, 1f);
            float edge = Mathf.Min(Mathf.Min(x, 1f - x), Mathf.Min(y, 1f - y));
            if (edge < 0.034f) return new Color(0.36f, 0.45f, 0.41f);
            float glaze = Grain(u, v, 35f) * 0.035f + Grain(Mathf.Floor(u * 4f), row, 1f) * 0.055f;
            float bevel = Mathf.Clamp01((edge - 0.034f) * 16f);
            return color * (0.86f + bevel * 0.1f + glaze);
        }

        private static Color Wood(float u, float v, Color color)
        {
            float warp = Grain(u, v, 4f) * 0.06f;
            float grain = Mathf.Sin((v + warp) * Mathf.PI * 120f) * 0.035f;
            float fine = Grain(u * 0.12f, v, 160f) * 0.12f;
            float plank = Mathf.Repeat(v * 4f, 1f) < 0.012f ? 0.67f : 1f;
            return color * (0.91f + grain + fine) * plank;
        }

        private static Color Terrazzo(float u, float v, Color color)
        {
            float n = Grain(u, v, 110f);
            if (n > 0.71f) return color * 0.65f;
            if (n < 0.28f) return Color.Lerp(color, new Color(0.43f, 0.52f, 0.48f), 0.55f);
            return color * (0.96f + Grain(u, v, 280f) * 0.05f);
        }

        private static Color Steel(float u, float v, Color color) => color * (0.88f + Grain(u * 0.016f, v, 460f) * 0.2f);
        private static Color Paint(float u, float v, Color color) => color * (0.98f + Grain(u, v, 100f) * 0.04f);
        private static Color Meat(float u, float v, Color color)
        {
            float n = Grain(u, v, 48f);
            return Color.Lerp(color * 0.66f, new Color(0.76f, 0.48f, 0.37f), Mathf.Pow(n, 2.2f)) * (0.88f + Grain(u, v, 190f) * 0.2f);
        }
        private static Color GrilledMeat(float u, float v, Color color)
        {
            float stripe = Mathf.Abs(Mathf.Sin((u + v * 0.23f) * Mathf.PI * 6f));
            return Meat(u, v, color) * (stripe < 0.20f ? 0.34f : 0.94f);
        }
        private static Color Onion(float u, float v, Color color) => color * (0.88f + Mathf.Pow(Mathf.Abs(Mathf.Sin(u * Mathf.PI * 36f + Grain(u, v, 3f))), 12f) * 0.3f);
        private static Color OnionRings(float u, float v, Color color)
        {
            float radius = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f));
            float ring = Mathf.Pow(Mathf.Abs(Mathf.Sin(radius * Mathf.PI * 24f)), 10f);
            return Color.Lerp(color, new Color(0.56f, 0.19f, 0.37f), ring * 0.65f);
        }
        private static Color Leaf(float u, float v, Color color)
        {
            float vein = Mathf.Pow(Mathf.Abs(Mathf.Sin((u + v * 0.5f) * Mathf.PI * 14f)), 16f);
            return color * (0.87f + Grain(u, v, 16f) * 0.25f + vein * 0.18f);
        }

        private static void BakeSurface(string name, Color color, float smoothness, float metallic, Func<float, float, Color, Color> draw)
        {
            var pixels = new Color[TextureSize * TextureSize];
            for (int y = 0; y < TextureSize; y++)
                for (int x = 0; x < TextureSize; x++)
                {
                    Color pixel = draw((x + 0.5f) / TextureSize, (y + 0.5f) / TextureSize, color);
                    pixel.a = 1f;
                    pixels[y * TextureSize + x] = pixel;
                }
            string basePath = ArtRoot + "/Textures/" + name;
            WriteTexture(basePath + ".png", pixels, false);
            var normal = new Color[pixels.Length];
            float relief = name == "WallTiles" || name.StartsWith("Floor") ? 4f : 1.3f;
            for (int y = 0; y < TextureSize; y++)
                for (int x = 0; x < TextureSize; x++)
                {
                    float dx = pixels[y * TextureSize + (x + 1) % TextureSize].grayscale - pixels[y * TextureSize + (x + TextureSize - 1) % TextureSize].grayscale;
                    float dy = pixels[((y + 1) % TextureSize) * TextureSize + x].grayscale - pixels[((y + TextureSize - 1) % TextureSize) * TextureSize + x].grayscale;
                    Vector3 n = new Vector3(-dx * relief, -dy * relief, 1f).normalized;
                    normal[y * TextureSize + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
                }
            WriteTexture(basePath + "_Normal.png", normal, true);
            Material mat = SaveMaterial(name);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(basePath + ".png"));
            mat.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(basePath + "_Normal.png"));
            mat.EnableKeyword("_NORMALMAP");
            mat.SetFloat("_BumpScale", 0.65f);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", metallic);
            EditorUtility.SetDirty(mat);
        }

        private static void WriteTexture(string path, Color[] pixels, bool normal)
        {
            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGB24, false, normal);
            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !normal;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 4;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = TextureSize;
            importer.SaveAndReimport();
        }

        private static Material SaveMaterial(string name)
        {
            string path = ArtRoot + "/Materials/" + name + ".mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            return mat;
        }
    }
}
