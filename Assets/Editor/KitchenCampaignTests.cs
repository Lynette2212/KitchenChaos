using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using KitchenChaos.Core;
using KitchenChaos.Data;
using KitchenChaos.Gameplay;

namespace KitchenChaos.EditorTools
{
    public static partial class KitchenSmokeTest
    {
        private static void NewRun()
        {
            gm.ReturnToMenu();
            gm.StartGame();
            gm.Orders.StopSpawning();
            gm.Orders.ClearOrders();
        }

        private static void FocusCutter() => FocusStation(cutter);

        private static void MovePlayer(Vector3 position)
        {
            CharacterController controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.transform.position = position;
            controller.enabled = true;
            player.ViewCamera.GetComponent<CameraFollow>().RefreshPose();
            Physics.SyncTransforms();
        }

        private static void AimAt(Vector3 point)
        {
            Vector3 direction = point - player.ViewCamera.transform.position;
            float yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Atan2(direction.y, new Vector2(direction.x, direction.z).magnitude) * Mathf.Rad2Deg;
            player.ViewCamera.GetComponent<CameraFollow>().SetViewAngles(yaw, pitch);
            RefreshTarget();
        }

        private static void RefreshTarget()
        {
            Physics.SyncTransforms();
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(PlayerController).GetMethod("FindTarget", flags).Invoke(player, null);
        }

        private static void FocusStation(MonoBehaviour station)
        {
            Vector3 offset = Mathf.Abs(station.transform.position.z) > 2f
                ? Vector3.back * Mathf.Sign(station.transform.position.z) * 1.3f
                : Vector3.left * Mathf.Sign(station.transform.position.x) * 1.3f;
            Vector3 position = station.transform.position + offset;
            position.y = 0.05f;
            MovePlayer(position);
            AimAt(station.transform.position + Vector3.up * 0.45f);
            bool selected = ReferenceEquals(player.CurrentTarget, station);
            if (!selected)
            {
                string hits = string.Empty;
                foreach (RaycastHit hit in Physics.RaycastAll(player.ViewCamera.transform.position, player.ViewCamera.transform.forward, player.interactRange))
                    hits += hit.collider.name + " at " + hit.distance + "; ";
                Debug.Log("Ray probe: " + station.name + " eye=" + player.ViewCamera.transform.position + " direction=" + player.ViewCamera.transform.forward + " hits=" + hits);
            }
            Expect(selected, "crosshair selects " + station.name);
        }

        private static bool UseStation(MonoBehaviour station)
        {
            FocusStation(station);
            return ReferenceEquals(player.CurrentTarget, station) && player.CurrentTarget.Interact(player);
        }

        private static IngredientContainer Container(IngredientType type)
        {
            foreach (IngredientContainer container in Object.FindObjectsByType<IngredientContainer>())
                if (container.IngredientType == type) return container;
            throw new System.InvalidOperationException("Missing container " + type);
        }

        private static List<Ingredient> Dish(Recipe recipe)
        {
            List<Ingredient> result = new List<Ingredient>();
            foreach (RecipeIngredient item in recipe.ingredients) result.Add(new Ingredient(item.type, item.requiredState));
            return result;
        }

