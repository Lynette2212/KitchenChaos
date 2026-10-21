using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using KitchenChaos.Core;
using KitchenChaos.Data;
using KitchenChaos.Gameplay;

namespace KitchenChaos.EditorTools
{
    /// <summary>
    /// 无头确定性校验：batchmode 下用 -executeMethod KitchenChaos.EditorTools.KitchenSmokeTest.RunBatch
    /// 程序化搭建厨房并断言核心玩法链路，结果写入 Temp/kitchen_smoke_result.txt，
    /// 失败时以非零退出码结束，便于 CI / 命令行判定。
    /// </summary>
    public static class KitchenSmokeTest
    {
        private const string ResultPath = "Temp/kitchen_smoke_result.txt";
        private static readonly List<string> failures = new List<string>();
        private static int checks;

        public static void RunBatch()
        {
            failures.Clear();
            checks = 0;
            Directory.CreateDirectory("Temp");

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject root = new GameObject("KitchenChaos");
            KitchenBuilder builder = root.AddComponent<KitchenBuilder>();
            builder.Build();

            VerifyScene();
            VerifyCookingData();
            VerifyDeliveryAndScore(root);

            string summary = "KITCHEN SMOKE: " + checks + " checks, " + failures.Count + " failures";
            File.WriteAllText(ResultPath,
                summary + "\n" + (failures.Count > 0 ? string.Join("\n", failures) : "ALL PASS") + "\n");
            Debug.Log(summary);

            EditorApplication.Exit(failures.Count == 0 ? 0 : 1);
        }

        private static void VerifyScene()
        {
            Expect(Object.FindFirstObjectByType<GameManager>() != null, "GameManager exists");
            Expect(Object.FindFirstObjectByType<PlayerController>() != null, "Player exists");
            Expect(Object.FindFirstObjectByType<Camera>() != null, "Camera exists");
            Expect(Object.FindFirstObjectByType<GameplayHUD>() != null, "HUD exists");

            Expect(Object.FindObjectsByType<IngredientContainer>(FindObjectsInactive.Exclude).Length == 7,
                "7 ingredient containers");
            Expect(Object.FindObjectsByType<StoveCounter>(FindObjectsInactive.Exclude).Length >= 2,
                ">=2 stoves");
            Expect(Object.FindObjectsByType<CuttingCounter>(FindObjectsInactive.Exclude).Length >= 2,
                ">=2 cutting counters");
            Expect(Object.FindObjectsByType<PlateCounter>(FindObjectsInactive.Exclude).Length == 1,
                "1 plate counter");
            Expect(Object.FindObjectsByType<DeliveryCounter>(FindObjectsInactive.Exclude).Length == 1,
                "1 delivery counter");
            Expect(Object.FindObjectsByType<TrashBin>(FindObjectsInactive.Exclude).Length == 1,
                "1 trash bin");
        }

        private static void VerifyCookingData()
        {
            // 食材加工要求
            Expect(IngredientCatalog.NeedsCut(IngredientType.Tomato), "tomato needs cut");
            Expect(IngredientCatalog.NeedsCook(IngredientType.MeatPatty), "patty needs cook");
            Expect(!IngredientCatalog.NeedsCook(IngredientType.Bun), "bun needs no cook");

            // 配方匹配
            Recipe patty = FindRecipe("香煎肉饼");
            Expect(patty != null, "recipe 香煎肉饼 exists");
            if (patty != null)
            {
                Expect(patty.Matches(new List<Ingredient>
                {
                    new Ingredient(IngredientType.MeatPatty, IngredientState.Cooked)
                }), "cooked patty matches");

                Expect(!patty.Matches(new List<Ingredient>
                {
                    new Ingredient(IngredientType.MeatPatty, IngredientState.Raw)
                }), "raw patty rejected");

                Expect(!patty.Matches(new List<Ingredient>
                {
                    new Ingredient(IngredientType.MeatPatty, IngredientState.Burned)
                }), "burned patty rejected");
            }

            Recipe burger = FindRecipe("芝士汉堡");
            Expect(burger != null, "recipe 芝士汉堡 exists");
            if (burger != null)
            {
                Expect(burger.Matches(new List<Ingredient>
                {
                    new Ingredient(IngredientType.Bun, IngredientState.Raw),
                    new Ingredient(IngredientType.MeatPatty, IngredientState.Cooked),
                    new Ingredient(IngredientType.Cheese, IngredientState.Cut)
                }), "assembled cheeseburger matches (order independent)");
            }
        }

        private static void VerifyDeliveryAndScore(GameObject root)
        {
            GameManager gm = Object.FindFirstObjectByType<GameManager>();
            Expect(gm != null, "GameManager for delivery test");
            if (gm == null) return;

            gm.StartGame();
            Expect(gm.IsPlaying, "game is playing after StartGame");
            Expect(gm.Reputation == gm.startReputation, "reputation initialized");

            // 错误出餐（无订单匹配）扣分但不直接结束整局（声望从 3 开始）
            int repBefore = gm.Reputation;
            gm.TryDeliverDish(new List<Ingredient>
            {
                new Ingredient(IngredientType.Bun, IngredientState.Raw)
            });
            Expect(gm.Reputation == repBefore - 1, "wrong dish costs reputation");

            // 计分链路
            int before = gm.Score.Score;
            gm.Score.RegisterSuccess(100, 1.0f);
            Expect(gm.Score.Score > before && gm.Score.CompletedOrders == 1, "score increases on success");
        }

        private static Recipe FindRecipe(string name)
        {
            foreach (Recipe r in Resources.LoadAll<Recipe>("Recipes"))
            {
                if (r.recipeName == name) return r;
            }
            foreach (Recipe r in RecipeFactory.CreateDefaultRecipes())
            {
                if (r.recipeName == name) return r;
            }
            return null;
        }

        private static void Expect(bool condition, string label)
        {
            checks++;
            if (!condition) failures.Add("FAIL: " + label);
        }
    }
}
