using UnityEngine;
using KitchenChaos.Data;

namespace KitchenChaos.Visual
{
    /// <summary>
    /// 运行时程序化生成厨房所需材质与食材 3D 模型，
    /// 全部使用 Unity 内置基本体，因此无需导入任何外部 FBX / PNG 素材。
    /// 自动兼容 URP（Lit）与内置管线（Standard）。
    /// </summary>
    public static class VisualFactory
    {
        private static Shader litShader;
        private static Material cachedBurned;

        public static Shader LitShader
        {
            get
            {
                if (litShader == null)
                {
                    litShader = Shader.Find("Universal Render Pipeline/Lit");
                    if (litShader == null) litShader = Shader.Find("Standard");
                    if (litShader == null) litShader = Shader.Find("Diffuse");
                }
                return litShader;
            }
        }

        public static Material MakeMaterial(Color color, float smoothness = 0.25f, float metallic = 0f)
        {
            Material mat;
            if (LitShader != null)
            {
                mat = new Material(LitShader);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            }
            else
            {
                mat = new Material(Shader.Find("Unlit/Color"));
                mat.color = color;
            }
            return mat;
        }

        public static Material BurnedMaterial
        {
            get
            {
                if (cachedBurned == null) cachedBurned = MakeMaterial(new Color(0.05f, 0.04f, 0.03f), 0.05f);
                return cachedBurned;
            }
        }

        /// <summary>根据类型与加工状态构建食材视觉模型（世界坐标局部原点在底部中心）。</summary>
        public static GameObject CreateIngredientVisual(IngredientType type, IngredientState state)
        {
            GameObject root = new GameObject("Ingredient_" + type + "_" + state);

            Color baseColor = IngredientCatalog.GetColor(type);
            Color cookedColor = Color.Lerp(baseColor, new Color(0.35f, 0.18f, 0.08f), 0.55f);

            GameObject body;
            switch (type)
            {
                case IngredientType.Tomato:
                    body = MakePrimitive(PrimitiveType.Sphere);
                    body.transform.localScale = Vector3.one * 0.62f;
                    body.transform.localPosition = new Vector3(0f, 0.31f, 0f);
                    break;
                case IngredientType.Lettuce:
                    body = MakePrimitive(PrimitiveType.Cylinder);
                    body.transform.localScale = new Vector3(0.62f, 0.12f, 0.62f);
                    body.transform.localPosition = new Vector3(0f, 0.12f, 0f);
                    break;
                case IngredientType.Cheese:
                    body = MakePrimitive(PrimitiveType.Cube);
                    body.transform.localScale = new Vector3(0.5f, 0.16f, 0.5f);
                    body.transform.localPosition = new Vector3(0f, 0.08f, 0f);
                    break;
                case IngredientType.MeatPatty:
                    body = MakePrimitive(PrimitiveType.Cylinder);
                    body.transform.localScale = new Vector3(0.6f, 0.16f, 0.6f);
                    body.transform.localPosition = new Vector3(0f, 0.16f, 0f);
                    break;
                case IngredientType.Bun:
                    body = MakePrimitive(PrimitiveType.Sphere);
                    body.transform.localScale = new Vector3(0.66f, 0.4f, 0.66f);
                    body.transform.localPosition = new Vector3(0f, 0.2f, 0f);
                    break;
                case IngredientType.Onion:
                    body = MakePrimitive(PrimitiveType.Sphere);
                    body.transform.localScale = Vector3.one * 0.52f;
                    body.transform.localPosition = new Vector3(0f, 0.26f, 0f);
                    break;
                case IngredientType.Mushroom:
                    body = MakePrimitive(PrimitiveType.Sphere);
                    body.transform.localScale = new Vector3(0.46f, 0.36f, 0.46f);
                    body.transform.localPosition = new Vector3(0f, 0.18f, 0f);
                    break;
                default:
                    body = MakePrimitive(PrimitiveType.Cube);
                    body.transform.localScale = Vector3.one * 0.5f;
                    body.transform.localPosition = new Vector3(0f, 0.25f, 0f);
                    break;
            }

            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            Renderer r = body.GetComponent<Renderer>();

            if (state == IngredientState.Burned)
            {
                r.sharedMaterial = BurnedMaterial;
            }
            else
            {
                Color useColor = state == IngredientState.Cooked ? cookedColor : baseColor;
                r.sharedMaterial = MakeMaterial(useColor, 0.35f);
            }

            // 切过的食材：再加两块小切片，表示被切碎
            if (state == IngredientState.Cut)
            {
                AddCutPieces(root.transform, type, baseColor);
            }

            return root;
        }

        private static void AddCutPieces(Transform parent, IngredientType type, Color color)
        {
            Material mat = MakeMaterial(color, 0.3f);
            for (int i = 0; i < 2; i++)
            {
                GameObject piece = MakePrimitive(PrimitiveType.Cube);
                piece.name = "CutPiece";
                piece.transform.SetParent(parent, false);
                piece.transform.localScale = new Vector3(0.2f, 0.1f, 0.2f);
                piece.transform.localPosition = new Vector3(i == 0 ? -0.16f : 0.16f, 0.05f, 0.05f * (i + 1));
                piece.transform.localRotation = Quaternion.Euler(0f, i == 0 ? 25f : -20f, 0f);
                piece.GetComponent<Renderer>().sharedMaterial = mat;
            }
        }

        private static GameObject MakePrimitive(PrimitiveType type)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            // 食材是纯视觉物体，移除碰撞体，避免干扰交互检测（用代码逻辑检测）
            Collider col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            return go;
        }

        /// <summary>生成一个带颜色的小圆片精灵，用于世界中的飘字/图标备用。</summary>
        public static Texture2D MakeFlatIcon(Color color, int size = 64, string label = null)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color clear = new Color(color.r, color.g, color.b, 1f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x / (float)(size - 1)) - 0.5f;
                    float dy = (y / (float)(size - 1)) - 0.5f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01((0.5f - d) * 4f);
                    tex.SetPixel(x, y, new Color(clear.r, clear.g, clear.b, alpha));
                }
            }
            tex.Apply();
            tex.name = "icon_" + (string.IsNullOrEmpty(label) ? color.ToString() : label);
            return tex;
        }
    }
}
