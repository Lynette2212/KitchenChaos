using UnityEngine;
using KitchenChaos.Visual;

namespace KitchenChaos.Core
{
    public partial class KitchenBuilder
    {
        private void BuildDecor()
        {
            Material oak = VisualFactory.Surface("Oak", new Vector2(3f, 1f));
            Material teal = VisualFactory.Surface("Cabinet");
            Material steel = VisualFactory.Surface("Steel");
            foreach (float z in new[] { -3.33f, 3.33f })
            {
                Detail("Wall dado", transform, new Vector3(0, 1.42f, z), new Vector3(8.7f, 0.09f, 0.04f), teal);
                Detail("Upper trim", transform, new Vector3(0, 2.94f, z), new Vector3(8.7f, 0.12f, 0.07f), oak);
            }
            Detail("Ingredient shelf", transform, new Vector3(0, 2.08f, 3.12f), new Vector3(7.9f, 0.09f, 0.40f), oak);
            Detail("Shelf light", transform, new Vector3(0, 2.025f, 2.99f), new Vector3(7.7f, 0.025f, 0.035f), VisualFactory.MakeMaterial(new Color(1f, 0.85f, 0.49f)));
            string[] shelfProps = { "pot", "bottle-oil", "pepper-mill", "bowl", "bottle-ketchup", "bottle-musterd", "shaker-salt" };
            for (int i = 0; i < shelfProps.Length; i++)
                PlaceProp(shelfProps[i], transform, new Vector3(-3.35f + i * 1.1f, 2.13f, 3.11f), Vector3.one, i % 2 == 0 ? 15 : -12);
            Label("DEEP BLUE  /  FRESH KITCHEN", transform, new Vector3(0, 2.63f, 3.31f), 0, 0.04f, new Color(0.10f, 0.22f, 0.23f));
            Detail("Extractor canopy", transform, new Vector3(2.05f, 2.35f, -3.03f), new Vector3(2.75f, 0.24f, 0.6f), steel);
            Detail("Extractor duct", transform, new Vector3(2.05f, 2.7f, -3.17f), new Vector3(0.78f, 0.6f, 0.3f), steel);
            for (int i = 0; i < 12; i++)
                Detail("Extractor vent", transform, new Vector3(0.84f + i * 0.22f, 2.35f, -2.72f), new Vector3(0.10f, 0.08f, 0.01f), VisualFactory.Surface("Grill"));
            PlaceProp("knife-block", transform, new Vector3(-2.48f, 1.06f, -2.78f), Vector3.one * 0.8f, 20);
            Detail("Prep rail", transform, new Vector3(-1.05f, 1.88f, -3.24f), new Vector3(2.2f, 0.04f, 0.04f), steel);
            Label("PREP   /   COOK", transform, new Vector3(-1.1f, 2.45f, -3.30f), 180, 0.028f, new Color(0.1f, 0.22f, 0.23f));
            Detail("Service frame", transform, new Vector3(4.30f, 1.7f, -1.7f), new Vector3(0.10f, 1.26f, 2.0f), oak);
            Detail("Service chalkboard", transform, new Vector3(4.24f, 1.7f, -1.7f), new Vector3(0.025f, 1.1f, 1.84f), VisualFactory.MakeMaterial(new Color(0.035f, 0.095f, 0.09f), 0.05f));
            Label("ORDER UP!\n\nHANDMADE DAILY", transform, new Vector3(4.215f, 1.71f, -1.7f), 270, 0.021f, new Color(0.91f, 0.85f, 0.65f));
            foreach (float x in new[] { -2.2f, 2.2f })
            {
                Detail("Ceiling fixture", transform, new Vector3(x, 2.92f, 0), new Vector3(1.4f, 0.1f, 0.24f), teal);
                Detail("Warm diffuser", transform, new Vector3(x, 2.86f, 0), new Vector3(1.28f, 0.035f, 0.17f), VisualFactory.MakeMaterial(new Color(1f, 0.9f, 0.7f)));
                var fixture = new GameObject("Warm task light");
                fixture.transform.SetParent(transform, false);
                fixture.transform.localPosition = new Vector3(x, 2.7f, 0);
                Light light = fixture.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1f, 0.85f, 0.62f);
                light.intensity = 1.6f;
                light.range = 5f;
            }
        }

