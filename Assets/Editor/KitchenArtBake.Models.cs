using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using KitchenChaos.Data;

namespace KitchenChaos.EditorTools
{
    public static partial class KitchenArtBake
    {
        private const string KenneyRoot = "Assets/ThirdParty/KenneyFoodKit/Models/FBX format/";

        private static void BakeFoodAndProps()
        {
            Material atlas = SaveMaterial("FoodAtlas");
            atlas.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(KenneyRoot + "Textures/colormap.png"));
            atlas.SetColor("_BaseColor", Color.white);
            atlas.SetFloat("_Smoothness", 0.28f);
            Material cooked = SaveMaterial("FoodCooked");
            cooked.CopyPropertiesFromMaterial(atlas);
            cooked.SetColor("_BaseColor", new Color(0.64f, 0.46f, 0.27f));
            Material burned = SaveMaterial("FoodBurned");
            burned.SetColor("_BaseColor", new Color(0.045f, 0.032f, 0.023f));
            burned.SetFloat("_Smoothness", 0.05f);
            foreach (IngredientType type in Enum.GetValues(typeof(IngredientType)))
                foreach (IngredientState state in Enum.GetValues(typeof(IngredientState)))
                {
                    GameObject root = new GameObject(type + "_" + state);
                    bool cut = state == IngredientState.Cut || state == IngredientState.Cooked;
                    string file = type switch
                    {
                        IngredientType.Tomato => cut ? "tomato-slice" : "tomato",
                        IngredientType.Lettuce => "cabbage",
                        IngredientType.Cheese => cut ? "cheese-cut" : "cheese",
                        IngredientType.MeatPatty => "meat-patty",
                        IngredientType.Bun => "loaf-round",
                        IngredientType.Onion => cut ? "onion-half" : "onion",
                        IngredientType.Mushroom => cut ? "mushroom-half" : "mushroom",
                        _ => throw new ArgumentOutOfRangeException(nameof(type))
                    };
                    float height = type == IngredientType.MeatPatty ? 0.13f : type == IngredientType.Bun ? 0.30f : 0.5f;
                    GameObject model = type == IngredientType.Lettuce && cut
                        ? BuildLeaves(root.transform)
                        : ImportModel(file, root.transform, type == IngredientType.Bun ? 0.65f : 0.58f, height, atlas);
                    if (type == IngredientType.MeatPatty)
                    {
                        Material meat = AssetDatabase.LoadAssetAtPath<Material>(ArtRoot + "/Materials/Patty" + (state == IngredientState.Cooked ? "Cooked" : "Raw") + ".mat");
                        ProjectMeshUV(model, "Patty_" + state);
                        AssignMaterial(model, meat);
                    }
                    else if (state == IngredientState.Cooked) AssignMaterial(model, cooked);
                    if (state == IngredientState.Burned) AssignMaterial(model, burned);
                    PrefabUtility.SaveAsPrefabAsset(root, ArtRoot + "/Food/" + root.name + ".prefab");
                    UnityEngine.Object.DestroyImmediate(root);
                }
            string[] props = { "plate", "plate-dinner", "cutting-board", "frying-pan", "cooking-knife", "knife-block", "bottle-ketchup", "bottle-musterd", "bottle-oil", "pepper-mill", "shaker-salt", "pot", "bowl" };
            foreach (string prop in props)
            {
                GameObject root = new GameObject(prop);
                float width = prop.StartsWith("plate") ? 0.85f : prop == "frying-pan" ? 0.92f : prop == "cutting-board" ? 0.86f : 0.3f;
                GameObject model = ImportModel(prop, root.transform, width, prop.StartsWith("plate") ? 0.08f : 0.5f, atlas);
                if (prop == "frying-pan") CenterFryingPan(model, root.transform);
                PrefabUtility.SaveAsPrefabAsset(root, ArtRoot + "/Props/" + prop + ".prefab");
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void CenterFryingPan(GameObject model, Transform root)
        {
            var planes = new Dictionary<int, (float area, Vector3 weightedCenter)>();
            foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>())
            {
                Vector3[] vertices = filter.sharedMesh.vertices;
                int[] triangles = filter.sharedMesh.triangles;
                Matrix4x4 toRoot = root.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    Vector3 a = toRoot.MultiplyPoint3x4(vertices[triangles[i]]);
                    Vector3 b = toRoot.MultiplyPoint3x4(vertices[triangles[i + 1]]);
                    Vector3 c = toRoot.MultiplyPoint3x4(vertices[triangles[i + 2]]);
                    Vector3 normal = Vector3.Cross(b - a, c - a);
                    if (normal.sqrMagnitude < 1e-12f || normal.normalized.y < 0.999f) continue;
                    Vector3 center = (a + b + c) / 3f;
                    float area = normal.magnitude * 0.5f;
                    int height = Mathf.RoundToInt(center.y * 10000f);
                    planes.TryGetValue(height, out var plane);
                    planes[height] = (plane.area + area, plane.weightedCenter + center * area);
                }
            }
            float largest = 0f;
            Vector3 surface = Vector3.zero;
            // The interior floor is the largest upward horizontal surface; bounds include the handle.
            foreach (var plane in planes.Values)
                if (plane.area > largest) { largest = plane.area; surface = plane.weightedCenter / plane.area; }
            if (largest <= 0f) throw new InvalidOperationException("Frying pan has no cooking surface.");
            model.transform.localPosition -= new Vector3(surface.x, 0f, surface.z);
            var anchor = new GameObject("CookingSurface").transform;
            anchor.SetParent(root, false);
            anchor.localPosition = new Vector3(0f, surface.y + 0.003f, 0f);
            Debug.Log("FRYING PAN SURFACE: center=" + surface.ToString("F5") + "; area=" + largest.ToString("F5"));
        }

