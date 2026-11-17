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
            if (File.Exists(ScenePath))
            {
                MakeSceneEditable();
                return;
            }
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (Resources.LoadAll<Recipe>("Recipes").Length == 0) CreateRecipeAssets();
            Directory.CreateDirectory("Assets/Scenes");
            AssetDatabase.Refresh();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            KitchenBuilder builder = new GameObject("KitchenChaos").AddComponent<KitchenBuilder>();
            SaveEditableScene(scene, builder);
        }

        [MenuItem("Tools/Kitchen Chaos/Make Scene Editable")]
        public static void MakeSceneEditable()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new System.InvalidOperationException("Exit Play Mode before editing the saved kitchen layout.");
            if (!File.Exists(ScenePath))
            {
                CreateKitchenScene();
                return;
            }
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            KitchenBuilder builder = null;
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.TryGetComponent(out KitchenBuilder candidate)) { builder = candidate; break; }
            if (builder == null) throw new System.InvalidOperationException("The kitchen scene has no KitchenBuilder root.");
            if (builder.HasSceneLayout)
            {
                Selection.activeGameObject = builder.gameObject;
                Debug.Log("Kitchen scene is already editable; existing layout was preserved.");
                return;
            }
            string backupFolder = Path.Combine("Backups", "SceneAuthoring");
            Directory.CreateDirectory(backupFolder);
            string backupPath = Path.Combine(backupFolder, "KitchenChaos-" + System.DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".unity");
            File.Copy(ScenePath, backupPath);
            Debug.Log("Original scene backup: " + Path.GetFullPath(backupPath));
            SaveEditableScene(scene, builder);
        }

        private static void SaveEditableScene(Scene scene, KitchenBuilder builder)
        {
            builder.BuildSceneObjects();
            PersistSceneResources(builder.gameObject);
            foreach (Transform child in builder.GetComponentsInChildren<Transform>(true))
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) != 0)
                    throw new System.InvalidOperationException("Missing script on " + child.name);
            EditorUtility.SetDirty(builder);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new System.IO.IOException("Unable to save " + ScenePath);
            AddSceneToBuildSettings(ScenePath);
            Selection.activeGameObject = builder.gameObject;
            if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected();
            Debug.Log("KITCHEN EDITABLE SCENE: saved " + ScenePath + " with "
                + builder.GetComponentsInChildren<Renderer>(true).Length + " renderers.");
        }

        private static void PersistSceneResources(GameObject root)
        {
            const string folder = "Assets/SceneResources/KitchenChaos";
            Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
            var assets = new Dictionary<UnityEngine.Object, UnityEngine.Object>();
            T Persist<T>(T source, string extension) where T : UnityEngine.Object
            {
                if (source == null || EditorUtility.IsPersistent(source)) return source;
                if (assets.TryGetValue(source, out UnityEngine.Object saved)) return (T)saved;
                T copy = UnityEngine.Object.Instantiate(source);
                copy.name = source.name;
                copy.hideFlags = HideFlags.None;
                string name = string.Join("_", source.name.Split(Path.GetInvalidFileNameChars()));
                string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + assets.Count.ToString("000") + "_" + name + extension);
                AssetDatabase.CreateAsset(copy, path);
                assets.Add(source, copy);
                return copy;
            }
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material source = materials[i];
                    Material saved = Persist(source, ".mat");
                    if (saved != null && saved != source)
                    {
                        foreach (string property in saved.GetTexturePropertyNames())
                            saved.SetTexture(property, Persist(saved.GetTexture(property), ".asset"));
                        EditorUtility.SetDirty(saved);
                    }
                    materials[i] = saved;
                }
                renderer.sharedMaterials = materials;
                EditorUtility.SetDirty(renderer);
                if (renderer is SkinnedMeshRenderer skinned)
                    skinned.sharedMesh = Persist(skinned.sharedMesh, ".asset");
            }
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                filter.sharedMesh = Persist(filter.sharedMesh, ".asset");
                EditorUtility.SetDirty(filter);
            }
            AssetDatabase.SaveAssets();
            foreach (UnityEngine.Object temporary in assets.Keys)
                if (temporary != null) UnityEngine.Object.DestroyImmediate(temporary);
            KitchenChaos.Visual.VisualFactory.ReleaseMaterials();
        }

        [MenuItem("Tools/Kitchen Chaos/Open Kitchen Scene")]
        public static void OpenKitchenScene()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(ScenePath)) CreateKitchenScene();
            else EditorSceneManager.OpenScene(ScenePath);
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
                if (root.GetComponent<KitchenBuilder>() != null) { Selection.activeGameObject = root; break; }
            EditorWindow.FocusWindowIfItsOpen<SceneView>();
            if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected();
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
