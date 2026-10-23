using System;
using UnityEditor;
using UnityEngine;
using KitchenChaos.Data;
using KitchenChaos.Visual;

namespace KitchenChaos.EditorTools
{
    public static partial class KitchenSmokeTest
    {
        private static void VerifyArtAssets()
        {
            string[] surfaces = { "WallTiles", "FloorCream", "FloorTeal", "Oak", "Terrazzo", "Steel", "Cabinet", "Grill" };
            foreach (string surface in surfaces)
            {
                Material material = VisualFactory.Surface(surface);
                Expect(material != null && material.shader.name == "Universal Render Pipeline/Lit", surface + " uses URP material");
                Texture baseMap = material.GetTexture("_BaseMap");
                Texture normal = material.GetTexture("_BumpMap");
                Expect(baseMap != null && baseMap.width >= 512, surface + " has a baked base-color texture");
                Expect(normal != null && material.IsKeywordEnabled("_NORMALMAP"), surface + " has an active normal map");
                if (normal != null)
                {
                    var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(normal));
                    Expect(importer.textureType == TextureImporterType.NormalMap, surface + " is imported as normal data");
                }
            }
            foreach (IngredientType type in Enum.GetValues(typeof(IngredientType)))
                foreach (IngredientState state in Enum.GetValues(typeof(IngredientState)))
                {
                    GameObject food = VisualFactory.CreateIngredientVisual(type, state);
                    Renderer[] renderers = food.GetComponentsInChildren<Renderer>();
                    Expect(renderers.Length > 0, type + "/" + state + " has a food mesh");
                    Expect(food.GetComponentsInChildren<Collider>().Length == 0, type + "/" + state + " cannot obstruct interaction rays");
                    Bounds bounds = renderers[0].bounds;
                    foreach (Renderer renderer in renderers)
                    {
                        bounds.Encapsulate(renderer.bounds);
                        foreach (Material mat in renderer.sharedMaterials)
                            Expect(mat != null && mat.shader.name == "Universal Render Pipeline/Lit", type + "/" + state + " material is not pink");
                    }
                    Expect(bounds.size.x <= 0.7f && bounds.size.z <= 0.7f && bounds.size.y > 0.01f, type + "/" + state + " fits on the counter");
                    Expect(Mathf.Abs(bounds.min.y) < 0.025f, type + "/" + state + " rests at its base pivot");
                    food.SetActive(false);
                    UnityEngine.Object.Destroy(food);
                }
            GameObject plateModel = VisualFactory.CreateProp("plate-dinner");
            Expect(plateModel.GetComponentsInChildren<Renderer>().Length > 0 && plateModel.GetComponentsInChildren<Collider>().Length == 0, "imported plate is renderable and has no collider");
            plateModel.SetActive(false);
            UnityEngine.Object.Destroy(plateModel);
            Expect(GameObject.Find("Wall_N").GetComponent<Renderer>().sharedMaterial.GetTexture("_BaseMap") != null, "the actual room uses the new wall texture");
            Expect(GameObject.Find("Container_Tomato").transform.Find("Sample").GetComponentInChildren<Renderer>().sharedMaterial.GetTexture("_BaseMap") != null, "the ingredient counter actually uses the food atlas");
            Expect(GameObject.Find("Ingredient shelf") != null && GameObject.Find("Extractor canopy") != null, "shelf and extractor appear in the kitchen");
            foreach (KitchenChaos.Gameplay.CuttingCounter station in UnityEngine.Object.FindObjectsByType<KitchenChaos.Gameplay.CuttingCounter>(FindObjectsSortMode.None))
            {
                Transform board = station.transform.Find("cutting-board");
                float top = float.NegativeInfinity;
                foreach (Renderer renderer in board.GetComponentsInChildren<Renderer>()) top = Mathf.Max(top, renderer.bounds.max.y);
                Expect(station.transform.Find("ItemAnchor").position.y >= top, "thin cut ingredients rest above the cutting board");
            }
            GameObject leaves = Resources.Load<GameObject>("Art/Food/Lettuce_Cut");
            Mesh mesh = leaves.GetComponentInChildren<MeshFilter>().sharedMesh;
            Expect(mesh.tangents.Length == mesh.vertexCount, "lettuce normal map has mesh tangents");
            Transform plateStack = plates.transform.Find("ItemAnchor");
            Expect(plateStack.childCount == plates.PlateCount, "plate stack shows the current stock");
            foreach (Transform disk in plateStack)
                Expect(disk.GetComponentInChildren<Renderer>().sharedMaterial.GetTexture("_BaseMap") != null
                    && disk.GetComponentsInChildren<Collider>().Length == 0, "stocked plates use the imported textured mesh without colliders");
            VerifyPanAlignment();
        }

