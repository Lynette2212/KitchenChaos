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
            Physics.SyncTransforms();
        }

        private static void Face(Vector3 point)
        {
            Vector3 delta = point - player.transform.position;
            delta.y = 0f;
            if (delta.sqrMagnitude < 0.0001f) return;
            player.transform.rotation = Quaternion.LookRotation(delta.normalized);
            Physics.SyncTransforms();
        }

        private static void RefreshTarget()
        {
            Physics.SyncTransforms();
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(PlayerController).GetMethod("FindTarget", flags).Invoke(player, null);
        }

        private static void FocusStation(MonoBehaviour station)
        {
            Vector3 position = station.transform.position;
            Vector3 offset;
            if (Mathf.Abs(position.z) > 2f) offset = Vector3.back * Mathf.Sign(position.z) * 0.85f;
            else if (Mathf.Abs(position.x) > 2.5f) offset = Vector3.left * Mathf.Sign(position.x) * 0.85f;
            else offset = Vector3.back * 0.85f;
            Vector3 stand = position + offset;
            stand.y = 0.05f;
            MovePlayer(stand);
            Face(position + Vector3.up * 0.4f);
            RefreshTarget();
            bool selected = ReferenceEquals(player.CurrentTarget, station);
            if (!selected)
                Debug.Log("Target probe: " + station.name + " stand=" + stand + " current=" + (player.CurrentTarget as MonoBehaviour));
            Expect(selected, "proximity selects " + station.name);
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

        private static void VerifyTopDown()
        {
            NewRun();
            Camera camera = player.ViewCamera;
            TopDownCamera rig = camera != null ? camera.GetComponent<TopDownCamera>() : null;
            Expect(camera != null && camera.orthographic && camera.orthographicSize >= 3f, "2.5D camera frames the whole kitchen orthographically");
            Expect(rig != null && camera.transform.position.y > 5f && camera.transform.forward.y < -0.5f,
                "camera stays fixed high above the kitchen looking down");
            Expect(camera.cullingMask == -1, "main camera renders the chef and carried items");
            Expect(player.Chef != null && player.Chef.GetComponentsInChildren<Renderer>().Length >= 1, "imported chef model is visible");
            bool chefOnWorldLayer = true;
            foreach (Renderer part in player.Chef.GetComponentsInChildren<Renderer>())
                if (part.gameObject.layer != 0) chefOnWorldLayer = false;
            Expect(chefOnWorldLayer, "chef model renders on the world layer");
            Vector3 cameraPosition = camera.transform.position;
            Quaternion cameraRotation = camera.transform.rotation;

            foreach (Vector2 input in new[] { Vector2.up, Vector2.down, Vector2.left, Vector2.right, Vector2.one })
            {
                player.ResetForDay();
                Vector3 start = player.transform.position;
                player.AdvanceMovement(input, false, 0.3f);
                Vector3 direction = (Vector3.right * input.x + Vector3.forward * input.y).normalized;
                Vector3 expected = direction * player.moveSpeed * 0.3f;
                Vector3 actual = Vector3.ProjectOnPlane(player.transform.position - start, Vector3.up);
                Expect(Vector3.Distance(actual, expected) < 0.002f, "top-down movement follows screen axes " + input);
                Expect(Vector3.Dot(player.transform.forward, direction) > 0.999f, "chef turns toward movement " + input);
            }
            Expect(camera.transform.position == cameraPosition && camera.transform.rotation == cameraRotation, "camera never orbits with the player");

            player.ResetForDay();
            Expect(Mathf.Abs(Mathf.DeltaAngle(player.transform.eulerAngles.y, 180f)) < 0.01f, "chef idles facing the camera");

            IngredientContainer bun = Container(IngredientType.Bun);
            MovePlayer(bun.transform.position + Vector3.back * 0.9f);
            Face(bun.transform.position);
            RefreshTarget();
            Expect(ReferenceEquals(player.CurrentTarget, bun), "nearest station becomes the target");
            player.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            RefreshTarget();
            Expect(ReferenceEquals(player.CurrentTarget, bun), "turning away still targets a nearby station");
            Expect(player.CurrentTarget.Interact(player) && player.HasItem, "a nearby station works while facing away");
            player.ClearCarried();
            MovePlayer(new Vector3(0f, 0.05f, 0f));
            Face(bun.transform.position);
            RefreshTarget();
            Expect(player.CurrentTarget == null, "station out of reach is not targetable");

            player.ResetForDay();
            Expect(UseStation(Container(IngredientType.Bun)) && player.HasItem, "top-down interaction takes an ingredient");
            Vector3 localHeld = player.transform.InverseTransformPoint(player.CarriedItem.transform.position);
            MovePlayer(new Vector3(1.2f, 0.05f, -0.6f));
            Expect(Vector3.Distance(player.transform.InverseTransformPoint(player.CarriedItem.transform.position), localHeld) < 0.001f,
                "carried item follows the chef, not the camera");
            Expect(localHeld.y > 1f, "carried item floats above the chef's head");
            Expect(player.CarriedItem.gameObject.layer == PlayerController.HeldItemLayer, "carried item stays on its own render layer");

            player.ClearCarried();
            UseStation(plates);
            PlateHandle handle = player.Plate;
            handle.Add(new Ingredient(IngredientType.Bun, IngredientState.Raw));
            Transform plateTransform = handle.transform;
            Vector3 plateLocal = player.transform.InverseTransformPoint(plateTransform.position);
            MovePlayer(new Vector3(-1.1f, 0.05f, -0.6f));
            Expect(Vector3.Distance(player.transform.InverseTransformPoint(plateTransform.position), plateLocal) < 0.001f, "held plate follows the chef");
            UseStation(prep);
            Expect(!player.HandsBusy && AllOnLayer(plateTransform, 0) && plateTransform.localScale == Vector3.one, "stored plate restores world layer and scale");
            UseStation(prep);
            Expect(player.HasPlate, "stored plate can be retrieved");

            gm.TogglePause();
            Vector3 pausedPosition = player.transform.position;
            player.AdvanceMovement(Vector2.one, true, 1f);
            Expect(player.transform.position == pausedPosition, "pause rejects movement");
            Expect(Cursor.lockState == CursorLockMode.None && Cursor.visible && player.CurrentTarget == null, "pause releases mouse and clears the target");
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(PlayerController).GetMethod("OnApplicationFocus", flags).Invoke(player, new object[] { false });
            Expect(Cursor.lockState == CursorLockMode.None && Cursor.visible, "focus loss releases mouse");
            typeof(PlayerController).GetMethod("OnApplicationFocus", flags).Invoke(player, new object[] { true });
            Expect(Cursor.lockState == CursorLockMode.None && Cursor.visible, "focus gain does not unpause or trap the cursor");
            gm.TogglePause();
            Expect((int)typeof(PlayerController).GetField("inputBlockedFrame", flags).GetValue(player) == Time.frameCount, "resume suppresses gameplay input in the transition frame");
            typeof(PlayerController).GetMethod("Update", flags).Invoke(player, null);
            Expect(player.transform.position == pausedPosition && player.CurrentTarget == null, "resume frame ignores stale input");
            var hudOrder = (DefaultExecutionOrder)System.Attribute.GetCustomAttribute(typeof(GameplayHUD), typeof(DefaultExecutionOrder));
            var playerOrder = (DefaultExecutionOrder)System.Attribute.GetCustomAttribute(typeof(PlayerController), typeof(DefaultExecutionOrder));
            Expect(hudOrder.order < playerOrder.order && playerOrder.order < 0, "pause keys run before movement and processing");
            player.ResetForDay();
            Expect(!player.HandsBusy && player.CurrentTarget == null && Mathf.Abs(Mathf.DeltaAngle(player.transform.eulerAngles.y, 180f)) < 0.01f,
                "day reset clears held items and faces the camera");
        }

        private static void VerifyPlatePickup()
        {
            NewRun();
            Expect(UseStation(Container(IngredientType.MeatPatty)) && player.HasItem, "take a raw patty before the plate");
            int stock = plates.PlateCount;
            Expect(!UseStation(plates) && player.HasItem && !player.HasPlate, "a raw patty stops the plate pickup");
            Expect(plates.PlateCount == stock, "a rejected plate pickup keeps stock");
            Expect(UseStation(stove) && !player.HasItem && stove.OccupiedCount == 1, "the raw patty goes onto the stove");
            stove.AdvanceProcessing(IngredientCatalog.Get(IngredientType.MeatPatty).cookTime / gm.CookingSpeedMultiplier + 0.01f);
            Expect(stove.IsCooked, "the patty cooks before plating");
            Expect(UseStation(stove) && player.HasItem && stove.OccupiedCount == 0, "the cooked patty is taken by hand");
            Expect(UseStation(plates) && player.HasPlate && !player.HasItem && player.Plate.Count == 1,
                "taking a plate auto-plates the cooked patty");
            Expect(plates.PlateCount == stock - 1, "auto-plating still consumes one plate");
            Expect(UseStation(Container(IngredientType.Bun)) && player.HasPlate && player.Plate.Count == 2,
                "an ingredient picked up with a plate goes straight onto it");
            Expect(UseStation(prep) && !player.HandsBusy && player.Plate == null, "the full plate can still be stored at a prep counter");
            NewRun();
        }

        private static void VerifyMistakeFeedback()
        {
            NewRun();
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(PlayerController).GetField("lastRejectSound", flags).SetValue(player, -10f);
            KitchenChaos.Visual.FeedbackFX.Clear();
            player.GiveItem(new Ingredient(IngredientType.Cheese, IngredientState.Raw));
            FocusStation(stove);
            Expect(!stove.Interact(player) && player.HasItem, "raw cheese is rejected by the griddle");
            Expect(KitchenChaos.Visual.FeedbackFX.Instance.Popups.Count > 0, "rejected operation shows a mistake popup");
            Expect(player.Chef != null && player.Chef.IsMistakeActive, "chef visibly reacts to a mistake");
            player.ClearCarried();

            Expect(IngredientCatalog.Get(IngredientType.MeatPatty).cookTime >= 5.5f, "patty now takes noticeably longer to cook");

            gm.Orders.ClearOrders();
            Recipe burger = gm.Orders.AvailableRecipes.Find(r => r.recipeName == "芝士汉堡");
            Expect(burger != null, "cheeseburger recipe exists");
            if (burger != null)
            {
                gm.Orders.ActiveOrders.Add(new Order(burger, 90f));
                Expect(!gm.TryDeliverDish(new List<Ingredient> { new Ingredient(IngredientType.Bun, IngredientState.Raw) }),
                    "incomplete cheeseburger is rejected");
                Expect(gm.LastDeliveryError.Contains(burger.recipeName) && gm.LastDeliveryError.Contains("缺"),
                    "delivery error names the ticket and what is missing");
                Expect(gm.Reputation == gm.MaxReputation - 1 && gm.Score.Mistakes == 1,
                    "failed delivery still costs one reputation and counts one mistake");
            }
            gm.Orders.ClearOrders();
            NewRun();
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
            Expect(Object.FindObjectsByType<PrepCounter>().Length == 4, "four prep counters");
            Expect(delivery.transform.position.x < -2f && Mathf.Abs(delivery.transform.position.z - 0.5f) < 0.01f,
                "delivery counter sits on the customer window side");
            Expect(gm.Orders.AvailableRecipes.Count == 6, "six recipes");
            Expect(gm.State == GameState.Ready && !Container(IngredientType.Bun).Interact(player), "ready state blocks kitchen input");
            Expect(CampaignRules.TargetForDay(1) == 2 && CampaignRules.TargetForDay(2) == 4 && CampaignRules.TargetForDay(5) == 7, "day 1 is a short target and later days climb");
            Expect(CampaignRules.DurationForDay(1, 150f) == 120f && CampaignRules.DurationForDay(5, 150) == 230, "day 1 lasts two minutes and the final day stays long");
            int dayOneFullPlate = 0;
            foreach (Recipe opened in gm.Orders.AvailableRecipes)
            {
                if (CampaignRules.UnlockDay(opened) != 1 || opened.ingredients.Count < 2) continue;
                bool needsCut = false, needsCook = false;
                foreach (RecipeIngredient item in opened.ingredients)
                {
                    IngredientInfo info = IngredientCatalog.Get(item.type);
                    if (info != null && info.needsCut) needsCut = true;
                    if (info != null && info.needsCook) needsCook = true;
                }
                if (needsCut && needsCook) dayOneFullPlate++;
            }
            Expect(dayOneFullPlate >= 1, "day 1 unlocks a recipe that needs cutting, cooking, and plating");
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
            player.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
            Physics.SyncTransforms();
            RefreshTarget();
            Expect(ReferenceEquals(player.CurrentTarget, cutter), "turning away keeps the nearby cutter as the target");
            cutter.AdvanceProcessing(0.2f);
            Expect(cutter.Progress01 > cutProgress, "turning away keeps chopping while still near the cutter");
            FocusCutter();
            cutter.AdvanceProcessing(2f);
            Expect(cutter.PlacedIngredient.state == IngredientState.Cut && cutter.Progress01 == 1f, "chopping completes after retargeting");
            cutter.Interact(player);
            stove.Interact(player);
            stove.AdvanceProcessing(3f);
            Expect(stove.IsCooked && stove.SecondsUntilBurn >= 4f && stove.SecondsUntilBurn <= 6f, "mushroom cooks, then burns within about four to six seconds");
            gm.TogglePause();
            stove.AdvanceProcessing(30f);
            Expect(stove.IsCooked, "pause prevents burning");
            gm.TogglePause();
            stove.AdvanceProcessing(30f);
            Expect(stove.IsBurned, "unattended food burns after the short grace");
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
            Expect(gm.Orders.ActiveOrders.Count == 1 && CampaignRules.UnlockDay(gm.Orders.ActiveOrders[0].recipe) == 1 && gm.Orders.ActiveOrders[0].recipe.ingredients.Count > 1, "first day opens on a multi-ingredient recipe");
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
                        float pattyTime = IngredientCatalog.Get(IngredientType.MeatPatty).cookTime / gm.CookingSpeedMultiplier;
                        stove.AdvanceProcessing(pattyTime + 0.05f);
                        Expect(stove.IsCooked, "quick hands finishes the patty within its sped-up cook time");
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
                        Expect(Mathf.Approximately(patient.timeLimit, CampaignRules.OrderTimeLimit(patient.recipe, gm.Day, 15f, patient.urgency)), "patient guests adds fifteen seconds to spawned tickets");
                        break;
                    case KitchenUpgrade.SafeStove:
                        Container(IngredientType.MeatPatty).Interact(player);
                        stove.Interact(player);
                        stove.AdvanceProcessing(IngredientCatalog.Get(IngredientType.MeatPatty).cookTime);
                        stove.AdvanceProcessing(7f);
                        Expect(stove.IsCooked && stove.CookedCount == 1 && Mathf.Approximately(stove.SecondsUntilBurn, 3f), "safe stove leaves three seconds on a ten-second grace");
                        stove.AdvanceProcessing(3.1f);
                        Expect(stove.IsBurned, "upgraded griddle still burns once the extra grace runs out");
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
            gm.Score.RegisterSuccess(10, 1f);
            gm.Score.RegisterSuccess(10, 1f);
            Expect(gm.Score.Combo == 2 && gm.Score.ComboMultiplier == 2, "second delivery already pays 2x");
            gm.TryDeliverDish(new List<Ingredient> { new Ingredient(IngredientType.Bun, IngredientState.Raw) });
            Expect(gm.Score.Combo == 0 && gm.Score.Mistakes == 1 && gm.Score.FailedOrders == 0 && gm.Reputation == gm.MaxReputation - 1,
                "wrong dish breaks the combo, counts a mistake and costs one reputation");
            NewRun();
            Expect(UseStation(plates), "soft mismatch takes a plate");
            Expect(player.Plate.Add(new Ingredient(IngredientType.Bun, IngredientState.Raw)), "soft mismatch plates a bun");
            Expect(player.Plate.Add(new Ingredient(IngredientType.Cheese, IngredientState.Cut)), "soft mismatch plates cut cheese");
            int kept = player.Plate.Count;
            Expect(UseStation(delivery) && player.HasPlate && player.Plate.Count == kept, "wrong delivery keeps every ingredient on the plate");
            Expect(gm.Reputation == gm.MaxReputation - 1 && gm.Score.Mistakes == 1 && gm.Score.FailedOrders == 0, "wrong delivery spends one reputation and logs one mistake");
            NewRun();
            Recipe recipe = gm.Orders.AvailableRecipes[0];
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
            Expect(gm.Score.CompletedOrders == 24 && gm.TotalStars == 15 && gm.BestScore >= gm.Score.Score, "final cumulative statistics");
            gm.StartGame();
            Expect(gm.Day == 1 && gm.CookingSpeedMultiplier == 1f && gm.MoveSpeedMultiplier == 1f && gm.BurnGraceBonus == 0f && gm.OrderPatienceBonus == 0f && gm.TotalStars == 0, "new campaign resets all upgrades and stars");
        }

        private static void VerifyUrgencyAndErrors()
        {
            NewRun();
            Recipe single = gm.Orders.AvailableRecipes.Find(r => r.ingredients.Count == 1);
            Expect(single != null, "urgency test finds a single-ingredient recipe");
            if (single == null) return;

            float normalLimit = CampaignRules.OrderTimeLimit(single, 1, 0f, OrderUrgency.Normal);
            float expressLimit = CampaignRules.OrderTimeLimit(single, 1, 0f, OrderUrgency.Express);
            float criticalLimit = CampaignRules.OrderTimeLimit(single, 1, 0f, OrderUrgency.Critical);
            Expect(normalLimit > expressLimit && expressLimit > criticalLimit, "express and critical tickets use tighter time limits");
            Expect(OrderUrgencyInfo.ScoreBonus(OrderUrgency.Express) > 1f
                && OrderUrgencyInfo.ScoreBonus(OrderUrgency.Critical) > OrderUrgencyInfo.ScoreBonus(OrderUrgency.Express),
                "urgent tickets pay larger score bonuses");

            Order normal = new Order(single, 80f, OrderUrgency.Normal);
            Order critical = new Order(single, 30f, OrderUrgency.Critical);
            gm.Orders.ActiveOrders.Add(normal);
            gm.Orders.ActiveOrders.Add(critical);
            gm.Orders.SortByRemaining();
            Expect(ReferenceEquals(gm.Orders.ActiveOrders[0], critical), "order queue sorts by remaining time");
            DeliveryResult auto = gm.Orders.TryDeliver(Dish(single));
            Expect(auto != null && ReferenceEquals(auto.order, critical), "delivery auto-dispatches the most urgent matching ticket");
            Expect(auto != null && auto.speedBonus > normal.SpeedBonus, "urgent ticket applies its score multiplier");
            gm.Orders.ClearOrders();

            int warnings = 0;
            Order warned = null;
            System.Action<Order> handler = order => { warnings++; warned = order; };
            gm.Orders.StartSpawning();
            gm.Orders.OrderCritical += handler;
            Order lastCall = new Order(single, 100f, OrderUrgency.Critical) { timeRemaining = 21f };
            gm.Orders.ActiveOrders.Add(lastCall);
            gm.Orders.AdvanceTime(0.02f);
            Expect(warnings == 1 && ReferenceEquals(warned, lastCall), "critical ticket warns once when its time is nearly up");
            gm.Orders.AdvanceTime(0.02f);
            Expect(warnings == 1, "critical warning never repeats");
            gm.Orders.OrderCritical -= handler;
            gm.Orders.StopSpawning();
            gm.Orders.ClearOrders();

            NewRun();
            gm.TryDeliverDish(new List<Ingredient> { new Ingredient(IngredientType.Bun, IngredientState.Raw) });
            Expect(gm.Score.Mistakes == 1 && gm.Score.Combo == 0 && gm.Score.FailedOrders == 0 && gm.Reputation == gm.MaxReputation - 1,
                "wrong delivery counts a mistake, breaks the combo and costs one reputation");

            NewRun();
            Expect(Container(IngredientType.MeatPatty).Interact(player) && stove.Interact(player), "status test places a patty");
            stove.AdvanceProcessing(1f);
            var views = new List<StoveCounter.SlotStatus>();
            stove.CollectSlotStatus(views);
            Expect(views.Count == 1 && views[0].state == IngredientState.Raw && views[0].progress01 > 0f && views[0].progress01 < 1f,
                "patty status exposes live griddle progress");
            float cookTime = IngredientCatalog.Get(IngredientType.MeatPatty).cookTime;
            stove.AdvanceProcessing(cookTime);
            stove.CollectSlotStatus(views);
            Expect(views.Count == 1 && views[0].state == IngredientState.Cooked && views[0].progress01 == 1f
                && Mathf.Approximately(views[0].secondsUntilBurn, 4f), "cooked patty status exposes the burn countdown");
            Expect(gm.Score.Mistakes == 0, "cooking does not add mistakes");
            stove.AdvanceProcessing(4.1f);
            stove.CollectSlotStatus(views);
            Expect(views.Count == 1 && views[0].state == IngredientState.Burned && views[0].progress01 == 1f,
                "burned patty status stays visible until it is taken away");
            Expect(gm.Score.Mistakes == 1, "burned food counts as a kitchen mistake");
            stove.ResetStation();
        }
    }
}