        private static void VerifyFirstPerson()
        {
            NewRun();
            Camera camera = player.ViewCamera;
            CameraFollow view = camera.GetComponent<CameraFollow>();
            Expect(!camera.orthographic && camera.rect == new Rect(0f, 0f, 1f, 1f), "first-person camera uses the full perspective viewport");
            Expect(view.target == player.transform && view.eyeHeight > 1f, "camera follows the player at eye height");
            var stack = camera.GetUniversalAdditionalCameraData().cameraStack;
            int heldMask = 1 << PlayerController.HeldItemLayer;
            Expect(stack.Count == 1 && stack[0].GetUniversalAdditionalCameraData().renderType == CameraRenderType.Overlay
                && stack[0].GetUniversalAdditionalCameraData().clearDepth && stack[0].cullingMask == heldMask
                && (camera.cullingMask & heldMask) == 0, "held camera clears world depth and renders only carried objects");
            foreach (float yaw in new[] { 0f, 90f, 180f, 270f })
            {
                foreach (float pitch in new[] { -60f, 0f, 60f })
                {
                    view.SetViewAngles(yaw, pitch);
                    Expect(Quaternion.Angle(camera.transform.rotation, Quaternion.Euler(pitch, yaw, 0f)) < 0.01f,
                        "yaw/pitch pose " + yaw + "/" + pitch);
                    Expect(Vector3.Distance(camera.transform.position, player.transform.position + Vector3.up * view.eyeHeight) < 0.001f,
                        "eye stays centered without orbiting at " + yaw + "/" + pitch);
                }
                foreach (Vector2 input in new[] { Vector2.up, Vector2.down, Vector2.left, Vector2.right, Vector2.one })
                {
                    player.ResetForDay();
                    view.SetViewAngles(yaw, 60f);
                    Vector3 start = player.transform.position;
                    player.AdvanceMovement(input, false, 0.05f);
                    Vector3 expected = Quaternion.Euler(0f, yaw, 0f) * new Vector3(input.normalized.x, 0f, input.normalized.y) * player.moveSpeed * 0.05f;
                    Vector3 actual = Vector3.ProjectOnPlane(player.transform.position - start, Vector3.up);
                    Expect(Vector3.Distance(actual, expected) < 0.002f, "yaw-relative normalized movement " + yaw + "/" + input);
                    Expect(Quaternion.Angle(camera.transform.rotation, Quaternion.Euler(60f, yaw, 0f)) < 0.01f,
                        "movement does not turn or tilt the camera " + yaw + "/" + input);
                }
            }
            view.SetViewAngles(358f, 0f);
            player.AdvanceLook(new Vector2(5f, 3f));
            Expect(Mathf.Abs(Mathf.DeltaAngle(player.transform.eulerAngles.y, 358f + 5f * view.mouseSensitivity)) < 0.01f
                && Mathf.Approximately(view.Pitch, -3f * view.mouseSensitivity), "mouse look wraps yaw and applies pitch with the correct sign");
            view.SetViewAngles(0f, 74f);
            player.AdvanceLook(new Vector2(0f, -100f));
            Expect(view.Pitch == 75f, "look down clamps before flipping");
            player.AdvanceLook(new Vector2(0f, 200f));
            Expect(view.Pitch == -75f, "look up clamps before flipping");

            player.ResetForDay();
            view.SetViewAngles(0f, 0f);
            GameObject target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            GameObject blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                target.name = "RaycastTestCounter";
                target.transform.localScale = Vector3.one * 0.4f;
                target.transform.position = camera.transform.position + Vector3.forward * 1.5f;
                PrepCounter station = target.AddComponent<PrepCounter>();
                blocker.transform.localScale = Vector3.one * 0.3f;
                blocker.transform.position = camera.transform.position + Vector3.forward * 0.7f;
                blocker.SetActive(false);
                RefreshTarget();
                Expect(ReferenceEquals(player.CurrentTarget, station), "crosshair selects an in-range station");
                blocker.SetActive(true);
                RefreshTarget();
                Expect(player.CurrentTarget == null, "noninteractable first hit blocks a station behind it");
                blocker.AddComponent<PrepCounter>();
                RefreshTarget();
                Expect(ReferenceEquals(player.CurrentTarget, blocker.GetComponent<PrepCounter>()), "nearest station wins when counters overlap on screen");
                blocker.layer = PlayerController.HeldItemLayer;
                RefreshTarget();
                Expect(ReferenceEquals(player.CurrentTarget, station), "held layer never blocks the interaction ray");
                blocker.layer = 0;
                blocker.GetComponent<Collider>().isTrigger = true;
                RefreshTarget();
                Expect(ReferenceEquals(player.CurrentTarget, station), "triggers do not block interaction");
                blocker.SetActive(false);
                foreach (float offset in new[] { -0.01f, 0.01f })
                {
                    target.transform.position = camera.transform.position + Vector3.forward * (player.interactRange + 0.2f + offset);
                    RefreshTarget();
                    Expect((player.CurrentTarget != null) == (offset < 0f), "interaction reach tests hit surface at range " + offset);
                }
                target.transform.position = camera.transform.position + Vector3.forward * 1.5f;
                view.SetViewAngles(90f, 0f);
                RefreshTarget();
                Expect(player.CurrentTarget == null, "looking into empty space clears the old target");
                view.SetViewAngles(0f, 0f);
                station.enabled = false;
                RefreshTarget();
                Expect(player.CurrentTarget == null, "disabled station is not interactable");
            }
            finally
            {
                target.SetActive(false);
                blocker.SetActive(false);
                Object.Destroy(target);
                Object.Destroy(blocker);
            }

