using System.Collections.Generic;
using UnityEngine;
using KitchenChaos.Audio;
using KitchenChaos.Core;
using KitchenChaos.Data;
using KitchenChaos.Gameplay;
using KitchenChaos.Visual;

namespace KitchenChaos.Core
{
    /// <summary>
    /// 程序化搭建整个厨房关卡与全部游戏系统，无需手工摆放场景：
    /// 房间、灯光、相机、玩家、食材柜 / 切菜台 / 灶台 / 餐盘台 / 出餐台 / 垃圾桶，
    /// 以及 GameManager / Timer / Orders / Score / UI。
    /// 挂在空场景唯一根对象上即可，进入 Play 自动建厨房。
    /// </summary>
    public partial class KitchenBuilder : MonoBehaviour
    {
        [Header("关卡参数")]
        public int stoveCount = 2;
        public int cuttingCount = 2;
        public int prepCount = 4;
        public float roundDuration = 150f;
        public int startReputation = 3;

        private readonly List<GameObject> spawned = new List<GameObject>();
        private PlayerController player;

        public PlayerController Player => player;

        private void Start()
        {
            Build();
        }

        public void Build()
        {
            BuildEnvironment();
            BuildSystems();

            List<IngredientContainer> containers = BuildContainers();
            List<CuttingCounter> cutters = BuildCuttingCounters();
            List<StoveCounter> stoves = BuildStoves();
            BuildPlateCounter();
            DeliveryCounter delivery = BuildDeliveryCounter();
            BuildTrashBin();
            BuildPrepCounters();

            BuildPlayer();

            GameManager gameManager = GetComponent<GameManager>();
            gameManager.DayStarting += ResetKitchen;
            if (delivery != null) delivery.Init(gameManager);

            GameplayHUD hud = GetComponent<GameplayHUD>();
            if (hud != null && player != null) hud.Init(gameManager, player);
        }

        // ---------------- 环境 ----------------

        private void BuildEnvironment()
        {
            Material floorA = Visual.VisualFactory.Surface("FloorCream");
            Material floorB = Visual.VisualFactory.Surface("FloorTeal");
            for (int x = -5; x < 5; x++)
            {
                for (int z = -4; z < 4; z++)
                {
                    GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    tile.name = "FloorTile";
                    tile.transform.SetParent(transform, false);
                    tile.transform.position = new Vector3(x * 1f + 0.5f, -0.05f, z * 1f + 0.5f);
                    tile.transform.localScale = new Vector3(1f, 0.1f, 1f);
                    tile.GetComponent<Renderer>().sharedMaterial = ((x + z) & 1) == 0 ? floorA : floorB;
                    spawned.Add(tile);
                }
            }

            Material plinth = Visual.VisualFactory.Surface("Oak", new Vector2(6f, 5f));
            Detail("Kitchen plinth", transform, new Vector3(0f, -0.16f, 0f), new Vector3(9.4f, 0.22f, 7.4f), plinth);
            Material deck = Visual.VisualFactory.Surface("Deck", new Vector2(9f, 7f));
            Detail("Terrace", transform, new Vector3(0.6f, -0.25f, 0.2f), new Vector3(16f, 0.12f, 14f), deck);
            BuildOpenBoundary();

            // 灯光：开放露台要更亮、阴影更软
            GameObject lightGo = new GameObject("Key Light");
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
            Light key = lightGo.AddComponent<Light>();
            key.type = LightType.Directional;
            key.intensity = 1.05f;
            key.color = new Color(1f, 0.95f, 0.85f);
            key.shadows = LightShadows.Soft;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.70f, 0.74f, 0.70f);
            BuildOpenKitchen();
            var customerWindow = new GameObject("CustomerWindow");
            customerWindow.transform.SetParent(transform, false);
            customerWindow.AddComponent<Visual.CustomerWindow>();

            GameObject camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(transform, false);
            Camera cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 4.7f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 40f;
            cam.rect = new Rect(0f, 0f, 1f, 1f);
            cam.cullingMask = -1;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.80f, 0.87f, 0.82f);
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<TopDownCamera>();