        private static GameObject ImportModel(string name, Transform parent, float width, float maxHeight, Material material)
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(KenneyRoot + name + ".fbx");
            if (source == null) throw new InvalidOperationException("Missing food model: " + name);
            GameObject model = UnityEngine.Object.Instantiate(source, parent);
            model.name = name;
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
            float scale = width / Mathf.Max(bounds.size.x, bounds.size.z);
            Vector3 size = new Vector3(scale, Mathf.Min(scale, maxHeight / Mathf.Max(bounds.size.y, 0.001f)), scale);
            model.transform.localScale = size;
            model.transform.localPosition = -Vector3.Scale(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z), size);
            foreach (Collider collider in model.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(collider);
            AssignMaterial(model, material);
            return model;
        }

        private static void AssignMaterial(GameObject model, Material material)
        {
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
            {
                Material[] slots = renderer.sharedMaterials;
                for (int i = 0; i < slots.Length; i++) slots[i] = material;
                renderer.sharedMaterials = slots;
            }
        }

        private static void ProjectMeshUV(GameObject model, string name)
        {
            int index = 0;
            foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>())
            {
                Mesh mesh = UnityEngine.Object.Instantiate(filter.sharedMesh);
                Vector3[] vertices = mesh.vertices;
                Vector2[] uv = new Vector2[vertices.Length];
                Bounds bounds = mesh.bounds;
                for (int i = 0; i < uv.Length; i++)
                    uv[i] = new Vector2((vertices[i].x - bounds.min.x) / Mathf.Max(bounds.size.x, 0.001f), (vertices[i].z - bounds.min.z) / Mathf.Max(bounds.size.z, 0.001f));
                mesh.uv = uv;
                mesh.RecalculateTangents();
                mesh.name = name;
                string path = ArtRoot + "/Food/" + name + "_" + index++ + ".asset";
                Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (existing == null) AssetDatabase.CreateAsset(mesh, path);
                else { EditorUtility.CopySerialized(mesh, existing); UnityEngine.Object.DestroyImmediate(mesh); mesh = existing; }
                filter.sharedMesh = mesh;
            }
        }

        private static GameObject BuildLeaves(Transform parent)
        {
            var root = new GameObject("LettuceLeaves");
            root.transform.SetParent(parent, false);
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            const int segments = 32;
            for (int leaf = 0; leaf < 3; leaf++)
            {
                int offset = vertices.Count;
                vertices.Add(new Vector3(0, 0.02f + leaf * 0.03f, 0));
                uv.Add(new Vector2(0.5f, 0.5f));
                for (int i = 0; i <= segments; i++)
                {
                    float angle = i * Mathf.PI * 2f / segments;
                    float radius = 0.26f + Mathf.Sin(angle * 7f + leaf) * 0.025f;
                    vertices.Add(new Vector3(Mathf.Cos(angle) * radius, 0.045f + leaf * 0.03f + Mathf.Sin(angle * 5f + leaf) * 0.025f, Mathf.Sin(angle) * radius));
                    uv.Add(new Vector2(Mathf.Cos(angle) * 0.5f + 0.5f, Mathf.Sin(angle) * 0.5f + 0.5f));
                    if (i == segments) continue;
                    triangles.Add(offset); triangles.Add(offset + i + 2); triangles.Add(offset + i + 1);
                }
            }
            string path = ArtRoot + "/Food/LettuceLeaves.asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null) { mesh = new Mesh(); AssetDatabase.CreateAsset(mesh, path); }
            mesh.Clear(); mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            Material material = AssetDatabase.LoadAssetAtPath<Material>(ArtRoot + "/Materials/Lettuce.mat");
            material.SetFloat("_Cull", 0f);
            root.AddComponent<MeshRenderer>().sharedMaterial = material;
            EditorUtility.SetDirty(material);
            EditorUtility.SetDirty(mesh);
            return root;
        }
    }
}