            player.ResetForDay();
            player.GiveItem(new Ingredient(IngredientType.Tomato, IngredientState.Raw));
            Transform held = player.CarriedItem.transform;
            Vector3 localHeld = camera.transform.InverseTransformPoint(held.position);
            view.SetViewAngles(137f, -45f);
            Expect(Vector3.Distance(camera.transform.InverseTransformPoint(held.position), localHeld) < 0.001f, "held ingredient follows camera yaw and pitch");
            player.CarriedItem.SetState(IngredientState.Cut);
            Expect(AllOnLayer(held, PlayerController.HeldItemLayer), "rebuilt held ingredient stays on overlay layer");
            player.ClearCarried();
            UseStation(plates);
            PlateHandle handle = player.Plate;
            handle.Add(new Ingredient(IngredientType.Bun, IngredientState.Raw));
            Expect(AllOnLayer(handle.transform, PlayerController.HeldItemLayer), "plate and newly added food use the held layer");
            localHeld = camera.transform.InverseTransformPoint(handle.transform.position);
            view.SetViewAngles(223f, 55f);
            Expect(Vector3.Distance(camera.transform.InverseTransformPoint(handle.transform.position), localHeld) < 0.001f, "filled plate follows both camera axes");
            UseStation(prep);
            Expect(!player.HandsBusy && AllOnLayer(handle.transform, 0) && handle.transform.localScale == Vector3.one, "stored plate restores world layer and scale");
            typeof(KitchenChaos.Visual.FeedbackFX).GetMethod("UpdatePunches", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(KitchenChaos.Visual.FeedbackFX.Instance, new object[] { 1f });
            Expect(handle.transform.localScale == Vector3.one, "finishing pickup feedback does not shrink a newly stored plate");
            UseStation(prep);
            Expect(player.HasPlate && AllOnLayer(handle.transform, PlayerController.HeldItemLayer), "retrieved plate returns to the held layer");

            gm.TogglePause();
            Vector3 pausedPosition = player.transform.position;
            Quaternion pausedView = camera.transform.rotation;
            player.AdvanceLook(Vector2.one * 80f);
            player.AdvanceMovement(Vector2.one, true, 1f);
            Expect(player.transform.position == pausedPosition && camera.transform.rotation == pausedView, "pause rejects camera and movement changes");
            Expect(Cursor.lockState == CursorLockMode.None && Cursor.visible && player.CurrentTarget == null, "pause releases mouse and clears the target");
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(PlayerController).GetMethod("OnApplicationFocus", flags).Invoke(player, new object[] { false });
            Expect(Cursor.lockState == CursorLockMode.None && Cursor.visible, "focus loss releases mouse");
            typeof(PlayerController).GetMethod("OnApplicationFocus", flags).Invoke(player, new object[] { true });
            Expect(Cursor.lockState == CursorLockMode.None && Cursor.visible, "focus gain does not unpause or trap the cursor");
            gm.TogglePause();
            Expect((int)typeof(PlayerController).GetField("inputBlockedFrame", flags).GetValue(player) == Time.frameCount, "resume suppresses gameplay input in the transition frame");
            typeof(PlayerController).GetMethod("Update", flags).Invoke(player, null);
            Expect(player.transform.position == pausedPosition && camera.transform.rotation == pausedView && player.CurrentTarget == null,
                "resume frame does not apply stale input or restore a stale target");
            var hudOrder = (DefaultExecutionOrder)System.Attribute.GetCustomAttribute(typeof(GameplayHUD), typeof(DefaultExecutionOrder));
            var playerOrder = (DefaultExecutionOrder)System.Attribute.GetCustomAttribute(typeof(PlayerController), typeof(DefaultExecutionOrder));
            Expect(hudOrder.order < playerOrder.order && playerOrder.order < 0, "pause keys run before movement and processing");
            player.ResetForDay();
            Expect(!player.HandsBusy && player.CurrentTarget == null && view.Pitch == 12f
                && Mathf.Abs(Mathf.DeltaAngle(player.transform.eulerAngles.y, 0f)) < 0.01f, "day reset clears held items and restores initial view");
        }