            // 音频
            if (AudioManager.Instance == null)
            {
                GameObject audioGo = new GameObject("AudioManager");
                audioGo.transform.SetParent(transform, false);
                audioGo.AddComponent<AudioManager>();
            }
            if (Visual.FeedbackFX.Instance == null)
            {
                GameObject fxGo = new GameObject("FeedbackFX");
                fxGo.transform.SetParent(transform, false);
                fxGo.AddComponent<Visual.FeedbackFX>();
            }
        }

        private void BuildOpenBoundary()
        {
            // 开放式厨房没有墙，只保留隐形边界，避免角色跑出固定取景范围。
            AddBoundary("Kitchen boundary N", new Vector3(0f, 0.9f, 3.45f), new Vector3(9.2f, 1.8f, 0.1f));
            AddBoundary("Kitchen boundary S", new Vector3(0f, 0.9f, -3.45f), new Vector3(9.2f, 1.8f, 0.1f));
            AddBoundary("Kitchen boundary W", new Vector3(-4.45f, 0.9f, 0f), new Vector3(0.1f, 1.8f, 7.2f));
            AddBoundary("Kitchen boundary E", new Vector3(4.45f, 0.9f, 0f), new Vector3(0.1f, 1.8f, 7.2f));
        }

        private void AddBoundary(string name, Vector3 position, Vector3 size)
        {
            var boundary = new GameObject(name);
            boundary.transform.SetParent(transform, false);
            boundary.transform.localPosition = position;
            BoxCollider box = boundary.AddComponent<BoxCollider>();
            box.size = size;
        }

        // ---------------- 柜台 ----------------
        private GameObject MakeCounter(string name, Vector3 pos, Color color)
        {
            GameObject counter = GameObject.CreatePrimitive(PrimitiveType.Cube);
            counter.name = name;
            counter.transform.SetParent(transform, false);
            counter.transform.position = pos;
            counter.transform.localScale = new Vector3(1f, 1f, 1f);
            counter.GetComponent<Renderer>().sharedMaterial = Visual.VisualFactory.Surface("Cabinet");
            spawned.Add(counter);

            // 台面装饰条
            GameObject top = GameObject.CreatePrimitive(PrimitiveType.Cube);
            top.name = "CounterTop";
            top.transform.SetParent(counter.transform, false);
            top.transform.localPosition = new Vector3(0f, 0.52f, 0f);
            top.transform.localScale = new Vector3(1.04f, 0.08f, 1.04f);
            top.GetComponent<Renderer>().sharedMaterial = Visual.VisualFactory.Surface("Terrazzo");
            DecorateCounter(counter, color);
            counter.AddComponent<InteractableHighlighter>();

            // 食材摆放锚点
            GameObject anchor = new GameObject("ItemAnchor");
            anchor.transform.SetParent(counter.transform, false);
            anchor.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            return counter;
        }

        private List<IngredientContainer> BuildContainers()
        {
            // 北墙一排食材柜
            IngredientType[] types =
            {
                IngredientType.Bun, IngredientType.MeatPatty, IngredientType.Cheese,
                IngredientType.Tomato, IngredientType.Lettuce, IngredientType.Onion, IngredientType.Mushroom
            };
            List<IngredientContainer> list = new List<IngredientContainer>();
            float spacing = 1f;
            float startX = -3.5f;
            for (int i = 0; i < types.Length; i++)
            {
                Vector3 pos = new Vector3(startX + i * spacing, 0.5f, 2.5f);
                GameObject c = MakeContainerCounter("Container_" + types[i], pos, types[i]);
                list.Add(c.GetComponent<IngredientContainer>());
            }
            return list;
        }

        private GameObject MakeContainerCounter(string name, Vector3 pos, IngredientType type)
        {
            GameObject counter = MakeCounter(name, pos, new Color(0.30f, 0.42f, 0.55f));
            IngredientContainer container = counter.AddComponent<IngredientContainer>();
            container.IngredientType = type;

            // 在柜顶展示一份该食材样本
            GameObject sample = Visual.VisualFactory.CreateIngredientVisual(type, IngredientState.Raw);
            sample.name = "Sample";
            sample.transform.SetParent(counter.transform, false);
            sample.transform.localPosition = new Vector3(0f, 0.61f, 0f);
            Detail("Ingredient tray", counter.transform, new Vector3(0f, 0.58f, 0f), new Vector3(0.85f, 0.045f, 0.82f), Visual.VisualFactory.Surface("Oak"));
            spawned.Add(sample);
            return counter;
        }

        private List<CuttingCounter> BuildCuttingCounters()
        {
            List<CuttingCounter> list = new List<CuttingCounter>();
            float spacing = 1f;
            float startX = -1.5f;
            for (int i = 0; i < cuttingCount; i++)
            {
                Vector3 pos = new Vector3(startX + i * spacing, 0.5f, -2.5f);
                GameObject c = MakeCounter("CuttingCounter_" + i, pos, new Color(0.65f, 0.42f, 0.20f));
                GameObject board = PlaceProp("cutting-board", c.transform, new Vector3(0f, 0.565f, 0f), new Vector3(1f, 0.6f, 1f));
                c.transform.Find("ItemAnchor").position = new Vector3(pos.x, SurfaceTop(board) + 0.004f, pos.z);
                PlaceProp("cooking-knife", c.transform, new Vector3(0.39f, 0.575f, -0.08f), Vector3.one, 170);
                CuttingCounter cutter = c.AddComponent<CuttingCounter>();
                list.Add(cutter);
            }
            return list;
        }

        private List<StoveCounter> BuildStoves()
        {
            List<StoveCounter> list = new List<StoveCounter>();
            float spacing = 1f;
            float startX = 1.5f;
            for (int i = 0; i < stoveCount; i++)
            {
                Vector3 pos = new Vector3(startX + i * spacing, 0.5f, -2.5f);
                GameObject c = MakeCounter("StoveCounter_" + i, pos, new Color(0.25f, 0.25f, 0.28f));
                // 灶眼
                c.transform.Find("CounterTop").GetComponent<Renderer>().sharedMaterial = Visual.VisualFactory.Surface("Steel");
                GameObject griddle = GameObject.CreatePrimitive(PrimitiveType.Cube);
                griddle.name = "Griddle";
                Collider griddleCollider = griddle.GetComponent<Collider>();
                if (griddleCollider != null) { griddleCollider.enabled = false; Destroy(griddleCollider); }
                griddle.transform.SetParent(c.transform, false);
                griddle.transform.localPosition = new Vector3(0f, 0.6f, 0.02f);
                griddle.transform.localScale = new Vector3(0.92f, 0.045f, 0.78f);
                griddle.GetComponent<Renderer>().sharedMaterial = Visual.VisualFactory.Surface("Grill");
                c.transform.Find("ItemAnchor").localPosition = new Vector3(0f, 0.63f, 0.02f);
                StoveCounter stove = c.AddComponent<StoveCounter>();
                list.Add(stove);
            }
            return list;
        }

        private void BuildPlateCounter()
        {
            GameObject c = MakeCounter("PlateCounter", new Vector3(3.5f, 0.5f, 0.5f), new Color(0.4f, 0.45f, 0.5f));
            c.AddComponent<PlateCounter>();
        }

        private DeliveryCounter BuildDeliveryCounter()
        {
            GameObject c = MakeCounter("DeliveryCounter", new Vector3(-3.5f, 0.5f, 0.5f), new Color(0.65f, 0.51f, 0.23f));
            Detail("Service mat", c.transform, new Vector3(0f, 0.57f, 0f), new Vector3(0.85f, 0.02f, 0.83f), Visual.VisualFactory.Surface("Oak"));
            // 出餐铃铛标识（金色小球）
            GameObject bell = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Collider bcol = bell.GetComponent<Collider>();
            if (bcol != null) { bcol.enabled = false; Destroy(bcol); }
            bell.name = "Bell";
            bell.transform.SetParent(c.transform, false);
            bell.transform.localPosition = new Vector3(0.3f, 0.64f, -0.3f);
            bell.transform.localScale = new Vector3(0.22f, 0.12f, 0.22f);
            bell.GetComponent<Renderer>().sharedMaterial = Visual.VisualFactory.MakeMaterial(new Color(1f, 0.8f, 0.2f), 0.7f, 0.4f);
            return c.AddComponent<DeliveryCounter>();
        }

        private void BuildTrashBin()
        {
            GameObject bin = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bin.name = "TrashBin";
            bin.transform.SetParent(transform, false);
            bin.transform.position = new Vector3(-3.5f, 0.35f, -1.5f);
            bin.transform.localScale = new Vector3(0.55f, 0.7f, 0.55f);
            bin.GetComponent<Renderer>().sharedMaterial = Visual.VisualFactory.Surface("Steel", new Vector2(3f, 1f));
            bin.AddComponent<TrashBin>();
            bin.AddComponent<InteractableHighlighter>();
            spawned.Add(bin);
        }

        private void BuildPrepCounters()
        {
            // 东西墙外侧各一组，中央两组备菜岛：俯视下就近取放，通道仍可绕行。
            Vector3[] spots =
            {
                new Vector3(-3.5f, 0.5f, -2.5f),
                new Vector3(3.5f, 0.5f, -2.5f),
                new Vector3(-3.5f, 0.5f, -0.5f),
                new Vector3(3.5f, 0.5f, -0.5f)
            };
            int count = Mathf.Clamp(prepCount, 0, spots.Length);
            for (int i = 0; i < count; i++)
            {
                GameObject station = MakeCounter("PrepCounter_" + i, spots[i], new Color(0.22f, 0.48f, 0.47f));
                station.AddComponent<PrepCounter>();
            }
        }

        private void ResetKitchen()
        {
            Visual.FeedbackFX.Clear();
            player.ResetForDay();
            foreach (CuttingCounter station in GetComponentsInChildren<CuttingCounter>()) station.ResetStation();
            foreach (StoveCounter station in GetComponentsInChildren<StoveCounter>()) station.ResetStation();
            foreach (PrepCounter station in GetComponentsInChildren<PrepCounter>()) station.ResetStation();
            foreach (PlateCounter station in GetComponentsInChildren<PlateCounter>()) station.ResetStation();
        }

        private void OnDestroy()
        {
            GameManager manager = GetComponent<GameManager>();
            if (manager != null) manager.DayStarting -= ResetKitchen;
            Visual.VisualFactory.ReleaseMaterials();
        }

        // ---------------- 玩家 ----------------

        private void BuildPlayer()
        {
            GameObject p = new GameObject("Player");
            p.transform.SetParent(transform, false);
            p.layer = LayerMask.NameToLayer("Ignore Raycast");
            p.transform.position = new Vector3(0f, 0.05f, 0f);

            CharacterController cc = p.AddComponent<CharacterController>();
            cc.height = 1.7f;
            cc.radius = 0.32f;
            cc.center = new Vector3(0f, 0.85f, 0f);
            cc.slopeLimit = 45f;

            player = p.AddComponent<PlayerController>();
            ChefModel chef = p.AddComponent<ChefModel>();
            player.Initialize(Camera.main, chef);
        }

        // ---------------- 系统 ----------------

        private void BuildSystems()
        {
            TimerSystem timer = gameObject.AddComponent<TimerSystem>();
            OrderSystem orders = gameObject.AddComponent<OrderSystem>();
            ScoreManager score = gameObject.AddComponent<ScoreManager>();
            GameManager gm = gameObject.AddComponent<GameManager>();
            gm.roundDuration = roundDuration;
            gm.startReputation = startReputation;

            GameplayHUD hud = gameObject.AddComponent<GameplayHUD>();

            List<Recipe> recipes = new List<Recipe>(Resources.LoadAll<Recipe>("Recipes"));
            if (recipes == null || recipes.Count == 0) recipes = RecipeFactory.CreateDefaultRecipes();

            gm.Initialize(timer, orders, score);
            orders.Initialize(recipes);
        }
    }
}