        private void DecorateCounter(GameObject counter, Color accent)
        {
            Transform front = new GameObject("Cabinet front").transform;
            front.SetParent(counter.transform, false);
            Vector3 pos = counter.transform.position;
            float yaw = Mathf.Abs(pos.z) > 2f ? (pos.z > 0 ? 0 : 180) : pos.x < 0 ? -90 : 90;
            front.localRotation = Quaternion.Euler(0, yaw, 0);
            Material oak = VisualFactory.Surface("Oak");
            Material steel = VisualFactory.Surface("Steel");
            Detail("Drawer", front, new Vector3(0, 0.28f, -0.512f), new Vector3(0.9f, 0.23f, 0.035f), oak);
            Detail("Lower panel", front, new Vector3(0, -0.14f, -0.512f), new Vector3(0.9f, 0.55f, 0.035f), VisualFactory.Surface("Cabinet"));
            Detail("Left stile", front, new Vector3(-0.44f, -0.12f, -0.535f), new Vector3(0.025f, 0.58f, 0.02f), oak);
            Detail("Right stile", front, new Vector3(0.44f, -0.12f, -0.535f), new Vector3(0.025f, 0.58f, 0.02f), oak);
            Detail("Toe kick", front, new Vector3(0, -0.455f, -0.512f), new Vector3(0.96f, 0.065f, 0.035f), VisualFactory.Surface("Grill"));
            Detail("Drawer pull", front, new Vector3(0, 0.3f, -0.55f), new Vector3(0.25f, 0.027f, 0.032f), steel);
            Detail("Station stripe", front, new Vector3(0, 0.48f, -0.532f), new Vector3(0.93f, 0.033f, 0.02f), VisualFactory.MakeMaterial(accent));
        }

        private void StationLabel(GameObject station, string text)
        {
            Transform front = station.transform.Find("Cabinet front");
            Detail("Label plate", front, new Vector3(0, 0.005f, -0.54f), new Vector3(0.76f, 0.17f, 0.022f), VisualFactory.MakeMaterial(new Color(0.87f, 0.81f, 0.64f)));
            Label(text, front, new Vector3(0, 0.005f, -0.554f), 0, 0.011f, new Color(0.10f, 0.19f, 0.19f));
        }

        private static GameObject Detail(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            return Part(name, parent, PrimitiveType.Cube, position, scale, material);
        }

