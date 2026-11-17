using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using KitchenChaos.Core;
using KitchenChaos.Data;
using KitchenChaos.Gameplay;

namespace KitchenChaos.EditorTools
{
    public static partial class KitchenSmokeTest
    {
        private static void VerifyInputSystem()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            Expect(false, "legacy Input Manager is disabled");
#else
            Expect(true, "legacy Input Manager is disabled");
#endif
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            InputSettings.EditorInputBehaviorInPlayMode originalEditorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSettings.BackgroundBehavior originalBackgroundBehavior = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Gamepad gamepad = InputSystem.AddDevice<Gamepad>();
            bool wasEnabled = player.enabled;
            try
            {
                InputAction move = (InputAction)typeof(PlayerController).GetField("moveAction", flags).GetValue(player);
                Expect(move.enabled, "movement action is enabled with the player");
                Key[] keys = { Key.W, Key.UpArrow, Key.S, Key.DownArrow, Key.A, Key.LeftArrow, Key.D, Key.RightArrow };
                Vector2[] directions = { Vector2.up, Vector2.up, Vector2.down, Vector2.down, Vector2.left, Vector2.left, Vector2.right, Vector2.right };
                for (int i = 0; i < keys.Length; i++)
                {
                    SetInputKeys(keyboard, keys[i]);
                    Expect(move.ReadValue<Vector2>() == directions[i], "movement binding " + keys[i]);
                }
                SetInputKeys(keyboard, Key.W, Key.D);
                Expect(move.ReadValue<Vector2>() == Vector2.one, "diagonal input reaches the existing movement clamp unchanged");
                SetInputKeys(keyboard, Key.W, Key.S, Key.A, Key.D);
                Expect(move.ReadValue<Vector2>() == Vector2.zero, "opposite movement keys cancel");
                SetInputKeys(keyboard, Key.W, Key.UpArrow);
                Expect(move.ReadValue<Vector2>() == Vector2.up, "duplicate up bindings do not double movement");
                SetInputKeys(keyboard);
                Expect(move.ReadValue<Vector2>() == Vector2.zero, "key release stops movement input");

                SetInputKeys(keyboard, Key.E);
                Expect(keyboard.eKey.wasPressedThisFrame && keyboard.eKey.isPressed, "interaction begins on the press frame");
                SetInputKeys(keyboard, Key.E);
                Expect(!keyboard.eKey.wasPressedThisFrame && keyboard.eKey.isPressed, "held interaction does not create repeated presses");
                SetInputKeys(keyboard);
                Expect(!keyboard.eKey.isPressed, "interaction releases");

                InputSystem.QueueStateEvent(gamepad, new GamepadState { leftStick = Vector2.right });
                UpdateGameInput();
                Expect(move.ReadValue<Vector2>().x > 0.99f, "gamepad stick still moves right");
                InputSystem.QueueStateEvent(gamepad, new GamepadState());
                UpdateGameInput();
                Expect(move.ReadValue<Vector2>() == Vector2.zero, "gamepad stick returns to idle");

                player.enabled = false;
                Expect(!move.enabled, "disabling the player disables its movement action");
                player.enabled = true;
                Expect(move.enabled, "re-enabling the player restores its movement action");

                GameplayHUD hud = gm.GetComponent<GameplayHUD>();
                foreach (Key enter in new[] { Key.Enter, Key.NumpadEnter })
                {
                    gm.ReturnToMenu();
                    SetInputKeys(keyboard);
                    SetInputKeys(keyboard, enter);
                    InvokeInputUpdate(hud);
                    Expect(gm.State == GameState.Playing, "start game with " + enter);
                    SetInputKeys(keyboard, Key.P);
                    InvokeInputUpdate(hud);
                    Expect(gm.State == GameState.Paused, "P pauses gameplay");
                    SetInputKeys(keyboard, Key.P);
                    InvokeInputUpdate(hud);
                    Expect(gm.State == GameState.Paused, "holding P does not toggle pause repeatedly");
                    SetInputKeys(keyboard, Key.Escape);
                    InvokeInputUpdate(hud);
                    Expect(gm.State == GameState.Playing, "Escape resumes paused gameplay");
                    SetInputKeys(keyboard);
                    SetInputKeys(keyboard, Key.Escape);
                    InvokeInputUpdate(hud);
                    Expect(gm.State == GameState.Paused, "Escape pauses gameplay");
                }

                if (!Application.isBatchMode)
                {
                    NewRun();
                    FocusStation(Container(IngredientType.Bun));
                    SetInputKeys(keyboard, Key.E);
                    InvokePlayerInputUpdate();
                    Expect(player.HasItem && player.CarriedItem.Type == IngredientType.Bun, "E reaches the current station through PlayerController.Update");
                }

                NewRun();
                UseStation(Container(IngredientType.Lettuce));
                UseStation(cutter);
                SetInputKeys(keyboard, Key.E);
                InvokeInputUpdate(cutter);
                Expect(cutter.Progress01 > 0f, "holding E advances cutting through CuttingCounter.Update");
                float cutProgress = cutter.Progress01;
                SetInputKeys(keyboard);
                InvokeInputUpdate(cutter);
                Expect(cutter.Progress01 == cutProgress && !cutter.IsProcessing, "releasing E stops cutting");

                VerifyMenuPointer(hud);

                if (!Application.isBatchMode)
                {
                    NewRun();
                    UseStation(Container(IngredientType.Lettuce));
                    UseStation(cutter);
                    SetInputKeys(keyboard, Key.X);
                    InvokePlayerInputUpdate();
                    Expect(player.HasItem && player.CarriedItem.Type == IngredientType.Lettuce && cutter.PlacedIngredient == null,
                        "X retrieves the uncut ingredient through PlayerController.Update");
                    foreach (Key dash in new[] { Key.LeftShift, Key.RightShift, Key.Space })
                    {
                        NewRun();
                        SetInputKeys(keyboard, dash);
                        InvokePlayerInputUpdate();
                        Expect(player.DashReady01 < 1f, "dash starts with " + dash);
                    }
                }
                else Debug.Log("INPUT CHECKS: run RunWindowed for the five player keypress checks that require cursor locking.");
                SetInputKeys(keyboard);
            }
            finally
            {
                if (keyboard.added) InputSystem.RemoveDevice(keyboard);
                if (gamepad.added) InputSystem.RemoveDevice(gamepad);
                player.enabled = wasEnabled;
                gm.ReturnToMenu();
                InputSystem.settings.editorInputBehaviorInPlayMode = originalEditorBehavior;
                InputSystem.settings.backgroundBehavior = originalBackgroundBehavior;
            }
        }