        private static bool AllOnLayer(Transform root, int layer)
        {
            foreach (Transform part in root.GetComponentsInChildren<Transform>(true))
                if (part.gameObject.layer != layer) return false;
            return true;
        }

        private static void VerifySceneAndRules()
        {
            Expect(gm != null && player != null && cutter != null && stove != null && plates != null && prep != null && delivery != null, "all gameplay systems exist");
            Expect(Object.FindObjectsByType<IngredientContainer>().Length == 7, "seven ingredient sources");
            Expect(Object.FindObjectsByType<PrepCounter>().Length == 2, "two prep counters");
            Expect(gm.Orders.AvailableRecipes.Count == 6, "six recipes");
            Expect(gm.State == GameState.Ready && !Container(IngredientType.Bun).Interact(player), "ready state blocks kitchen input");
            Expect(CampaignRules.TargetForDay(1) == 3 && CampaignRules.TargetForDay(5) == 7, "progressive day targets");
            Expect(CampaignRules.DurationForDay(5, 150) == 230, "final day gives time for complex dishes");
            Expect(CampaignRules.Stars(0.25f, 0) == 3 && CampaignRules.Stars(0.24f, 0) == 2 && CampaignRules.Stars(0.1f, 1) == 1, "star thresholds and mistake penalty");
            foreach (Recipe recipe in gm.Orders.AvailableRecipes)
            {
                List<Ingredient> dish = Dish(recipe);
                dish.Reverse();
                Expect(recipe.Matches(dish), recipe.recipeName + " matches in any order");
                dish.Add(dish[0].Clone());
                Expect(!recipe.Matches(dish), recipe.recipeName + " rejects extra ingredients");
            }
            Order expired = new Order(gm.Orders.AvailableRecipes[0], 1f);
            Expect(expired.Tick(2f) && expired.timeRemaining == 0f && !expired.Tick(1f), "order expiry clamps and fires once");
        }

        private static void VerifyEveryRecipe()
        {
            foreach (Recipe recipe in gm.Orders.AvailableRecipes)
            {
                NewRun();
                Expect(UseStation(plates), recipe.recipeName + " take plate");
                Expect(UseStation(prep) && !player.HandsBusy, "store plate intact to free hands");
                foreach (RecipeIngredient ingredient in recipe.ingredients)
                {
                    IngredientInfo info = IngredientCatalog.Get(ingredient.type);
                    Expect(UseStation(Container(ingredient.type)), "take raw " + ingredient.type);
                    if (info.needsCut)
                    {
                        Expect(UseStation(cutter), "place raw " + ingredient.type + " on cutter");
                        cutter.AdvanceProcessing(info.cutTime / gm.CookingSpeedMultiplier + 0.01f);
                        Expect(cutter.PlacedIngredient != null && cutter.PlacedIngredient.state == IngredientState.Cut, "real cutting produces cut " + ingredient.type);
                        Expect(UseStation(cutter), "retrieve cut ingredient");
                    }
                    if (info.needsCook)
                    {
                        Expect(UseStation(stove), "place ready ingredient on stove");
                        stove.AdvanceProcessing(info.cookTime / gm.CookingSpeedMultiplier + 0.01f);
                        Expect(stove.IsCooked, "real cooking produces cooked " + ingredient.type);
                        Expect(UseStation(stove), "retrieve cooked ingredient");
                    }
                    Expect(UseStation(prep) && !player.HandsBusy, "add prepared ingredient to stored plate");
                }
                Expect(UseStation(prep) && player.HasPlate, "retrieve assembled plate");
                Expect(recipe.Matches(new List<Ingredient>(player.Plate.Contents)), "ALL STATIONS: " + recipe.recipeName);
                Expect(!UseStation(plates) && player.Plate.Count == recipe.ingredients.Count, "nonempty plate cannot be silently discarded");
                gm.Orders.ActiveOrders.Add(new Order(recipe, 90f));
                Expect(UseStation(delivery), "deliver assembled " + recipe.recipeName);
                Expect(gm.Score.CompletedOrders == 1 && gm.Score.Score > 0 && player.Plate.Count == 0, "delivery scores and retains reusable empty plate");
            }
        }

