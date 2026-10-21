using System.Collections.Generic;
using UnityEngine;
using KitchenChaos.Data;
using KitchenChaos.Gameplay;
using KitchenChaos.UI;

namespace KitchenChaos.Core
{
    public class SetupGuide : MonoBehaviour
    {
        public float StationCookTime = 2.5f;

        private bool built;

        private void Start()
        {
            BuildKitchen();
        }

        public void BuildKitchen()
        {
            if (built) return;
            built = true;

            Camera kitchenCamera = CreateCamera();
            CreateLight();
            CreateEnvironment();

            List<IngredientItem> bins = CreateBins();
            List<CookingStation> stations = CreateStations();

            GameManager gameManager = gameObject.AddComponent<GameManager>();
            TimerSystem timer = gameObject.AddComponent<TimerSystem>();
            OrderSystem orders = gameObject.AddComponent<OrderSystem>();
            ScoreManager score = gameObject.AddComponent<ScoreManager>();

            GameObject gameUIObject = new GameObject("GameUI");
            gameUIObject.transform.SetParent(transform, false);
            GameUI gameUI = gameUIObject.AddComponent<GameUI>();

            GameObject scoreUIObject = new GameObject("ScoreUI");
            scoreUIObject.transform.SetParent(transform, false);
            ScoreUI scoreUI = scoreUIObject.AddComponent<ScoreUI>();

            GameObject orderPanelObject = new GameObject("OrderPanel");
            orderPanelObject.transform.SetParent(transform, false);
            OrderPanelManager orderPanel = orderPanelObject.AddComponent<OrderPanelManager>();

            DragAndDrop drag = gameObject.AddComponent<DragAndDrop>();

            List<Recipe> recipes = new List<Recipe>(Resources.LoadAll<Recipe>("Recipes"));
            if (recipes.Count == 0)
            {
                recipes = CreateDefaultRecipes();
            }

            orders.Initialize(recipes);
            orderPanel.Initialize(orders, gameManager);
            scoreUI.Initialize(score);
            drag.Initialize(kitchenCamera, orderPanel, gameManager);
            gameUI.Initialize(gameManager, stations, bins, kitchenCamera);
            gameManager.Initialize(timer, orders, score, drag, gameUI);
        }

        private Camera CreateCamera()
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(transform, false);
            cameraObject.transform.position = new Vector3(0f, 12f, -11f);
            cameraObject.transform.LookAt(new Vector3(0f, 0f, 0f));

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 8f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.07f, 0.08f, 0.1f);
            cameraObject.AddComponent<AudioListener>();
            
            Debug.Log("[Camera] Created at: " + cameraObject.transform.position);
            Debug.Log("[Camera] Looking at: (0, 0, 0)");
            Debug.Log("[Camera] Forward: " + cameraObject.transform.forward);
            Debug.Log("[Camera] Orthographic size: " + camera.orthographicSize);
            Debug.Log("[Camera] Camera count in scene: " + Camera.allCamerasCount);
            
            return camera;
        }

        private void CreateLight()
        {
            GameObject lightObject = new GameObject("Directional Light");
            lightObject.transform.SetParent(transform, false);
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            Light directionalLight = lightObject.AddComponent<Light>();
            directionalLight.type = LightType.Directional;
            directionalLight.intensity = 1.2f;
            directionalLight.color = new Color(1f, 0.95f, 0.85f);

            RenderSettings.ambientLight = new Color(0.35f, 0.35f, 0.4f);
        }

        private void CreateEnvironment()
        {
            CreateBox("Floor", new Vector3(0f, -0.25f, 0f), new Vector3(24f, 0.5f, 16f),
                new Color(0.14f, 0.15f, 0.18f), transform);
            CreateBox("BackCounter", new Vector3(0f, 0.35f, 4.5f), new Vector3(22f, 0.7f, 1.2f),
                new Color(0.2f, 0.21f, 0.24f), transform);
        }

        private List<IngredientItem> CreateBins()
        {
            IngredientType[] types = { IngredientType.Tomato, IngredientType.Lettuce, IngredientType.Cheese, IngredientType.Meat };
            float[] positions = { -7f, -3.5f, 0f, 3.5f };
            List<IngredientItem> bins = new List<IngredientItem>();

            for (int i = 0; i < types.Length; i++)
            {
                Color color = Color.Lerp(Recipe.GetIngredientColor(types[i]), Color.black, 0.15f);
                GameObject bin = CreateBox("Bin_" + types[i], new Vector3(positions[i], 0.4f, -4.5f),
                    new Vector3(1.9f, 0.8f, 1.4f), color, transform);

                IngredientItem item = bin.AddComponent<IngredientItem>();
                item.Type = types[i];
                item.IsSource = true;
                bins.Add(item);
            }

            return bins;
        }

        private List<CookingStation> CreateStations()
        {
            float[] positions = { -6f, -1f, 4f };
            List<CookingStation> stations = new List<CookingStation>();

            for (int i = 0; i < positions.Length; i++)
            {
                GameObject stationObject = CreateBox("Station_" + (i + 1), new Vector3(positions[i], 0.4f, 1.5f),
                    new Vector3(2.4f, 0.8f, 2.2f), new Color(0.35f, 0.36f, 0.4f), transform);

                CookingStation station = stationObject.AddComponent<CookingStation>();
                station.StationName = "灶台 " + (char)('A' + i);
                station.CookTime = StationCookTime;
                stations.Add(station);
            }

            return stations;
        }

        private static List<Recipe> CreateDefaultRecipes()
        {
            return new List<Recipe>
            {
                CreateRecipe("番茄沙拉", 100, IngredientType.Tomato, IngredientType.Lettuce),
                CreateRecipe("芝士番茄", 120, IngredientType.Cheese, IngredientType.Tomato),
                CreateRecipe("香煎肉盘", 80, IngredientType.Meat),
                CreateRecipe("豪华拼盘", 200, IngredientType.Meat, IngredientType.Tomato, IngredientType.Cheese),
                CreateRecipe("田园卷", 130, IngredientType.Lettuce, IngredientType.Cheese)
            };
        }

        private static Recipe CreateRecipe(string recipeName, int score, params IngredientType[] ingredients)
        {
            Recipe recipe = ScriptableObject.CreateInstance<Recipe>();
            recipe.name = recipeName;
            recipe.recipeName = recipeName;
            recipe.score = score;
            recipe.ingredients = new List<IngredientType>(ingredients);
            return recipe;
        }

        private static GameObject CreateBox(string objectName, Vector3 position, Vector3 scale, Color color, Transform parent)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = objectName;
            if (parent != null) box.transform.SetParent(parent, false);
            box.transform.position = position;
            box.transform.localScale = scale;

            Renderer targetRenderer = box.GetComponent<Renderer>();
            if (targetRenderer != null)
            {
                Material material = new Material(Shader.Find("Unlit/Color"));
                material.color = color;
                targetRenderer.material = material;
            }
            else
            {
                Debug.LogError("No Renderer found on " + objectName);
            }

            return box;
        }
    }
}