        private static GameObject Part(string name, Transform parent, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            Collider collider = go.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        private static GameObject PlaceProp(string name, Transform parent, Vector3 position, Vector3 scale, float yaw = 0f)
        {
            GameObject go = VisualFactory.CreateProp(name);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            go.transform.localScale = scale;
            return go;
        }

        private static float SurfaceTop(GameObject prop)
        {
            float top = float.NegativeInfinity;
            foreach (Renderer renderer in prop.GetComponentsInChildren<Renderer>()) top = Mathf.Max(top, renderer.bounds.max.y);
            return top;
        }

        private static void Label(string text, Transform parent, Vector3 position, float yaw, float size, Color color)
        {
            var go = new GameObject("Label " + text);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            var label = go.AddComponent<TextMesh>();
            label.text = text;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 64;
            label.characterSize = size;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.color = color;
            go.GetComponent<MeshRenderer>().sharedMaterial = label.font.material;
        }

        private void BuildServiceWindow(Material wallMat)
        {
            CreateWall("Wall_E", new Vector3(4.5f, 1.5f, -1.675f), new Vector3(0.3f, 3f, 3.65f), wallMat);
            CreateWall("Wall_E_North", new Vector3(4.5f, 1.5f, 2.975f), new Vector3(0.3f, 3f, 1.05f), wallMat);
            CreateWall("WindowSill", new Vector3(4.5f, 0.6f, 1.3f), new Vector3(0.3f, 1.2f, 2.3f), wallMat);
            CreateWall("WindowHeader", new Vector3(4.5f, 2.75f, 1.3f), new Vector3(0.3f, 0.5f, 2.3f), wallMat);

            Material oak = VisualFactory.Surface("Oak");
            Detail("Window frame left", transform, new Vector3(4.32f, 1.85f, 0.2f), new Vector3(0.07f, 1.36f, 0.08f), oak);
            Detail("Window frame right", transform, new Vector3(4.32f, 1.85f, 2.4f), new Vector3(0.07f, 1.36f, 0.08f), oak);
            Detail("Window frame top", transform, new Vector3(4.32f, 2.46f, 1.3f), new Vector3(0.07f, 0.08f, 2.28f), oak);
            Detail("Window frame bottom", transform, new Vector3(4.32f, 1.24f, 1.3f), new Vector3(0.07f, 0.08f, 2.28f), oak);
            Detail("Window mullion", transform, new Vector3(4.32f, 1.85f, 1.3f), new Vector3(0.05f, 1.2f, 0.05f), oak);
            Detail("Awning", transform, new Vector3(5.2f, 2.58f, 1.3f), new Vector3(1.15f, 0.06f, 2.55f), VisualFactory.MakeMaterial(new Color(0.72f, 0.18f, 0.16f)));
            Label("取餐窗口", transform, new Vector3(4.28f, 2.66f, 1.3f), 270, 0.028f, new Color(0.12f, 0.24f, 0.25f));

            Material glass = new Material(VisualFactory.LitShader) { name = "StorefrontGlass" };
            glass.SetColor("_BaseColor", new Color(0.72f, 0.86f, 0.95f, 0.18f));
            glass.SetFloat("_Smoothness", 0.92f);
            glass.SetFloat("_Surface", 1f);
            glass.SetOverrideTag("RenderType", "Transparent");
            glass.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            glass.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            glass.SetInt("_ZWrite", 0);
            glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            glass.renderQueue = 3000;
            GameObject pane = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pane.name = "ServiceWindow";
            pane.transform.SetParent(transform, false);
            pane.transform.position = new Vector3(4.46f, 1.85f, 1.3f);
            pane.transform.localScale = new Vector3(0.015f, 1.16f, 2.08f);
            Collider paneCollider = pane.GetComponent<Collider>();
            paneCollider.enabled = false;
            Destroy(paneCollider);
            pane.GetComponent<Renderer>().sharedMaterial = glass;

            Detail("Sidewalk", transform, new Vector3(6.55f, -0.02f, 1.2f), new Vector3(3.6f, 0.08f, 5.4f), VisualFactory.Surface("FloorCream"));
            Detail("Street", transform, new Vector3(8.55f, -0.05f, 1.2f), new Vector3(1.5f, 0.06f, 6.6f), VisualFactory.MakeMaterial(new Color(0.18f, 0.2f, 0.22f), 0.12f));
            Detail("Opposite block", transform, new Vector3(9.35f, 1.7f, 1.2f), new Vector3(0.28f, 3.6f, 7.2f), VisualFactory.MakeMaterial(new Color(0.25f, 0.3f, 0.36f), 0.18f));
            Material streetWindows = LitWindow();
            for (int i = 0; i < 4; i++)
                Detail("Street window", transform, new Vector3(9.18f, 1.15f + (i % 2) * 1.15f, -0.8f + i * 1.15f), new Vector3(0.04f, 0.55f, 0.42f), streetWindows);

            GameObject lamp = new GameObject("Storefront light");
            lamp.transform.SetParent(transform, false);
            lamp.transform.position = new Vector3(5.35f, 2.45f, 1.3f);
            Light light = lamp.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.8f, 0.52f);
            light.intensity = 2.6f;
            light.range = 8f;
        }

        private static Material LitWindow()
        {
            Material glow = new Material(VisualFactory.LitShader) { name = "StreetWindow" };
            glow.SetColor("_BaseColor", new Color(1f, 0.78f, 0.42f));
            glow.EnableKeyword("_EMISSION");
            glow.SetColor("_EmissionColor", new Color(1f, 0.62f, 0.22f) * 1.6f);
            return glow;
        }
    }
}