        private static void VerifyProcessingAndPause()
        {
            NewRun();
            Container(IngredientType.Mushroom).Interact(player);
            Expect(!stove.Interact(player) && player.HasItem, "raw mushroom cannot skip chopping");
            FocusCutter();
            cutter.Interact(player);
            cutter.AdvanceProcessing(0.3f);
            float cutProgress = cutter.Progress01;
            gm.TogglePause();
            float remaining = gm.Timer.TimeRemaining;
            cutter.AdvanceProcessing(20f);
            gm.Timer.AdvanceTime(20f);
            Expect(cutter.Progress01 == cutProgress && gm.Timer.TimeRemaining == remaining, "pause freezes chopping and shift timer");
            Expect(!cutter.Interact(player) && !plates.Interact(player) && !prep.Interact(player), "paused station interactions rejected");
            gm.TogglePause();
            FocusCutter();
            player.ViewCamera.GetComponent<CameraFollow>().SetViewAngles(0f, 0f);
            RefreshTarget();
            cutter.AdvanceProcessing(2f);
            Expect(cutter.Progress01 == cutProgress, "looking away stops chopping even with the key held");
            FocusCutter();
            cutter.AdvanceProcessing(2f);
            Expect(cutter.PlacedIngredient.state == IngredientState.Cut && cutter.Progress01 == 1f, "resume continues partial chopping after retargeting");
            cutter.Interact(player);
            stove.Interact(player);
            stove.AdvanceProcessing(3f);
            Expect(stove.IsCooked, "mushroom cooks after chopping");
            gm.TogglePause();
            stove.AdvanceProcessing(30f);
            Expect(stove.IsCooked, "pause prevents burning");
            gm.TogglePause();
            stove.AdvanceProcessing(30f);
            Expect(stove.IsBurned, "unattended food eventually burns");
            stove.Interact(player);
            Object.FindAnyObjectByType<TrashBin>().Interact(player);
            Expect(!player.HandsBusy, "burned food can be discarded");

            plates.Interact(player);
            Expect(!player.Plate.Add(new Ingredient(IngredientType.MeatPatty, IngredientState.Raw)), "plate rejects raw meat");
            Expect(!player.Plate.Add(new Ingredient(IngredientType.Tomato, IngredientState.Raw)), "plate rejects uncut vegetables");
            Expect(!player.Plate.Add(new Ingredient(IngredientType.MeatPatty, IngredientState.Burned)), "plate rejects burned food");
            Expect(player.Plate.Add(new Ingredient(IngredientType.Bun, IngredientState.Raw)), "plate accepts ready bun");
            Expect(!player.Plate.Add(new Ingredient(IngredientType.Bun, IngredientState.Raw)), "plate rejects duplicate ingredient");

            NewRun();
            gm.Orders.StartSpawning();
            gm.Orders.AdvanceTime(2f);
            Expect(gm.Orders.ActiveOrders.Count == 1 && CampaignRules.UnlockDay(gm.Orders.ActiveOrders[0].recipe) == 1, "first day spawns tutorial recipe");
            Order pending = gm.Orders.ActiveOrders[0];
            gm.TogglePause();
            float patience = pending.timeRemaining;
            gm.Orders.AdvanceTime(30f);
            Expect(pending.timeRemaining == patience, "pause freezes customer patience");
            gm.TogglePause();
            gm.Orders.ActiveOrders.Clear();
            for (int i = 0; i < gm.MaxReputation; i++) gm.Orders.ActiveOrders.Add(new Order(pending.recipe, 0.1f));
            gm.Orders.AdvanceTime(1f);
            Expect(gm.State == GameState.GameOver && gm.Reputation == 0, "simultaneous expired orders end run without collection error");
            Expect(!gm.TryDeliverDish(Dish(pending.recipe)), "cannot score after failure");
            gm.StartGame();
            Expect(gm.IsPlaying && gm.Day == 1 && gm.Score.Score == 0 && !player.HandsBusy, "restart fully resets run and kitchen");
            gm.Timer.AdvanceTime(1000f);
            Expect(gm.State == GameState.GameOver && gm.EndReason.Contains("未达到"), "timer failure has correct reason");
        }

