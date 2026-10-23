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
using KitchenChaos.Audio;

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
            if (Application.isBatchMode || UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
            {
                Debug.LogError("Visual capture needs an interactive Editor with no unsaved scene changes.");
                return;
            }
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
        private CameraFollow view;
        private float mouseSensitivity;
        private CuttingCounter cutter;
        private StoveCounter stove;
        private PlateCounter plates;
        private DeliveryCounter delivery;
        private int errors;

        private IEnumerator Start()
        {
            Application.logMessageReceived += CaptureError;
            Directory.CreateDirectory("Logs/visual");
            while (GameManager.Instance == null || Time.frameCount < 2) yield return null;
            gm = GameManager.Instance;
            gm.SaveRecords = false;
            gm.ReturnToMenu();
            gm.StartGame();
            gm.Orders.StopSpawning();
            gm.Orders.ClearOrders();
            player = Object.FindAnyObjectByType<PlayerController>();
            view = player.ViewCamera.GetComponent<CameraFollow>();
            mouseSensitivity = view.mouseSensitivity;
            view.mouseSensitivity = 0f;
            cutter = Object.FindObjectsByType<CuttingCounter>()[0];
            stove = Object.FindObjectsByType<StoveCounter>()[0];
            plates = Object.FindAnyObjectByType<PlateCounter>();
            delivery = Object.FindAnyObjectByType<DeliveryCounter>();

            yield return VerifyAudioPlayback();
            yield return new WaitForSeconds(1.2f);
            yield return Shot("01-kitchen");

            view.RefreshPose();
            Vector3 effectPosition = player.ViewCamera.transform.position + player.ViewCamera.transform.forward * 1.8f;
            FeedbackFX.Confetti(effectPosition, 40);
            FeedbackFX.Burst(effectPosition, new Color(1f, 0.82f, 0.3f), 24, 3.2f, 0.12f, 5f, 1.1f);
            FeedbackFX.Popup(effectPosition + player.ViewCamera.transform.up * 0.45f, "色香味俱全！", new Color(1f, 0.86f, 0.4f), true);
            FeedbackFX.Shake(0.22f, 0.45f);
            yield return new WaitForSeconds(0.1f);
            yield return Shot("02-particle-burst");
            yield return new WaitForSeconds(1.8f);

            IngredientContainer tomato = FindContainer(IngredientType.Tomato);
            Teleport(tomato.transform.position + new Vector3(0f, 0f, -1.2f));
            Focus(tomato);
            yield return new WaitForSeconds(0.7f);
            Focus(tomato);
            tomato.Interact(player);
            yield return new WaitForSeconds(0.1f);
            yield return Shot("03-pickup", tomato);
            yield return new WaitForSeconds(1.4f);

            Teleport(cutter.transform.position + new Vector3(0f, 0f, 1.2f));
            Focus(cutter);
            yield return new WaitForSeconds(0.65f);
            Focus(cutter);
            cutter.Interact(player);
            yield return new WaitForSeconds(0.15f);
            yield return Shot("03a-stored", cutter);
            Focus(cutter);
            cutter.TakeBack(player);
            yield return new WaitForSeconds(0.1f);
            yield return Shot("03b-retrieved", cutter);
            Focus(cutter);
            cutter.Interact(player);
            yield return new WaitForSeconds(0.15f);
            Focus(cutter);
            cutter.AdvanceProcessing(0.21f);
            yield return new WaitForSeconds(0.06f);
            yield return Shot("04-chopping", cutter);
            Focus(cutter);
            cutter.AdvanceProcessing(2.5f);
            yield return new WaitForSeconds(0.08f);
            yield return Shot("05-cut-done", cutter);
            yield return new WaitForSeconds(1.8f);

            IngredientContainer patty = FindContainer(IngredientType.MeatPatty);
            Teleport(patty.transform.position + new Vector3(0f, 0f, -1.2f));
            Focus(patty);
            patty.Interact(player);
            Teleport(stove.transform.position + new Vector3(0f, 0f, 1.35f));
            Focus(stove);
            yield return new WaitForSeconds(0.6f);
            Focus(stove);
            stove.Interact(player);
            yield return new WaitForSeconds(0.15f);
            Focus(stove);
            stove.AdvanceProcessing(3.05f);
            yield return new WaitForSeconds(0.08f);
            yield return Shot("06-cooked", stove);
            yield return new WaitForSeconds(0.8f);
            GameplayHUD hud = Object.FindAnyObjectByType<GameplayHUD>();
            hud.enabled = false;
            yield return Shot("06a-pan-alignment", stove);
            hud.enabled = true;
            yield return new WaitForSeconds(0.8f);
            Focus(stove);
            stove.AdvanceProcessing(Mathf.Max(0f, stove.SecondsUntilBurn - 3f));
            yield return new WaitForSeconds(0.08f);
            yield return Shot("07-burn-warning", stove);
            view.SetViewAngles(player.transform.eulerAngles.y + 180f, 0f);
            yield return null;
            yield return Shot("07a-offscreen-stove-warning");
            yield return new WaitForSeconds(1.8f);
            Focus(stove);
            stove.AdvanceProcessing(3.2f);
            yield return new WaitForSeconds(0.08f);
            yield return Shot("08-burned", stove);
            yield return new WaitForSeconds(1.4f);

            gm.Orders.ClearOrders();
            Recipe recipe = gm.Orders.AvailableRecipes[0];
            Teleport(plates.transform.position - new Vector3(plates.transform.position.x, 0f, plates.transform.position.z).normalized * 1.35f);
            Focus(plates);
            plates.Interact(player);
            FillPlate(recipe);
            yield return Shot("08a-held-plate", plates);
            gm.Orders.ActiveOrders.Add(new Order(recipe, 90f));
            Teleport(delivery.transform.position + new Vector3(-1.35f, 0f, 0f));
            Focus(delivery);
            yield return new WaitForSeconds(0.8f);
            Focus(delivery);
            delivery.Interact(player);
            yield return new WaitForSeconds(0.1f);
            yield return Shot("09-delivery", delivery);

            Focus(delivery);
            FillPlate(recipe);
            gm.Orders.ActiveOrders.Add(new Order(recipe, 90f));
            Focus(delivery);
            delivery.Interact(player);
            yield return new WaitForSeconds(0.35f);
            yield return Shot("10-combo-banner", delivery);

            player.ResetForDay();
            Teleport(new Vector3(0f, 0.05f, 0.6f));
            yield return new WaitForSeconds(0.5f);
            view.SetViewAngles(0f, 12f);
            player.AdvanceMovement(Vector2.right, true, 0.05f);
            yield return new WaitForSeconds(0.06f);
            yield return Shot("11-dash");

            gm.TogglePause();
            yield return new WaitForSecondsRealtime(0.2f);
            Debug.Assert(Cursor.lockState == CursorLockMode.None && Cursor.visible, "Pause should release the cursor.");
            yield return Shot("12-paused-cursor-released");
            gm.TogglePause();
            yield return new WaitForSecondsRealtime(0.2f);
            Debug.Assert(Cursor.lockState == CursorLockMode.Locked && !Cursor.visible, "Resume should lock the cursor.");
            yield return Shot("13-resumed-cursor-locked");

            Debug.Log("VISUAL CAPTURE: done; errors=" + errors);
            yield return new WaitForSeconds(0.4f);
            view.mouseSensitivity = mouseSensitivity;
            SessionState.SetBool("KitchenChaos.VisualCapture.Running", false);
            EditorApplication.Exit(errors == 0 ? 0 : 1);
        }

        private IEnumerator VerifyAudioPlayback()
        {
            int before = errors;
            AudioManager audio = AudioManager.Instance;
            yield return new WaitForSeconds(0.4f);
            Debug.Assert(audio.MusicSource.isPlaying && audio.MusicSource.clip == Resources.Load<AudioClip>("Audio/Music/CafeLoop"), "Imported cafe music should be playing.");
            int sample = audio.MusicSource.timeSamples;
            yield return new WaitForSeconds(0.2f);
            Debug.Assert(audio.MusicSource.timeSamples != sample && audio.MusicSource.pitch == 1f, "Music advances at its original pitch.");
            float musicLevel = audio.musicVolume;
            float sfxLevel = audio.volume;
            audio.musicVolume = 0f;
            yield return null;
            Debug.Assert(audio.MusicSource.volume == 0f, "Music slider zero silences the source without changing SFX volume.");
            Debug.Assert(audio.volume == sfxLevel, "Music and SFX controls are independent.");
            audio.musicVolume = musicLevel;
            foreach (SfxType type in new[] { SfxType.Footstep, SfxType.PlatePickup, SfxType.PlatePlace })
            {
                audio.PlayAt(type, player.transform.position);
                bool playingSample = false;
                foreach (AudioSource voice in audio.SfxSources)
                    if (voice.isPlaying && voice.clip != null && voice.clip.name.StartsWith(type + "_")
                        && AssetDatabase.Contains(voice.clip)) playingSample = true;
                Debug.Assert(playingSample, "Imported sample plays through the real voice pool: " + type);
                yield return new WaitForSeconds(0.7f);
            }
            Teleport(stove.transform.position + new Vector3(0f, 0f, 1.35f));
            Focus(stove);
            player.GiveItem(new Ingredient(IngredientType.MeatPatty, IngredientState.Raw));
            Debug.Assert(stove.Interact(player), "Audio check places a patty through the stove interaction.");
            yield return new WaitForSeconds(0.3f);
            AudioSource loop = stove.SizzleSource;
            Debug.Assert(loop != null && loop.isPlaying && loop.loop && loop.volume > 0f, "Occupied stove has a continuous audible source.");
            audio.volume = 0f;
            yield return null;
            Debug.Assert(loop.volume == 0f && audio.musicVolume == musicLevel, "SFX slider zero silences frying without changing music volume.");
            audio.volume = sfxLevel;
            gm.TogglePause();
            Debug.Assert(!loop.isPlaying && loop.volume == 0f, "Pause immediately silences the stove.");
            yield return new WaitForSecondsRealtime(0.25f);
            Debug.Assert(audio.MusicSource.isPlaying, "Music continues quietly while paused.");
            gm.TogglePause();
            yield return new WaitForSeconds(0.25f);
            Debug.Assert(loop.isPlaying && loop.volume > 0f, "Resume restores the occupied stove loop.");
            Focus(stove);
            Debug.Assert(stove.Interact(player), "Taking the patty back succeeds.");
            Debug.Assert(!loop.isPlaying && loop.volume == 0f, "Taking food stops the loop immediately.");
            gm.TogglePause();
            gm.TogglePause();
            yield return new WaitForSeconds(0.2f);
            Debug.Assert(!loop.isPlaying, "An empty stove stays silent after pause/resume.");
            player.ClearCarried();
            player.GiveItem(new Ingredient(IngredientType.MeatPatty, IngredientState.Raw));
            Focus(stove);
            stove.Interact(player);
            yield return new WaitForSeconds(0.2f);
            stove.AdvanceProcessing(20f);
            Debug.Assert(stove.IsBurned && !loop.isPlaying && loop.volume == 0f, "Burning stops the frying loop.");
            gm.ReturnToMenu();
            gm.StartGame();
            gm.Orders.StopSpawning();
            gm.Orders.ClearOrders();
            yield return new WaitForSeconds(0.2f);
            Debug.Assert(!loop.isPlaying && stove.PlacedIngredient == null, "Day reset clears the old loop.");
            Debug.Log("AUDIO PLAYBACK CHECKS: " + (errors == before ? "PASS" : "FAIL"));
        }

        private void CaptureError(string message, string trace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++;
        }

        private void OnDestroy()
        {
            Application.logMessageReceived -= CaptureError;
            if (view != null) view.mouseSensitivity = mouseSensitivity;
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
            view.RefreshPose();
            Physics.SyncTransforms();
        }

        private void Focus(MonoBehaviour station)
        {
            view.RefreshPose();
            Vector3 direction = station.transform.position + Vector3.up * 0.45f - player.ViewCamera.transform.position;
            float yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Atan2(direction.y, new Vector2(direction.x, direction.z).magnitude) * Mathf.Rad2Deg;
            view.SetViewAngles(yaw, pitch);
            view.RefreshPose();
            Physics.SyncTransforms();
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(PlayerController).GetMethod("FindTarget", flags).Invoke(player, null);
        }

        private static IngredientContainer FindContainer(IngredientType type)
        {
            foreach (IngredientContainer container in Object.FindObjectsByType<IngredientContainer>())
                if (container.IngredientType == type) return container;
            return null;
        }

        private IEnumerator Shot(string name, MonoBehaviour station = null)
        {
            if (station != null) Focus(station);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot("Logs/visual/" + name + ".png");
            yield return new WaitForEndOfFrame();
            yield return new WaitForSecondsRealtime(0.25f);
        }
    }
}
