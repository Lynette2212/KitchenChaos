using UnityEngine;
using KitchenChaos.Visual;

namespace KitchenChaos.Core
{
    public partial class KitchenBuilder
    {
        private void BuildOpenKitchen()
        {
            Material soil = VisualFactory.MakeMaterial(new Color(0.28f, 0.22f, 0.16f), 0.08f);
            Material planterWood = VisualFactory.MakeMaterial(new Color(0.46f, 0.37f, 0.29f), 0.12f);
            Planter("West planter A", new Vector3(-6.6f, 0f, 2.8f), new Vector3(1.3f, 0.5f, 1.1f), soil, planterWood);
            Planter("West planter B", new Vector3(-6.6f, 0f, -2.8f), new Vector3(1.15f, 0.45f, 1.2f), soil, planterWood);
            Planter("North hedge", new Vector3(0f, 0f, 3.4f), new Vector3(8.7f, 0.35f, 0.42f), soil, planterWood);
            foreach (float x in new[] { -2.4f, 2.4f })
            {
                var lightGo = new GameObject("Terrace light");
                lightGo.transform.SetParent(transform, false);
                lightGo.transform.localPosition = new Vector3(x, 2.6f, 0.4f);
                Light light = lightGo.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1f, 0.88f, 0.68f);
                light.intensity = 1.05f;
                light.range = 6.5f;
            }
        }

        private void Planter(string name, Vector3 position, Vector3 size, Material soil, Material planterWood)
        {
            Detail(name, transform, position + Vector3.up * (size.y * 0.5f + 0.01f), size, planterWood);
            Detail(name + " soil", transform, position + Vector3.up * (size.y + 0.02f),
                new Vector3(size.x * 0.9f, 0.03f, size.z * 0.86f), soil);
            Material leafA = VisualFactory.MakeMaterial(new Color(0.31f, 0.50f, 0.31f), 0.18f);
            Material leafB = VisualFactory.MakeMaterial(new Color(0.42f, 0.60f, 0.38f), 0.18f);
            Material leafC = VisualFactory.MakeMaterial(new Color(0.25f, 0.43f, 0.29f), 0.18f);
            int count = Mathf.Max(1, Mathf.RoundToInt(size.x / 0.48f));
            for (int i = 0; i < count; i++)
            {
                float t = count == 1 ? 0.5f : i / (float)(count - 1);
                float x = Mathf.Lerp(-size.x * 0.38f, size.x * 0.38f, t);
                float z = Mathf.Sin(i * 1.7f) * size.z * 0.16f;
                float height = 0.15f + (i % 3) * 0.045f;
                GameObject bush = Part("Bush", transform, PrimitiveType.Sphere,
                    new Vector3(position.x + x, size.y + 0.06f + height, position.z + z),
                    new Vector3(0.44f, 0.36f, 0.44f), i % 3 == 0 ? leafB : i % 3 == 1 ? leafA : leafC);
                bush.transform.localRotation = Quaternion.Euler(0f, i * 37f, 0f);
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

    }
}
