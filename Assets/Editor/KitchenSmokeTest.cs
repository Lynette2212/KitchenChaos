using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using KitchenChaos.Core;
using KitchenChaos.Data;
using KitchenChaos.Gameplay;

namespace KitchenChaos.EditorTools
{
    [InitializeOnLoad]
    public static class KitchenSmokeTest
    {
        private const string ActiveKey = "KitchenChaos.SmokeTest.Active";
        private const string StartKey = "KitchenChaos.SmokeTest.Start";

        private static double playStartTime;
        private static double actionTime;
        private static int phase;

        static KitchenSmokeTest()
        {
            if (SessionState.GetBool(ActiveKey, false))
            {
                EditorApplication.update += Tick;
            }
        }

        public static void Run()
        {
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetFloat(StartKey, (float)EditorApplication.timeSinceStartup);
            EditorSceneManager.OpenScene(KitchenSetup.ScenePath);
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.isPlaying = true;
        }

        private static void Tick()
        {
            float startTime = SessionState.GetFloat(StartKey, 0f);
            if (startTime > 0f && EditorApplication.timeSinceStartup - startTime > 45.0)
            {
                Debug.LogError("KITCHEN SMOKE: timeout");
                Finish(1);
                return;
            }

            if (!EditorApplication.isPlaying) return;

            if (playStartTime <= 0.0) playStartTime = EditorApplication.timeSinceStartup;
            if (EditorApplication.timeSinceStartup - playStartTime < 3.0) return;

            GameManager gameManager = GameManager.Instance;
            if (gameManager == null)
            {
                Debug.LogError("KITCHEN SMOKE: GameManager.Instance is null");
                Finish(1);
                return;
            }

            if (phase == 0) RunInitialChecks(gameManager);
            else if (phase == 1) RunCookingChecks(gameManager);
        }

        private static void RunInitialChecks(GameManager gameManager)
        {
            int stations = Object.FindObjectsByType<CookingStation>(FindObjectsSortMode.None).Length;
            int bins = Object.FindObjectsByType<IngredientItem>(FindObjectsSortMode.None).Length;
            int orders = gameManager.Orders != null ? gameManager.Orders.ActiveOrders.Count : 0;

            bool ok = gameManager.IsPlaying
                      && stations == 3
                      && bins == 4
                      && orders == 3
                      && gameManager.Timer != null
                      && gameManager.Score != null;

            Debug.Log("KITCHEN SMOKE: initial ok=" + ok
                      + ", playing=" + gameManager.IsPlaying
                      + ", stations=" + stations
                      + ", bins=" + bins
                      + ", orders=" + orders);

            if (!ok)
            {
                Finish(1);
                return;
            }

            CookingStation station = Object.FindObjectsByType<CookingStation>(FindObjectsSortMode.None)[0];
            bool added = station.TryAddIngredient(IngredientType.Tomato);
            Debug.Log("KITCHEN SMOKE: station add=" + added);

            phase = 1;
            actionTime = EditorApplication.timeSinceStartup;
        }

        private static void RunCookingChecks(GameManager gameManager)
        {
            if (EditorApplication.timeSinceStartup - actionTime < 3.0) return;

            CookingStation station = Object.FindObjectsByType<CookingStation>(FindObjectsSortMode.None)[0];
            bool ready = station.IsReady;
            bool took = station.TryTakeCooked(out IngredientType cookedType);
            bool cookOk = ready && took && cookedType == IngredientType.Tomato;

            bool deliveryOk = false;
            int scoreAfter = 0;
            int completedAfter = 0;

            if (gameManager.Orders != null && gameManager.Orders.ActiveOrders.Count > 0)
            {
                Order order = gameManager.Orders.ActiveOrders[0];
                if (order != null && order.recipe != null)
                {
                    List<IngredientType> required = new List<IngredientType>(order.recipe.ingredients);
                    foreach (IngredientType type in required)
                    {
                        gameManager.Deliver(order, type);
                    }

                    deliveryOk = gameManager.Score.CompletedOrders >= 1 && gameManager.Score.Score > 0;
                    scoreAfter = gameManager.Score.Score;
                    completedAfter = gameManager.Score.CompletedOrders;
                }
            }

            Debug.Log("KITCHEN SMOKE: cook ok=" + cookOk
                      + ", ready=" + ready
                      + ", cookedType=" + cookedType
                      + ", deliveryOk=" + deliveryOk
                      + ", score=" + scoreAfter
                      + ", completed=" + completedAfter);

            Finish(cookOk && deliveryOk ? 0 : 1);
        }

        private static void Finish(int exitCode)
        {
            SessionState.SetBool(ActiveKey, false);
            EditorApplication.update -= Tick;
            Debug.Log("KITCHEN SMOKE: done exit=" + exitCode);
            EditorApplication.Exit(exitCode);
        }
    }
}
