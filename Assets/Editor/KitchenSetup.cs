using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using KitchenChaos.Core;
using KitchenChaos.Data;

namespace KitchenChaos.EditorTools
{
    public static class KitchenSetup
    {
        public const string ScenePath = "Assets/Scenes/KitchenChaos.unity";
        private const string RecipeFolder = "Assets/Resources/Recipes";

        [MenuItem("Tools/Kitchen Chaos/Create Kitchen Scene")]
        public static void CreateKitchenScene()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            CreateSampleRecipes();
            Directory.CreateDirectory("Assets/Scenes");
            AssetDatabase.Refresh();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject root = new GameObject("KitchenChaos");
            root.AddComponent<SetupGuide>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("Kitchen Chaos scene created: " + ScenePath);
        }

        [MenuItem("Tools/Kitchen Chaos/Open Kitchen Scene")]
        public static void OpenKitchenScene()
        {
            if (!File.Exists(ScenePath))
            {
                CreateKitchenScene();
            }

            EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Tools/Kitchen Chaos/Create Sample Recipes")]
        public static void CreateSampleRecipes()
        {
            Directory.CreateDirectory(RecipeFolder);
            AssetDatabase.Refresh();

            CreateRecipeAsset("Salad", "番茄沙拉", 100, IngredientType.Tomato, IngredientType.Lettuce);
            CreateRecipeAsset("CheeseTomato", "芝士番茄", 120, IngredientType.Cheese, IngredientType.Tomato);
            CreateRecipeAsset("MeatPlate", "香煎肉盘", 80, IngredientType.Meat);
            CreateRecipeAsset("DeluxePlate", "豪华拼盘", 200, IngredientType.Meat, IngredientType.Tomato, IngredientType.Cheese);
            CreateRecipeAsset("GardenWrap", "田园卷", 130, IngredientType.Lettuce, IngredientType.Cheese);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Sample recipes created in " + RecipeFolder);
        }

        private static void CreateRecipeAsset(string fileName, string displayName, int score, params IngredientType[] ingredients)
        {
            string path = RecipeFolder + "/" + fileName + ".asset";
            if (AssetDatabase.LoadAssetAtPath<Recipe>(path) != null) return;

            Recipe recipe = ScriptableObject.CreateInstance<Recipe>();
            recipe.recipeName = displayName;
            recipe.score = score;
            recipe.ingredients = new List<IngredientType>(ingredients);
            AssetDatabase.CreateAsset(recipe, path);
        }

        private static void AddSceneToBuildSettings(string scenePath)
        {
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            for (int i = 0; i < scenes.Count; i++)
            {
                if (scenes[i].path == scenePath)
                {
                    scenes[i] = new EditorBuildSettingsScene(scenePath, true);
                    EditorBuildSettings.scenes = scenes.ToArray();
                    return;
                }
            }

            scenes.Insert(0, new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
