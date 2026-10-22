using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using KitchenChaos.Core;
using KitchenChaos.Data;
using KitchenChaos.Gameplay;
using KitchenChaos.Visual;

namespace KitchenChaos.EditorTools
{
    /// <summary>
    /// 可视化验收：在编辑器 Play Mode 中驱动真实交互，并在关键帧截图，
    /// 用于检查粒子、弹跳、屏幕震动、飘字与连击横幅。仅编辑器使用。
    /// </summary>
    [InitializeOnLoad]
    public static class KitchenVisualCapture
    {
        private const string SessionKey = "KitchenChaos.VisualCapture.Running";
        private const string Folder = "Logs/visual";

        static KitchenVisualCapture()
        {
            if (SessionState.GetBool(SessionKey, false)) EditorApplication.update += StartWhenReady;
        }

        public static void Run()
        {
            Directory.CreateDirectory(Folder);
            SessionState.SetBool(SessionKey, true);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("KitchenChaos").AddComponent<KitchenBuilder>();
            System.Type gameView = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            if (gameView != null) EditorWindow.GetWindow(gameView).Focus();
            EditorApplication.isPlaying = true;
        }

        private static void StartWhenReady()
        {
            if (!EditorApplication.isPlaying || GameManager.Instance == null) return;
            EditorApplication.update -= StartWhenReady;
            new GameObject("VisualCaptureRunner").AddComponent<VisualCaptureRunner>();
        }
    }

    public sealed class VisualCaptureRunner : MonoBehaviour
    {
        private GameManager gm;
        private PlayerController player;
        private CuttingCounter cutter;
        private StoveCounter stove;
        private PlateCounter plates;
        private DeliveryCounter delivery;

        private IEnumerator Start()
        {
            Directory.CreateDirectory("Logs/visual");
            while (GameManager.Instance == null || Time.frameCount < 2) yield return null;
            gm = GameManager.Instance;
            gm.SaveRecords = false;
            gm.ReturnToMenu();
            gm.StartGame();
            gm.Orders.StopSpawning();
            gm.Orders.ClearOrders();
            player = Object.FindAnyObjectByType<PlayerController>();
            cutter = Object.FindObjectsByType<CuttingCounter>()[0];
            stove = Object.FindObjectsByType<StoveCounter>()[0];
            plates = Object.FindAnyObjectByType<PlateCounter>();
            delivery = Object.FindAnyObjectByType<DeliveryCounter>();

            yield return new WaitForSeconds(1.2f);
            yield return Shot("01-kitchen");

            FeedbackFX.Confetti(player.transform.position + Vector3.up * 1.4f, 40);
            FeedbackFX.Burst(player.transform.position + Vector3.up * 1.2f, new Color(1f, 0.82f, 0.3f), 24, 3.2f, 0.12f, 5f, 1.1f);
            FeedbackFX.Popup(player.transform.position + Vector3.up * 2.1f, "色香味俱全！", new Color(1f, 0.86f, 0.4f), true);
            FeedbackFX.Shake(0.22f, 0.45f);
            yield return new WaitForSeconds(0.1f);
            yield return Shot("02-particle-burst");
            yield return new WaitForSeconds(1.8f);

            IngredientContainer tomato = FindContainer(IngredientType.Tomato);
            Teleport(tomato.transform.position + new Vector3(0f, 0f, -1.2f));
            yield return new WaitForSeconds(0.7f);
            tomato.Interact(player);
            yield return new WaitForSeconds(0.1f);
            yield return Shot("03-pickup");
            yield return new WaitForSeconds(1.4f);

            Teleport(cutter.transform.position + new Vector3(0f, 0f, 1.2f));
            yield return new WaitForSeconds(0.65f);
            Focus(cutter);
            cutter.Interact(player);
            yield return new WaitForSeconds(0.15f);
            cutter.AdvanceProcessing(0.21f);
            yield return new WaitForSeconds(0.06f);
            yield return Shot("04-chopping");
            cutter.AdvanceProcessing(2.5f);
            yield return new WaitForSeconds(0.08f);
            yield return Shot("05-cut-done");
            yield return new WaitForSeconds(1.8f);

            IngredientContainer patty = FindContainer(IngredientType.MeatPatty);
            patty.Interact(player);
            Teleport(stove.transform.position + new Vector3(0f, 0f, 1.35f));
            yield return new WaitForSeconds(0.6f);
            Focus(stove);
            stove.Interact(player);
            yield return new WaitForSeconds(0.15f);
            stove.AdvanceProcessing(3.05f);
            yield return new WaitForSeconds(0.08f);
            yield return Shot("06-cooked");
            yield return new WaitForSeconds(1.8f);
            stove.AdvanceProcessing(5.5f);
            yield return new WaitForSeconds(0.08f);
            yield return Shot("07-burn-warning");
            yield return new WaitForSeconds(1.8f);
            stove.AdvanceProcessing(3.2f);
            yield return new WaitForSeconds(0.08f);
            yield return Shot("08-burned");
            yield return new WaitForSeconds(1.4f);

            gm.Orders.ClearOrders();
            Recipe recipe = gm.Orders.AvailableRecipes[0];
            plates.Interact(player);
            FillPlate(recipe);
            gm.Orders.ActiveOrders.Add(new Order(recipe, 90f));
            Teleport(delivery.transform.position + new Vector3(0f, 0f, 1.35f));
            yield return new WaitForSeconds(0.8f);
            delivery.Interact(player);
            yield return new WaitForSeconds(0.1f);
            yield return Shot("09-delivery");

            FillPlate(recipe);
            gm.Orders.ActiveOrders.Add(new Order(recipe, 90f));
            delivery.Interact(player);
            yield return new WaitForSeconds(0.35f);
            yield return Shot("10-combo-banner");

            player.ResetForDay();
            Teleport(new Vector3(0f, 0.05f, 0.6f));
            yield return new WaitForSeconds(0.5f);
            player.AdvanceMovement(Vector2.right, true, 0.05f);
            yield return new WaitForSeconds(0.06f);
            yield return Shot("11-dash");

            Debug.Log("VISUAL CAPTURE: done");
            yield return new WaitForSeconds(0.4f);
            SessionState.SetBool("KitchenChaos.VisualCapture.Running", false);
            EditorApplication.Exit(0);
        }

        private void FillPlate(Recipe recipe)
        {
            foreach (RecipeIngredient item in recipe.ingredients)
                player.Plate.Add(new Ingredient(item.type, item.requiredState));
        }

        private void Teleport(Vector3 position)
        {
            position.y = 0.05f;
            CharacterController controller = player.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            player.transform.position = position;
            if (controller != null) controller.enabled = true;
            Physics.SyncTransforms();
        }

        private void Focus(MonoBehaviour station)
        {
            Vector3 direction = station.transform.position - player.transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f) direction = Vector3.forward;
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(PlayerController).GetField("facingDirection", flags).SetValue(player, direction.normalized);
            typeof(PlayerController).GetMethod("FindTarget", flags).Invoke(player, null);
        }

        private static IngredientContainer FindContainer(IngredientType type)
        {
            foreach (IngredientContainer container in Object.FindObjectsByType<IngredientContainer>())
                if (container.IngredientType == type) return container;
            return null;
        }

        private static IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot("Logs/visual/" + name + ".png");
            yield return new WaitForEndOfFrame();
            yield return new WaitForSeconds(0.25f);
        }
    }
}
