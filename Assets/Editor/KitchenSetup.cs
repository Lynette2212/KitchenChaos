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
    /// <summary>
    /// 一键生成厨房场景与配方资产，避免手工在 Inspector 中搭建。
    /// </summary>
    public static class KitchenSetup
    {
        public const string ScenePath = "Assets/Scenes/KitchenChaos.unity";
        private const string RecipeFolder = "Assets/Resources/Recipes";

        [MenuItem("Tools/Kitchen Chaos/Create Kitchen Scene")]
        public static void CreateKitchenScene()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            CreateRecipeAssets();
            Directory.CreateDirectory("Assets/Scenes");
            AssetDatabase.Refresh();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject root = new GameObject("KitchenChaos");
            root.AddComponent<KitchenBuilder>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("Kitchen Chaos scene created: " + ScenePath);
        }

        [MenuItem("Tools/Kitchen Chaos/Open Kitchen Scene")]
        public static void OpenKitchenScene()
        {
            if (!File.Exists(ScenePath)) CreateKitchenScene();
            else EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Tools/Kitchen Chaos/Create Recipe Assets")]
        public static void CreateRecipeAssets()
        {
            Directory.CreateDirectory(RecipeFolder);
            AssetDatabase.Refresh();

            // 先清空旧的简易配方，避免菜单重复
            string[] old = Directory.GetFiles(RecipeFolder, "*.asset");
            foreach (string f in old) AssetDatabase.DeleteAsset(f.Replace("\\", "/"));

            CreateRecipe("CheeseBurger", "芝士汉堡", 120,
                (IngredientType.Bun, IngredientState.Raw),
                (IngredientType.MeatPatty, IngredientState.Cooked),
                (IngredientType.Cheese, IngredientState.Cut));

            CreateRecipe("UltimateBurger", "至尊汉堡", 180,
                (IngredientType.Bun, IngredientState.Raw),
                (IngredientType.MeatPatty, IngredientState.Cooked),
                (IngredientType.Cheese, IngredientState.Cut),
                (IngredientType.Tomato, IngredientState.Cut),
                (IngredientType.Lettuce, IngredientState.Cut));

            CreateRecipe("GardenSalad", "田园沙拉", 90,
                (IngredientType.Lettuce, IngredientState.Cut),
                (IngredientType.Tomato, IngredientState.Cut),
                (IngredientType.Onion, IngredientState.Cut));

            CreateRecipe("PanFriedPatty", "香煎肉饼", 80,
                (IngredientType.MeatPatty, IngredientState.Cooked));

            CreateRecipe("MushroomBurger", "蘑菇芝士堡", 150,
                (IngredientType.Bun, IngredientState.Raw),
                (IngredientType.Mushroom, IngredientState.Cooked),
                (IngredientType.Cheese, IngredientState.Cut));

            CreateRecipe("MushroomTomato", "番茄蘑菇盘", 110,
                (IngredientType.Mushroom, IngredientState.Cooked),
                (IngredientType.Tomato, IngredientState.Cut));

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Recipe assets created in " + RecipeFolder);
        }

        private static void CreateRecipe(string fileName, string displayName, int score,
            params (IngredientType type, IngredientState state)[] items)
        {
            string path = RecipeFolder + "/" + fileName + ".asset";
            Recipe recipe = ScriptableObject.CreateInstance<Recipe>();
            recipe.recipeName = displayName;
            recipe.score = score;
            recipe.ingredients = new List<RecipeIngredient>();
            foreach ((IngredientType type, IngredientState state) it in items)
            {
                recipe.ingredients.Add(new RecipeIngredient { type = it.type, requiredState = it.state });
            }
            AssetDatabase.CreateAsset(recipe, path);
        }

        private static void AddSceneToBuildSettings(string scenePath)
        {
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == scenePath);
            scenes.Insert(0, new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