        private static void VerifyPanAlignment()
        {
            NewRun();
            foreach (KitchenChaos.Gameplay.StoveCounter station in UnityEngine.Object.FindObjectsByType<KitchenChaos.Gameplay.StoveCounter>(FindObjectsSortMode.None))
            {
                Transform pan = station.transform.Find("frying-pan");
                Transform surface = pan.Find("CookingSurface");
                Transform anchor = station.transform.Find("ItemAnchor");
                Expect(surface != null && Vector3.Distance(anchor.position, surface.position) < 0.0001f,
                    station.name + " uses the pan cooking surface, not the bounds including its handle");
                Expect(Vector3.ProjectOnPlane(anchor.position - station.transform.Find("Burner").position, Vector3.up).magnitude < 0.0001f,
                    station.name + " cooking surface is centered on its burner");
                MeshFilter filter = pan.GetComponentInChildren<MeshFilter>();
                var probe = new GameObject("PanSurfaceProbe");
                probe.layer = KitchenChaos.Gameplay.PlayerController.HeldItemLayer;
                try
                {
                    probe.transform.SetPositionAndRotation(filter.transform.position, filter.transform.rotation);
                    probe.transform.localScale = filter.transform.lossyScale;
                    MeshCollider collider = probe.AddComponent<MeshCollider>();
                    collider.sharedMesh = filter.sharedMesh;
                    Physics.SyncTransforms();
                    bool hitPan = collider.Raycast(new Ray(anchor.position + Vector3.up, Vector3.down), out RaycastHit hit, 2f);
                    Expect(hitPan && Vector3.Dot(hit.normal, Vector3.up) > 0.999f,
                        station.name + " food anchor is directly over the flat interior floor");
                    Expect(UseStation(Container(IngredientType.MeatPatty)) && UseStation(station), station.name + " accepts the alignment-test patty");
                    foreach (IngredientState state in new[] { IngredientState.Raw, IngredientState.Cooked, IngredientState.Burned })
                    {
                        if (state == IngredientState.Cooked) station.AdvanceProcessing(IngredientCatalog.Get(IngredientType.MeatPatty).cookTime + 0.01f);
                        if (state == IngredientState.Burned) station.AdvanceProcessing(station.SecondsUntilBurn + 0.01f);
                        var item = anchor.GetComponentInChildren<KitchenChaos.Gameplay.IngredientItem>();
                        Renderer[] renderers = item.GetComponentsInChildren<Renderer>();
                        Bounds bounds = renderers[0].bounds;
                        foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
                        Expect(item.State == state && Vector3.ProjectOnPlane(bounds.center - anchor.position, Vector3.up).magnitude < 0.001f,
                            station.name + " " + state + " patty remains centered over the pan floor");
                        Expect(hitPan && bounds.min.y >= hit.point.y && bounds.min.y - hit.point.y < 0.005f,
                            station.name + " " + state + " patty rests on the pan floor without the old height offset");
                        bool fitsFloor = true;
                        float radius = Mathf.Max(bounds.extents.x, bounds.extents.z);
                        for (int edge = 0; edge < 8; edge++)
                        {
                            float angle = edge * Mathf.PI / 4f;
                            Vector3 origin = anchor.position + Vector3.up + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                            fitsFloor &= collider.Raycast(new Ray(origin, Vector3.down), out RaycastHit edgeHit, 2f)
                                && Mathf.Abs(edgeHit.point.y - hit.point.y) < 0.002f;
                        }
                        Expect(fitsFloor, station.name + " " + state + " patty fits inside the flat pan floor in eight directions");
                    }
                    station.ResetStation();
                }
                finally
                {
                    probe.SetActive(false);
                    UnityEngine.Object.Destroy(probe);
                }
            }
            NewRun();
        }
    }
}