        private static void VerifyFeedback()
        {
            KitchenChaos.Visual.FeedbackFX fx = Object.FindAnyObjectByType<KitchenChaos.Visual.FeedbackFX>();
            Expect(fx != null && KitchenChaos.Visual.FeedbackFX.Instance == fx, "feedback system is created with the kitchen");
            KitchenChaos.Visual.FeedbackFX.Clear();
            Expect(KitchenChaos.Visual.FeedbackFX.Instance.ActiveParticles == 0 && KitchenChaos.Visual.FeedbackFX.Instance.Popups.Count == 0, "clearing feedback resets particles and popups");
            KitchenChaos.Visual.FeedbackFX.Burst(player.transform.position + Vector3.up, Color.white, 5);
            KitchenChaos.Visual.FeedbackFX.Popup(player.transform.position + Vector3.up, "+10", Color.white, true);
            KitchenChaos.Visual.FeedbackFX.Punch(player.transform, 0.2f, 0.3f);
            KitchenChaos.Visual.FeedbackFX.Shake(0.2f, 0.3f);
            Expect(KitchenChaos.Visual.FeedbackFX.Instance.ActiveParticles == 5, "particle burst spawns pooled particles");
            Expect(KitchenChaos.Visual.FeedbackFX.Instance.Popups.Count == 1, "world popup is queued for the HUD");
            Expect(KitchenChaos.Visual.FeedbackFX.Instance.ActivePunches == 1, "punch animation targets a transform");
            Expect(KitchenChaos.Visual.FeedbackFX.Instance.ShakeRemaining > 0f, "camera shake is armed");
            KitchenChaos.Visual.FeedbackFX.Clear();
            Expect(player.transform.localScale == Vector3.one, "clearing feedback restores punched scales");

            NewRun();
            Recipe simple = gm.Orders.AvailableRecipes.Find(r => r.ingredients.Count == 1);
            Expect(simple != null, "feedback test finds a single-ingredient recipe");
            Expect(plates.Interact(player), "feedback test takes a plate");
            foreach (RecipeIngredient item in simple.ingredients)
                player.Plate.Add(new Ingredient(item.type, item.requiredState));
            gm.Orders.ActiveOrders.Add(new Order(simple, 90f));
            KitchenChaos.Visual.FeedbackFX.Clear();
            Expect(delivery.Interact(player) && gm.Score.CompletedOrders == 1, "feedback delivery succeeds");
            Expect(KitchenChaos.Visual.FeedbackFX.Instance.Popups.Count >= 1, "successful delivery spawns a score popup");
            KitchenChaos.Visual.FeedbackFX.Clear();
            Expect(KitchenChaos.Visual.FeedbackFX.Instance.Popups.Count == 0 && KitchenChaos.Visual.FeedbackFX.Instance.ActiveParticles == 0, "feedback cleanup after delivery");
        }