        private static void VerifyMenuPointer(GameplayHUD hud)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            MethodInfo setState = typeof(GameplayHUD).GetMethod("SetPointerState", flags);
            MethodInfo control = typeof(GameplayHUD).GetMethod("PointerControl", flags);
            Rect rect = new Rect(100f, 100f, 200f, 40f);
            Vector2 inside = rect.center;
            Vector2 outside = Vector2.zero;
            void Set(Vector2 position, bool down, bool up, bool held) =>
                setState.Invoke(hud, new object[] { position, down, up, held });
            bool Click(bool enabled = true) =>
                (bool)control.Invoke(hud, new object[] { rect, enabled, false, false });

            Set(inside, true, false, true);
            Expect(!Click(), "menu button captures press without activating");
            Set(inside, false, false, true);
            Expect(!Click(), "menu button does not repeat while held");
            Set(inside, false, true, false);
            Expect(Click(), "menu button activates on release inside");
            Expect(!Click(), "menu release is consumed once across GUI repaints");

            Set(outside, true, false, true);
            Click();
            Set(inside, false, true, false);
            Expect(!Click(), "pressing outside and releasing inside does not click");
            Set(inside, true, false, true);
            Click();
            Set(outside, false, true, false);
            Expect(!Click(), "releasing outside cancels the button click");

            Set(inside, true, false, true);
            Expect(!Click(false), "disabled menu control ignores press");
            Set(inside, false, true, false);
            Expect(!Click(), "disabled control did not capture a future release");

            Set(inside, true, false, true);
            Click();
            Set(outside, false, false, true);
            object[] drag = { rect, true, false, false };
            control.Invoke(hud, drag);
            Expect((bool)drag[3], "slider capture continues when dragging outside its rectangle");
            typeof(GameplayHUD).GetMethod("OnApplicationFocus", flags).Invoke(hud, new object[] { false });
            Set(inside, false, true, false);
            Expect(!Click(), "focus callback cancels pointer capture even when Update stops");

            Set(inside, true, true, false);
            Expect(Click(), "press and release in one frame still clicks");
            Set(outside, false, false, false);
        }

        private static void UpdateGameInput()
        {
            // EditorApplication.update otherwise updates the editor buffer rather than the gameplay buffer.
            typeof(InputSystem).GetMethod("Update", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { InputUpdateType.Dynamic });
        }

        private static void SetInputKeys(Keyboard keyboard, params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
            UpdateGameInput();
        }

        private static void InvokeInputUpdate(MonoBehaviour component)
        {
            component.GetType().GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(component, null);
        }

        private static void InvokePlayerInputUpdate()
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(PlayerController).GetField("inputBlockedFrame", flags).SetValue(player, -1);
            typeof(PlayerController).GetField("hasFocus", flags).SetValue(player, true);
            Cursor.lockState = CursorLockMode.Locked;
            InvokeInputUpdate(player);
        }
    }
}
