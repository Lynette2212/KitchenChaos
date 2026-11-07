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
                Transform griddle = station.transform.Find("Griddle");
                Transform anchor = station.transform.Find("ItemAnchor");
                Expect(griddle != null && anchor != null, station.name + " has a flat griddle instead of a pan");
                Expect(anchor.position.y >= griddle.GetComponent<Renderer>().bounds.max.y - 0.001f, station.name + " food sits on the griddle");
                Expect(UseStation(Container(IngredientType.MeatPatty)) && UseStation(station), station.name + " accepts a patty");
                Expect(station.OccupiedCount == 1, station.name + " holds the first patty");
                Expect(UseStation(Container(IngredientType.MeatPatty)) && UseStation(station) && station.OccupiedCount == 2, station.name + " accepts a second patty beside the first");
                station.ResetStation();
                Expect(station.OccupiedCount == 0 && station.PlacedIngredient == null, station.name + " clears every griddle slot");
                Expect(UseStation(Container(IngredientType.MeatPatty)) && UseStation(station), station.name + " accepts one more patty for the burn check");
                station.AdvanceProcessing(IngredientCatalog.Get(IngredientType.MeatPatty).cookTime);
                Expect(station.IsCooked && station.SecondsUntilBurn >= 4f && station.SecondsUntilBurn <= 6f, station.name + " burns a cooked patty within about four to six seconds");
                station.AdvanceProcessing(station.SecondsUntilBurn + 0.05f);
                Expect(station.IsBurned, station.name + " actually chars the patty");
                station.ResetStation();
            }
            NewRun();
        }
    }
}