        private static void VerifyUpgradeEffects()
        {
            foreach (KitchenUpgrade upgrade in System.Enum.GetValues(typeof(KitchenUpgrade)))
            {
                NewRun();
                Recipe recipe = gm.Orders.AvailableRecipes.Find(r => r.ingredients.Count == 1);
                for (int i = 0; i < gm.DayTarget; i++)
                {
                    gm.Orders.ActiveOrders.Add(new Order(recipe, 90f));
                    gm.TryDeliverDish(Dish(recipe));
                }
                gm.UpgradeChoices[0] = upgrade;
                gm.ChooseUpgrade(0);
                switch (upgrade)
                {
                    case KitchenUpgrade.QuickHands:
                        Container(IngredientType.Tomato).Interact(player);
                        FocusCutter();
                        cutter.Interact(player);
                        cutter.AdvanceProcessing(1f);
                        Expect(cutter.PlacedIngredient.state == IngredientState.Cut, "quick hands actually finishes 1.2s chopping within 1s");
                        Container(IngredientType.MeatPatty).Interact(player);
                        stove.Interact(player);
                        stove.AdvanceProcessing(2.5f);
                        Expect(stove.IsCooked, "quick hands actually finishes 3s cooking within 2.5s");
                        break;
                    case KitchenUpgrade.RunningShoes:
                        float before = player.transform.position.x;
                        player.AdvanceMovement(Vector2.right, false, 0.05f);
                        float travelled = player.transform.position.x - before;
                        Expect(Mathf.Abs(travelled - player.moveSpeed * 1.15f * 0.05f) < 0.002f, "running shoes increase actual CharacterController movement by fifteen percent");
                        break;
                    case KitchenUpgrade.PatientGuests:
                        gm.Orders.AdvanceTime(2f);
                        Order patient = gm.Orders.ActiveOrders[0];
                        Expect(Mathf.Approximately(patient.timeLimit, CampaignRules.OrderTimeLimit(patient.recipe, gm.Day, 0f) + 15f), "patient guests adds fifteen seconds to spawned tickets");
                        break;
                    case KitchenUpgrade.SafeStove:
                        Container(IngredientType.MeatPatty).Interact(player);
                        stove.Interact(player);
                        stove.AdvanceProcessing(3f);
                        stove.AdvanceProcessing(10f);
                        Expect(stove.IsCooked && Mathf.Approximately(stove.SecondsUntilBurn, 3f), "safe stove survives beyond standard eight-second grace");
                        stove.AdvanceProcessing(3.1f);
                        Expect(stove.IsBurned, "upgraded stove still burns after thirteen seconds");
                        break;
                    case KitchenUpgrade.ExtraHeart:
                        for (int i = 0; i < gm.startReputation; i++)
                            gm.TryDeliverDish(new List<Ingredient> { new Ingredient(IngredientType.Bun, IngredientState.Raw) });
                        Expect(gm.IsPlaying && gm.Reputation == 1, "extra heart survives one additional mistake");
                        break;
                    case KitchenUpgrade.BonusTips:
                        gm.Orders.ClearOrders();
                        gm.Orders.ActiveOrders.Add(new Order(recipe, 90f));
                        int beforeScore = gm.Score.Score;
                        gm.TryDeliverDish(Dish(recipe));
                        Expect(gm.Score.Score - beforeScore == Mathf.RoundToInt(Mathf.RoundToInt(recipe.score * 1.25f) * 1.5f), "bonus tips affect real delivery score");
                        break;
                }
            }
            NewRun();
            player.AdvanceMovement(Vector2.right, true, 0.2f);
            float dashEnd = player.transform.position.x;
            Expect(Mathf.Abs(dashEnd - player.moveSpeed * (2.6f * 0.18f + 0.02f)) < 0.002f && player.DashReady01 < 0.1f, "dash travels further and starts cooldown");
            player.AdvanceMovement(Vector2.right, true, 0.05f);
            Expect(Mathf.Abs(player.transform.position.x - dashEnd - player.moveSpeed * 0.05f) < 0.002f, "dash cannot be spammed during cooldown");
            gm.TogglePause();
            Vector3 paused = player.transform.position;
            player.AdvanceMovement(Vector2.one, true, 1f);
            Expect(player.transform.position == paused, "pause rejects direct movement and dash");
            gm.TogglePause();
            player.ResetForDay();
            player.AdvanceMovement(Vector2.right, true, 0.18f);
            for (int i = 0; i < 20; i++) player.AdvanceMovement(Vector2.right, false, 0.1f);
            Expect(player.transform.position.x < 4.2f, "dash and walking cannot pass through counter or boundary colliders");
            int persisted = PlayerPrefs.GetInt("KitchenChaos.Campaign.BestScore", 0);
            gm.Score.RegisterSuccess(gm.BestScore + 100, 1f);
            int achieved = gm.Score.Score;
            gm.ReturnToMenu();
            Expect(gm.BestScore >= achieved, "returning to menu records newly achieved high score");
            gm.StartGame();
            Expect(gm.BestScore >= achieved && gm.Score.Score == 0, "new run preserves high score while clearing current score");
            Expect(PlayerPrefs.GetInt("KitchenChaos.Campaign.BestScore", 0) == persisted, "test runs do not modify the user's saved records");
        }

        private static void VerifyCampaign()
        {
            NewRun();
            Recipe recipe = gm.Orders.AvailableRecipes[0];
            gm.Orders.ActiveOrders.Add(new Order(recipe, 90f));
            gm.TryDeliverDish(Dish(recipe));
            Expect(gm.Score.Combo == 1 && gm.Score.ComboMultiplier == 1, "first delivery starts at 1x");
            gm.TryDeliverDish(new List<Ingredient> { new Ingredient(IngredientType.Bun, IngredientState.Raw) });
            Expect(gm.Score.Combo == 0 && gm.Score.FailedOrders == 1 && gm.Reputation == gm.MaxReputation - 1, "wrong dish consistently breaks combo and costs reputation");

            NewRun();
            int initialWins = gm.TotalWins;
            for (int day = 1; day <= CampaignRules.TotalDays; day++)
            {
                Expect(gm.Day == day && gm.Reputation == gm.MaxReputation, "day starts at full upgraded reputation");
                foreach (Recipe unlocked in gm.Orders.UnlockedRecipes)
                    Expect(CampaignRules.UnlockDay(unlocked) <= day, "menu respects day unlock");
                for (int order = 0; order < gm.DayTarget; order++)
                {
                    gm.Orders.ActiveOrders.Add(new Order(recipe, 90f));
                    Expect(gm.TryDeliverDish(Dish(recipe)), "campaign delivery " + day + "/" + order);
                }
                Expect(gm.DayStars == 3 && gm.TotalStars == day * 3, "perfect day earns three stars");
                if (day == CampaignRules.TotalDays) break;
                Expect(gm.State == GameState.DayComplete && Time.timeScale == 0f, "goal immediately opens intermission and freezes kitchen");
                Expect(gm.UpgradeChoices.Length == 3 && new HashSet<KitchenUpgrade>(gm.UpgradeChoices).Count == 3, "three distinct upgrade choices");
                Expect(!gm.ChooseUpgrade(-1) && !gm.ChooseUpgrade(3) && gm.Day == day, "invalid upgrade indexes rejected");
                KitchenUpgrade choice = gm.UpgradeChoices[0];
                float speed = gm.CookingSpeedMultiplier, move = gm.MoveSpeedMultiplier, patience = gm.OrderPatienceBonus, grace = gm.BurnGraceBonus, tips = gm.TipMultiplier;
                int maxHearts = gm.MaxReputation;
                Expect(gm.ChooseUpgrade(0), "select upgrade and advance");
                bool effect = choice == KitchenUpgrade.QuickHands ? gm.CookingSpeedMultiplier > speed
                    : choice == KitchenUpgrade.RunningShoes ? gm.MoveSpeedMultiplier > move
                    : choice == KitchenUpgrade.PatientGuests ? gm.OrderPatienceBonus > patience
                    : choice == KitchenUpgrade.SafeStove ? gm.BurnGraceBonus > grace
                    : choice == KitchenUpgrade.ExtraHeart ? gm.MaxReputation == maxHearts + 1 : gm.TipMultiplier > tips;
                Expect(effect && gm.UpgradeSummary.Contains(CampaignRules.UpgradeName(choice)), "chosen upgrade changes real gameplay values");
                Expect(gm.IsPlaying && Time.timeScale == 1f && gm.Orders.ActiveOrders.Count == 0 && !player.HandsBusy, "next day starts clean and unpaused");
                Expect(!gm.ChooseUpgrade(0), "upgrade cannot be applied twice");
            }
            Expect(gm.State == GameState.Victory && gm.TotalWins == initialWins + 1 && gm.BestDay == 5, "full campaign wins and records completion");
            Expect(gm.Score.CompletedOrders == 25 && gm.TotalStars == 15 && gm.BestScore >= gm.Score.Score, "final cumulative statistics");
            gm.StartGame();
            Expect(gm.Day == 1 && gm.CookingSpeedMultiplier == 1f && gm.MoveSpeedMultiplier == 1f && gm.BurnGraceBonus == 0f && gm.OrderPatienceBonus == 0f && gm.TotalStars == 0, "new campaign resets all upgrades and stars");
        }
    }
}
