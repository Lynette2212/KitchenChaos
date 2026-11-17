using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using KitchenChaos.Core;
using KitchenChaos.Gameplay;
using KitchenChaos.Visual;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace KitchenChaos.EditorTools
{
    // RunBatch owns its Editor lifetime; -quit would end it before Play Mode verification.
    [InitializeOnLoad]
    public static class KitchenSceneAuthoringTests
    {
        private const string Key = "KitchenChaos.SceneAuthoringTests.State";
        private const string ScenePath = "Assets/Scenes/KitchenChaos.unity";
        private const string CopyPrefix = "Assets/Scenes/__KitchenSceneAuthoring_";
        private const float PositionTolerance = 0.001f;
        private const float RotationTolerance = 0.05f;
        private const double RunTimeout = 180.0;
        private const double ExitPlayTimeout = 30.0;
        private static RunState state;
        private static bool inUpdate;

        private enum Phase { Preparing, WaitingForPlay, Sampling, WaitingForEdit }

        [Serializable]
        private sealed class Pose
        {
            public string path;
            public string label;
            public Vector3 position;
            public Quaternion rotation;
            public Vector3 scale;
            public Vector3 worldPosition;
            public Quaternion worldRotation;
            public Vector3 worldScale;
        }

        [Serializable]
        private sealed class TreeStamp
        {
            public string path;
            public string label;
            public string signature;
        }

        [Serializable]
        private sealed class RunState
        {
            public bool active;
            public string runId;
            public Phase phase;
            public long startedTicks;
            public long exitTicks;
            public int checks;
            public List<string> failures = new List<string>();
            public string originalHash;
            public string originalMetaHash;
            public string copyPath;
            public string copyGuid;
            public string copyHash;
            public string copyMetaHash;
            public bool ownsCopy;
            public bool savedExpectations;
            public bool enteredPlay;
            public bool returnedToEdit;
            public bool runtimeComplete;
            public int enteredFrame;
            public int lastFrame = -1;
            public int samples;
            public float sampleStartTime;
            public float minCustomerY = float.MaxValue;
            public float maxCustomerY = float.MinValue;
            public Quaternion firstCustomerRotation;
            public Quaternion firstHeadRotation;
            public float customerTurn;
            public float headTurn;
            public float cameraSize;
            public int plateCount;
            public Pose counter;
            public Pose player;
            public Pose chef;
            public Pose camera;
            public Pose customer;
            public Pose head;
            public Pose carryAnchor;
            public Pose plateAnchor;
            public List<Pose> stock = new List<Pose>();
            public List<TreeStamp> trees = new List<TreeStamp>();
        }

        private sealed class Layout
        {
            public Scene scene;
            public KitchenBuilder builder;
            public PlayerController player;
            public ChefModel chef;
            public Camera camera;
            public CustomerWindow window;
            public CustomerView[] customers;
            public PlateCounter plates;
            public Transform visual;
            public Transform head;
            public Transform counter;
            public Transform carryAnchor;
            public Transform plateAnchor;
            public Transform stackAnchor;
            public List<GameObject> stock;
        }

        static KitchenSceneAuthoringTests()
        {
            if (!Application.isBatchMode) return;
            string json = SessionState.GetString(Key, string.Empty);
            if (string.IsNullOrEmpty(json)) return;
            state = JsonUtility.FromJson<RunState>(json);
            if (state != null && state.active) Attach();
        }

        public static void RunBatch()
        {
            // These guards run before registering callbacks or touching any scene.
            if (!Application.isBatchMode || !HasArgument("-nographics"))
                throw new InvalidOperationException("Use a separate -batchmode -nographics Editor; never run this in an interactive Editor.");
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("RunBatch must start in Edit Mode.");
            if (state != null && state.active)
                throw new InvalidOperationException("A scene-authoring test is already active in this Editor session.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Refusing to discard an unsaved scene.");

            state = new RunState
            {
                active = true,
                runId = Guid.NewGuid().ToString("N"),
                startedTicks = DateTime.UtcNow.Ticks,
                phase = Phase.Preparing
            };
            SaveState();
            Attach();
            try
            {
                Require(!EditorSettings.enterPlayModeOptionsEnabled ||
                    (EditorSettings.enterPlayModeOptions & EnterPlayModeOptions.DisableSceneReload) == 0,
                    "Scene reload must be enabled; this test does not change Editor settings.");
                PrepareCopy();
                state.phase = Phase.WaitingForPlay;
                SaveState();
                EditorApplication.isPlaying = true;
            }
            catch (Exception exception)
            {
                RecordFailure("Preparation: " + exception);
                RequestFinish();
            }
        }

        private static bool HasArgument(string value)
        {
            foreach (string argument in Environment.GetCommandLineArgs())
                if (string.Equals(argument, value, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static void Attach()
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged -= PlayModeChanged;
            EditorApplication.playModeStateChanged += PlayModeChanged;
            Application.logMessageReceived -= CaptureError;
            Application.logMessageReceived += CaptureError;
            AssemblyReloadEvents.beforeAssemblyReload -= SaveState;
            AssemblyReloadEvents.beforeAssemblyReload += SaveState;
        }

        private static void SaveState()
        {
            if (state != null) SessionState.SetString(Key, JsonUtility.ToJson(state));
        }

        private static void PlayModeChanged(PlayModeStateChange change)
        {
            if (state == null || !state.active) return;
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                state.enteredPlay = true;
                state.enteredFrame = Time.frameCount;
            }
            if (change == PlayModeStateChange.EnteredEditMode)
            {
                state.returnedToEdit = true;
                if (state.phase != Phase.WaitingForEdit)
                {
                    RecordFailure("Play Mode ended before the runtime checks completed.");
                    state.phase = Phase.WaitingForEdit;
                    state.exitTicks = DateTime.UtcNow.Ticks;
                }
            }
            SaveState();
        }

        private static void CaptureError(string message, string trace, LogType type)
        {
            if (state == null || !state.active) return;
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                RecordFailure(type + ": " + message + "\n" + trace);
        }

        private static void Tick()
        {
            if (state == null || !state.active || inUpdate) return;
            inUpdate = true;
            try
            {
                if (state.phase == Phase.WaitingForEdit)
                {
                    if (!EditorApplication.isPlayingOrWillChangePlaymode && state.returnedToEdit)
                        FinishInEditMode();
                    else if (SecondsSince(state.exitTicks) > ExitPlayTimeout)
                    {
                        RecordFailure("Timed out leaving Play Mode. Post-Play verification and safe scene-copy cleanup could not run.");
                        ReportAndExit(2);
                    }
                    return;
                }
                if (SecondsSince(state.startedTicks) > RunTimeout)
                {
                    RecordFailure("Timed out during " + state.phase + "; required coverage is incomplete.");
                    RequestFinish();
                    return;
                }
                if (GameManager.Instance != null) GameManager.Instance.SaveRecords = false;
                if (!EditorApplication.isPlaying || !state.enteredPlay) return;
                Require(SceneManager.GetActiveScene().path == state.copyPath, "Play Mode is running only the owned scene copy");
                if (state.phase == Phase.WaitingForPlay)
                {
                    GameManager gm = GameManager.Instance;
                    if (Time.frameCount < state.enteredFrame + 3 || gm == null || gm.Timer == null ||
                        gm.Orders == null || gm.Score == null) return;
                    Layout layout = InspectLayout(SceneManager.GetActiveScene());
                    Require(layout.player.ViewCamera == layout.camera && layout.player.Chef == layout.chef,
                        "KitchenBuilder.Start initialized Player with the authored camera and ChefModel");
                    Require(gm.State == GameState.Ready, "Initialization leaves the game Ready");
                    VerifyRuntimeStructure(layout, "after Start");
                    VerifySpawn(layout.player, "initial Awake spawn");
                    state.sampleStartTime = Time.time;
                    state.phase = Phase.Sampling;
                }
                if (state.phase == Phase.Sampling && Time.frameCount != state.lastFrame &&
                    Time.time - state.sampleStartTime >= state.samples * 0.12f)
                {
                    state.lastFrame = Time.frameCount;
                    SampleRuntime();
                    if (state.samples >= 24 && Time.time - state.sampleStartTime >= 2.5f)
                    {
                        Expect(state.maxCustomerY - state.minCustomerY > 0.0005f,
                            "Customer Update actually animated across sampled frames (not a vacuous bound check)");
                        Expect(state.customerTurn > 0.05f && state.headTurn > 0.05f,
                            "Customer root and head actually turned across sampled frames");
                        TestDayReset();
                        state.runtimeComplete = true;
                        RequestFinish();
                    }
                }
                SaveState();
            }
            catch (Exception exception)
            {
                RecordFailure("Test phase " + state.phase + ": " + exception);
                RequestFinish();
            }
            finally { inUpdate = false; }
        }

        private static double SecondsSince(long ticks) => (DateTime.UtcNow.Ticks - ticks) / (double)TimeSpan.TicksPerSecond;

        private static void PrepareCopy()
        {
            Require(File.Exists(Absolute(ScenePath)), "The formal scene already exists; no bake tool is invoked");
            state.originalHash = FileHash(Absolute(ScenePath));
            state.originalMetaHash = FileHash(Absolute(ScenePath) + ".meta");
            SaveState();
            Scene original = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Layout formal = InspectLayout(original);
            VerifyPersistentAssets(original);
            Require(state.failures.Count == 0, "Formal scene validation passed before creating a test copy");
            Require(formal.builder.HasSceneLayout, "The formal KitchenBuilder has a baked layout");

            state.copyPath = CopyPrefix + state.runId + ".unity";
            string destination = Absolute(state.copyPath);
            Require(!File.Exists(destination) && !File.Exists(destination + ".meta") &&
                string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(state.copyPath)), "Unique test-copy path is unused");
            SaveState();
            // CreateNew is exclusive: neither a stale test scene nor its meta can be overwritten.
            using (var target = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                state.ownsCopy = true;
                SaveState();
                using (var source = File.OpenRead(Absolute(ScenePath))) source.CopyTo(target);
            }
            state.copyHash = FileHash(destination);
            SaveState();
            AssetDatabase.ImportAsset(state.copyPath, ImportAssetOptions.ForceSynchronousImport);
            state.copyGuid = AssetDatabase.AssetPathToGUID(state.copyPath);
            state.copyMetaHash = FileHash(destination + ".meta");
            SaveState();
            Require(!string.IsNullOrEmpty(state.copyGuid), "Unity imported the owned scene copy with a new GUID");
            Require(state.copyGuid != AssetDatabase.AssetPathToGUID(ScenePath), "Copy and formal scene have different GUIDs");

            Scene copy = EditorSceneManager.OpenScene(state.copyPath, OpenSceneMode.Single);
            Layout layout = InspectLayout(copy);
            ChangeLocal(layout.counter, new Vector3(0.23f, 0.07f, -0.19f), new Vector3(4f, 23f, -3f), new Vector3(1.07f, 0.96f, 1.03f));
            layout.player.transform.position += new Vector3(0.67f, 0.31f, 0.43f);
            layout.player.transform.rotation = Quaternion.Euler(0f, 73f, 0f);
            layout.player.transform.localScale = Vector3.Scale(layout.player.transform.localScale, new Vector3(1.04f, 1.02f, 0.97f));
            ChangeLocal(layout.visual, new Vector3(0.12f, 0.09f, -0.08f), new Vector3(3f, 19f, -4f), new Vector3(1.11f, 0.93f, 1.06f));
            ChangeLocal(layout.camera.transform, new Vector3(0.61f, 0.47f, -0.36f), new Vector3(7f, 17f, 3f), new Vector3(1.02f, 1.02f, 1.02f));
            layout.camera.orthographicSize += 0.73f;
            ChangeLocal(layout.customers[0].transform, new Vector3(0.21f, 0.12f, -0.17f), new Vector3(5f, 31f, -4f), new Vector3(1.08f, 0.94f, 1.03f));
            ChangeLocal(layout.head, new Vector3(0.04f, 0.06f, -0.03f), new Vector3(11f, -27f, 8f), new Vector3(1.05f, 0.96f, 1.04f));
            // An authored stock sentinel also detects destroy/rebuild with identical plate counts.
            ChangeLocal(layout.stock[0].transform, new Vector3(0.017f, 0.013f, -0.019f), new Vector3(0f, 13f, 0f), new Vector3(0.97f, 1.03f, 1.02f));
            ChangeLocal(layout.carryAnchor, new Vector3(0.03f, 0.02f, -0.01f), new Vector3(0f, 7f, 0f), Vector3.one);
            ChangeLocal(layout.plateAnchor, new Vector3(-0.02f, 0.03f, 0.01f), new Vector3(0f, -9f, 0f), Vector3.one);
            CaptureExpectations(layout);
            Require(Vector3.Distance(state.player.worldPosition, new Vector3(0f, 0.05f, -0.6f)) > 0.3f,
                "Test spawn is distinct from the old hard-coded spawn");
            Require(Quaternion.Angle(state.player.worldRotation, Quaternion.identity) > 30f,
                "Test spawn facing is non-default");
            EditorSceneManager.MarkSceneDirty(copy);
            Require(EditorSceneManager.SaveScene(copy, state.copyPath), "Saved only the owned scene copy");
            state.copyHash = FileHash(destination);
            state.savedExpectations = true;
            SaveState();
            // OpenScene(Single) closes the copy. Reopening must deserialize the values from disk.
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            copy = EditorSceneManager.OpenScene(state.copyPath, OpenSceneMode.Single);
            InspectLayout(copy);
            VerifySavedLayout("save / close / reopen");
            Require(state.failures.Count == 0, "Round-trip checks passed before entering Play Mode");
        }

        private static Layout InspectLayout(Scene scene)
        {
            var layout = new Layout { scene = scene };
            layout.builder = Single<KitchenBuilder>(scene);
            Require(layout.builder.name == "KitchenChaos" && layout.builder.transform.parent == null,
                "KitchenChaos is the root containing KitchenBuilder");
            Require(layout.builder.HasSceneLayout, "KitchenBuilder.HasSceneLayout is true");
            foreach (string group in new[] { "Environment", "Stations", "Actors", "Camera", "Lighting", "Systems" })
                Require(layout.builder.transform.Find(group) != null, "Authored root group exists: " + group);
            foreach (Transform transform in Components<Transform>(scene))
                Expect(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) == 0,
                    "No missing scripts: " + transform.name);

            layout.player = Reference<PlayerController>(layout.builder, "player");
            layout.camera = Reference<Camera>(layout.builder, "sceneCamera");
            Require(layout.player == Single<PlayerController>(scene), "Builder has the one serialized scene Player");
            Require(layout.camera == Single<Camera>(scene) && layout.camera.CompareTag("MainCamera") &&
                layout.camera.orthographic && layout.camera.isActiveAndEnabled,
                "Builder has the one active serialized orthographic MainCamera");
            Require(layout.camera.GetComponent<TopDownCamera>() != null && layout.camera.GetComponent<TopDownCamera>().isActiveAndEnabled,
                "Authored camera has an active TopDownCamera");
            layout.chef = Single<ChefModel>(scene);
            Require(layout.chef.gameObject == layout.player.gameObject && layout.chef.isActiveAndEnabled && layout.player.isActiveAndEnabled,
                "The active ChefModel belongs to the active Player");
            layout.visual = Reference<Transform>(layout.chef, "visual");
            Require(layout.visual.name == "ChefVisual" && layout.visual.IsChildOf(layout.player.transform) &&
                layout.visual.GetComponentsInChildren<Renderer>(true).Length > 0, "Serialized rendered ChefVisual is authored under Player");
            layout.carryAnchor = Reference<Transform>(layout.player, "carryAnchor");
            layout.plateAnchor = Reference<Transform>(layout.player, "plateAnchor");
            Require(layout.carryAnchor == layout.player.transform.Find("HeldItems") &&
                layout.plateAnchor == layout.player.transform.Find("HeldPlateAnchor"), "Player serializes the existing carry and plate anchors");
            Require(layout.player.transform.IsChildOf(layout.builder.transform.Find("Actors")) &&
                layout.camera.transform.IsChildOf(layout.builder.transform.Find("Camera")), "Player and camera are in the authored groups");

            layout.window = Single<CustomerWindow>(scene);
            List<CustomerView> people = References<CustomerView>(layout.window, "people");
            Require(people.Count == 3 && new HashSet<CustomerView>(people).Count == 3 && Components<CustomerView>(scene).Count == 3,
                "CustomerWindow serializes exactly three distinct CustomerViews");
            layout.customers = people.ToArray();
            foreach (CustomerView person in people)
            {
                Require(person.isActiveAndEnabled && person.transform.IsChildOf(layout.window.transform), "Each authored customer is active under CustomerWindow");
                foreach (CustomerView other in people)
                    if (person != other) Require(!person.transform.IsChildOf(other.transform), "CustomerViews have independent roots");
                Transform head = Reference<Transform>(person, "head");
                Require(head.name == "Head" && head != person.transform && head.IsChildOf(person.transform), "Each customer serializes its own Head");
            }
            layout.head = Reference<Transform>(people[0], "head");
            layout.plates = Single<PlateCounter>(scene);
            layout.stackAnchor = Reference<Transform>(layout.plates, "stackAnchor");
            layout.stock = References<GameObject>(layout.plates, "plateVisuals");
            Require(layout.plates.PlateCount > 0 && layout.stock.Count == layout.plates.PlateCount &&
                new HashSet<GameObject>(layout.stock).Count == layout.stock.Count, "Plate stock is non-empty, distinct and serialized at the stock count");
            Require(layout.stackAnchor.IsChildOf(layout.plates.transform) && layout.stackAnchor.childCount == layout.stock.Count,
                "Plate stack contains exactly the serialized stock");
            foreach (GameObject disk in layout.stock)
                Require(disk.name == "Plate" && disk.transform.parent == layout.stackAnchor && disk.GetComponentsInChildren<Renderer>(true).Length > 0,
                    "Each stock plate is an authored rendered child of the stack anchor");

            Transform stations = layout.builder.transform.Find("Stations");
            foreach (string name in new[] { "Container_Bun", "Container_MeatPatty", "Container_Cheese", "Container_Tomato",
                "Container_Lettuce", "Container_Onion", "Container_Mushroom", "PlateCounter", "DeliveryCounter", "TrashBin" })
                Require(stations.Find(name) != null, "Legacy station name retained: " + name);
            for (int i = 0; i < layout.builder.cuttingCount; i++) Require(stations.Find("CuttingCounter_" + i) != null, "Legacy cutting counter " + i);
            for (int i = 0; i < layout.builder.stoveCount; i++) Require(stations.Find("StoveCounter_" + i) != null, "Legacy stove counter " + i);
            for (int i = 0; i < Mathf.Clamp(layout.builder.prepCount, 0, 4); i++) Require(stations.Find("PrepCounter_" + i) != null, "Legacy prep counter " + i);
            layout.counter = stations.Find("Container_Tomato");
            return layout;
        }

        private static void VerifyPersistentAssets(Scene scene)
        {
            List<Renderer> renderers = Components<Renderer>(scene);
            Require(renderers.Count > 0, "Formal scene has renderers before Play Mode");
            var materials = new HashSet<Material>();
            foreach (Renderer renderer in renderers)
            {
                Material[] shared = renderer.sharedMaterials;
                Expect(shared.Length > 0, "Renderer has shared materials: " + renderer.name);
                foreach (Material material in shared)
                {
                    Expect(material != null && EditorUtility.IsPersistent(material), "Persistent shared material: " + renderer.name);
                    if (material == null || !materials.Add(material)) continue;
                    foreach (string property in material.GetTexturePropertyNames())
                    {
                        Texture texture = material.GetTexture(property);
                        if (texture != null) Expect(EditorUtility.IsPersistent(texture), "Persistent material texture: " + material.name + "/" + property);
                    }
                }
                if (renderer is MeshRenderer)
                    Require(renderer.GetComponent<MeshFilter>() != null, "MeshRenderer has a MeshFilter: " + renderer.name);
            }
            foreach (MeshFilter filter in Components<MeshFilter>(scene)) PersistentMesh(filter.sharedMesh, filter.name);
            foreach (SkinnedMeshRenderer renderer in Components<SkinnedMeshRenderer>(scene)) PersistentMesh(renderer.sharedMesh, renderer.name);
            foreach (MeshCollider collider in Components<MeshCollider>(scene)) PersistentMesh(collider.sharedMesh, collider.name + " collider");
        }

        private static void PersistentMesh(Mesh mesh, string label)
        {
            Expect(mesh != null && EditorUtility.IsPersistent(mesh), "Persistent shared mesh: " + label);
        }

        private static void CaptureExpectations(Layout layout)
        {
            state.counter = Capture(layout.counter);
            state.player = Capture(layout.player.transform);
            state.chef = Capture(layout.visual);
            state.camera = Capture(layout.camera.transform);
            state.customer = Capture(layout.customers[0].transform);
            state.head = Capture(layout.head);
            state.carryAnchor = Capture(layout.carryAnchor);
            state.plateAnchor = Capture(layout.plateAnchor);
            state.cameraSize = layout.camera.orthographicSize;
            state.plateCount = layout.plates.PlateCount;
            foreach (GameObject disk in layout.stock) state.stock.Add(Capture(disk.transform));
            // Runtime audio voices and FX pools are not part of the saved visual layout.
            foreach (string group in new[] { "Environment", "Stations", "Actors", "Camera", "Lighting" })
            {
                Transform root = layout.builder.transform.Find(group);
                state.trees.Add(new TreeStamp { path = ObjectPath(root), label = group, signature = TreeSignature(root) });
            }
        }

        private static void ChangeLocal(Transform transform, Vector3 offset, Vector3 euler, Vector3 scale)
        {
            transform.localPosition += offset;
            transform.localRotation *= Quaternion.Euler(euler);
            transform.localScale = Vector3.Scale(transform.localScale, scale);
            EditorUtility.SetDirty(transform);
        }

        private static Pose Capture(Transform transform)
        {
            return new Pose
            {
                path = ObjectPath(transform), label = transform.name,
                position = transform.localPosition, rotation = transform.localRotation, scale = transform.localScale,
                worldPosition = transform.position, worldRotation = transform.rotation, worldScale = transform.lossyScale
            };
        }

        // Sibling-index paths disambiguate repeated FloorTile and Plate names without adding test components.
        private static string ObjectPath(Transform transform)
        {
            string path = transform.GetSiblingIndex().ToString();
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = transform.GetSiblingIndex() + "/" + path;
            }
            return path;
        }

        private static Transform Resolve(string path)
        {
            string[] indices = path.Split('/');
            int rootIndex = int.Parse(indices[0]);
            Transform result = null;
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
                if (root.transform.GetSiblingIndex() == rootIndex) { result = root.transform; break; }
            Require(result != null, "Saved root still exists: " + path);
            for (int i = 1; i < indices.Length; i++)
            {
                int index = int.Parse(indices[i]);
                Require(index < result.childCount, "Saved child still exists: " + path);
                result = result.GetChild(index);
            }
            return result;
        }

        private static Transform Resolve(Pose pose)
        {
            Transform transform = Resolve(pose.path);
            Require(transform.name == pose.label, "Saved object identity/name: " + pose.label);
            return transform;
        }

        private static void VerifyPose(Pose pose, string stage, bool world = false)
        {
            Transform transform = Resolve(pose);
            string label = stage + ": " + pose.label;
            Expect(Near(transform.localPosition, pose.position), label + " local position");
            Expect(Quaternion.Angle(transform.localRotation, pose.rotation) <= RotationTolerance, label + " local rotation");
            Expect(Near(transform.localScale, pose.scale), label + " local scale");
            if (!world) return;
            Expect(Near(transform.position, pose.worldPosition), label + " world position");
            Expect(Quaternion.Angle(transform.rotation, pose.worldRotation) <= RotationTolerance, label + " world rotation");
            Expect(Near(transform.lossyScale, pose.worldScale), label + " world scale");
        }

        private static bool Near(Vector3 left, Vector3 right) => Vector3.Distance(left, right) <= PositionTolerance;

        private static void VerifySavedLayout(string stage)
        {
            Require(SceneManager.GetActiveScene().path == state.copyPath, stage + ": checking the owned copy");
            foreach (Pose pose in new[] { state.counter, state.player, state.chef, state.camera, state.customer, state.head,
                state.carryAnchor, state.plateAnchor }) VerifyPose(pose, stage, true);
            foreach (Pose pose in state.stock) VerifyPose(pose, stage, true);
            Expect(Mathf.Abs(Resolve(state.camera).GetComponent<Camera>().orthographicSize - state.cameraSize) < 0.0001f,
                stage + ": orthographicSize persisted");
            VerifyTrees(stage);
        }

        private static void SampleRuntime()
        {
            VerifyPose(state.counter, "runtime counter", true);
            VerifyPose(state.camera, "runtime camera without shake", true);
            Expect(Mathf.Abs(Resolve(state.camera).GetComponent<Camera>().orthographicSize - state.cameraSize) < 0.0001f,
                "LateUpdate retains authored orthographicSize");
            VerifyPose(state.chef, "idle ChefVisual base TRS");
            VerifyPose(state.carryAnchor, "existing HeldItems");
            VerifyPose(state.plateAnchor, "existing HeldPlateAnchor");
            foreach (Pose plate in state.stock) VerifyPose(plate, "pre-existing plate stock");
            Transform customer = Resolve(state.customer);
            Transform head = Resolve(state.head);
            Vector3 offset = customer.localPosition - state.customer.position;
            Expect(Mathf.Abs(offset.x) <= PositionTolerance && Mathf.Abs(offset.z) <= PositionTolerance && Mathf.Abs(offset.y) <= 0.016f,
                "Customer animation is relative to saved home (y <= .016, no x/z drift)");
            Expect(Quaternion.Angle(customer.localRotation, state.customer.rotation) <= 4.1f,
                "Customer root rotation stays within 4.1 degrees of authored rotation");
            Expect(Quaternion.Angle(head.localRotation, state.head.rotation) <= 10.1f,
                "Customer head rotation stays within 10.1 degrees of authored rotation");
            Expect(Near(customer.localScale, state.customer.scale) && Near(head.localScale, state.head.scale) && Near(head.localPosition, state.head.position),
                "Customer root/head scale and head local position are retained");
            if (state.samples == 0)
            {
                state.firstCustomerRotation = customer.localRotation;
                state.firstHeadRotation = head.localRotation;
            }
            state.customerTurn = Mathf.Max(state.customerTurn, Quaternion.Angle(customer.localRotation, state.firstCustomerRotation));
            state.headTurn = Mathf.Max(state.headTurn, Quaternion.Angle(head.localRotation, state.firstHeadRotation));
            state.minCustomerY = Mathf.Min(state.minCustomerY, customer.localPosition.y);
            state.maxCustomerY = Mathf.Max(state.maxCustomerY, customer.localPosition.y);
            state.samples++;
        }

        private static void TestDayReset()
        {
            Layout layout = InspectLayout(SceneManager.GetActiveScene());
            GameManager gm = GameManager.Instance;
            gm.SaveRecords = false;
            int dayStartingCalls = 0;
            Action onDayStarting = () => dayStartingCalls++;
            gm.DayStarting += onDayStarting;
            try
            {
                DisplacePlayer(layout.player);
                gm.StartGame();
                Expect(gm.IsPlaying && dayStartingCalls == 1, "StartGame actually starts a day and raises DayStarting");
                VerifySpawn(layout.player, "DayStarting / ResetKitchen");
                DisplacePlayer(layout.player);
                layout.player.ResetForDay();
                VerifySpawn(layout.player, "direct ResetForDay");
                // Read immediately after reset, before normal CharacterController gravity can settle the player.
                gm.ReturnToMenu();
                VerifySpawn(layout.player, "ReturnToMenu reset");
                VerifyRuntimeStructure(layout, "after day/reset");
                SampleRuntime();
            }
            finally { gm.DayStarting -= onDayStarting; }
        }

        private static void DisplacePlayer(PlayerController player)
        {
            CharacterController controller = player.GetComponent<CharacterController>();
            bool enabled = controller != null && controller.enabled;
            if (enabled) controller.enabled = false;
            player.transform.SetPositionAndRotation(state.player.worldPosition + new Vector3(0.81f, 0.42f, -0.71f), Quaternion.Euler(0f, -49f, 0f));
            if (enabled) controller.enabled = true;
            Require(!Near(player.transform.position, state.player.worldPosition) &&
                Quaternion.Angle(player.transform.rotation, state.player.worldRotation) > 30f,
                "Player was genuinely displaced and turned before reset");
        }

        private static void VerifySpawn(PlayerController player, string stage)
        {
            Expect(Near(player.transform.position, state.player.worldPosition), stage + ": returns to authored world spawn position");
            Expect(Quaternion.Angle(player.transform.rotation, state.player.worldRotation) <= RotationTolerance,
                stage + ": returns to authored world spawn facing");
            Expect(Near(player.transform.lossyScale, state.player.worldScale), stage + ": authored spawn scale is retained");
            Expect(Vector3.Distance(player.transform.position, new Vector3(0f, 0.05f, -0.6f)) > 0.3f,
                stage + ": does not return to the old hard-coded spawn");
        }

        private static void VerifyRuntimeStructure(Layout layout, string stage)
        {
            VerifyTrees(stage);
            // Include inactive and DontDestroyOnLoad objects, but not persistent project assets.
            Expect(LiveCount<KitchenBuilder>() == 1 && LiveCount<PlayerController>() == 1 && LiveCount<ChefModel>() == 1,
                stage + ": no extra kitchen, Player or ChefModel");
            Expect(LiveCount<Camera>() == 1 && LiveCount<CustomerWindow>() == 1 && LiveCount<CustomerView>() == 3 && LiveCount<PlateCounter>() == 1,
                stage + ": no extra cameras, customers or plate counters");
            int visuals = 0;
            int disks = 0;
            foreach (Transform transform in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (!transform.gameObject.scene.IsValid() || !transform.gameObject.scene.isLoaded ||
                    EditorSceneManager.IsPreviewScene(transform.gameObject.scene) || EditorUtility.IsPersistent(transform)) continue;
                if (transform.name == "ChefVisual" || transform.name.StartsWith("ChefVisual(Clone)", StringComparison.Ordinal)) visuals++;
                if (transform.name == "Plate" || transform.name.StartsWith("Plate(Clone)", StringComparison.Ordinal)) disks++;
            }
            Expect(visuals == 1 && disks == state.plateCount, stage + ": no additional ChefVisual or stock-plate clones");
            Expect(layout.plates.PlateCount == state.plateCount && layout.stackAnchor.childCount == state.stock.Count,
                stage + ": Start/reset did not duplicate or consume pre-authored stock");
            foreach (Pose plate in state.stock) VerifyPose(plate, stage + " stock sentinel");
        }

        private static int LiveCount<T>() where T : Component
        {
            int count = 0;
            foreach (T component in Resources.FindObjectsOfTypeAll<T>())
                if (component.gameObject.scene.IsValid() && component.gameObject.scene.isLoaded && !EditorUtility.IsPersistent(component) &&
                    !EditorSceneManager.IsPreviewScene(component.gameObject.scene)) count++;
            return count;
        }

        private static string TreeSignature(Transform root)
        {
            var result = new StringBuilder();
            AppendTree(root, result);
            return result.ToString();
        }

        private static void AppendTree(Transform transform, StringBuilder result)
        {
            result.Append('[').Append(transform.name.Length).Append(':').Append(transform.name);
            foreach (Component component in transform.GetComponents<Component>())
                result.Append('|').Append(component == null ? "MISSING" : component.GetType().FullName);
            foreach (Transform child in transform) AppendTree(child, result);
            result.Append(']');
        }

        private static void VerifyTrees(string stage)
        {
            foreach (TreeStamp tree in state.trees)
                Expect(TreeSignature(Resolve(tree.path)) == tree.signature,
                    stage + ": authored " + tree.label + " hierarchy/component counts are unchanged (no generated duplicates)");
        }

        private static List<T> Components<T>(Scene scene) where T : Component
        {
            var result = new List<T>();
            foreach (GameObject root in scene.GetRootGameObjects()) result.AddRange(root.GetComponentsInChildren<T>(true));
            return result;
        }

        private static T Single<T>(Scene scene) where T : Component
        {
            List<T> values = Components<T>(scene);
            Require(values.Count == 1, "Exactly one authored " + typeof(T).Name + " (found " + values.Count + ")");
            return values[0];
        }

        private static T Reference<T>(Object owner, string field) where T : Object
        {
            using (var serialized = new SerializedObject(owner))
            {
                SerializedProperty property = serialized.FindProperty(field);
                Require(property != null && property.propertyType == SerializedPropertyType.ObjectReference,
                    owner.name + "." + field + " is a serialized object reference");
                T value = property.objectReferenceValue as T;
                Require(value != null, owner.name + "." + field + " is assigned");
                Require(SameScene(owner, value), owner.name + "." + field + " references this scene, not a prefab asset");
                return value;
            }
        }

        private static List<T> References<T>(Object owner, string field) where T : Object
        {
            using (var serialized = new SerializedObject(owner))
            {
                SerializedProperty property = serialized.FindProperty(field);
                Require(property != null && property.isArray, owner.name + "." + field + " is a serialized list/array");
                var result = new List<T>();
                for (int i = 0; i < property.arraySize; i++)
                {
                    SerializedProperty element = property.GetArrayElementAtIndex(i);
                    Require(element.propertyType == SerializedPropertyType.ObjectReference, field + " contains serialized object references");
                    T value = element.objectReferenceValue as T;
                    Require(value != null && SameScene(owner, value), field + "[" + i + "] references an authored scene object");
                    result.Add(value);
                }
                return result;
            }
        }

        private static bool SameScene(Object owner, Object value)
        {
            GameObject left = owner is Component a ? a.gameObject : owner as GameObject;
            GameObject right = value is Component b ? b.gameObject : value as GameObject;
            return left != null && right != null && right.scene.IsValid() && left.scene == right.scene && !EditorUtility.IsPersistent(value);
        }

        private static void RequestFinish()
        {
            if (state.phase != Phase.WaitingForEdit)
            {
                state.phase = Phase.WaitingForEdit;
                state.exitTicks = DateTime.UtcNow.Ticks;
                state.returnedToEdit = !EditorApplication.isPlayingOrWillChangePlaymode;
            }
            SaveState();
            if (GameManager.Instance != null) GameManager.Instance.SaveRecords = false;
            if (EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.isPlaying = false;
        }

        private static void FinishInEditMode()
        {
            try
            {
                if (!state.runtimeComplete)
                    RecordFailure("NOT COVERED: runtime initialization, animation and/or day reset sequence did not complete.");
                if (state.savedExpectations && state.enteredPlay)
                {
                    VerifySavedLayout("after exiting Play Mode");
                    Expect(FileHash(Absolute(state.copyPath)) == state.copyHash, "Play Mode did not write runtime state into the saved copy");
                }
                else RecordFailure("NOT COVERED: post-Play saved-layout verification.");
            }
            catch (Exception exception) { RecordFailure("Post-Play verification: " + exception); }
            finally
            {
                bool originalOpened = false;
                try
                {
                    EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                    originalOpened = SceneManager.GetActiveScene().path == ScenePath;
                    Expect(originalOpened, "Reopened the formal scene before cleanup");
                    Expect(FileHash(Absolute(ScenePath)) == state.originalHash, "Formal scene bytes were never changed");
                    Expect(FileHash(Absolute(ScenePath) + ".meta") == state.originalMetaHash, "Formal scene meta was never changed");
                }
                catch (Exception exception) { RecordFailure("Reopening formal scene: " + exception); }
                try
                {
                    if (originalOpened) DeleteOwnedCopy();
                    else if (state.ownsCopy) RecordFailure("Owned copy retained because the formal scene could not be reopened: " + state.copyPath);
                }
                catch (Exception exception) { RecordFailure("Owned-copy cleanup: " + exception); }
                ReportAndExit(state.failures.Count == 0 ? 0 : 1);
            }
        }

        private static void DeleteOwnedCopy()
        {
            if (!state.ownsCopy) return;
            Require(state.copyPath == CopyPrefix + state.runId + ".unity" && state.copyPath != ScenePath &&
                Guid.TryParseExact(state.runId, "N", out _), "Cleanup is restricted to this invocation's exact copy path");
            string path = Absolute(state.copyPath);
            if (!File.Exists(path) && !File.Exists(path + ".meta"))
            {
                RecordFailure("The owned scene copy disappeared before cleanup: " + state.copyPath);
                return;
            }
            if (!string.IsNullOrEmpty(state.copyGuid))
            {
                Require(AssetDatabase.AssetPathToGUID(state.copyPath) == state.copyGuid &&
                    FileHash(path + ".meta") == state.copyMetaHash && FileHash(path) == state.copyHash,
                    "Copy GUID and file hashes still match ownership; unknown replacements must not be deleted");
                Require(AssetDatabase.DeleteAsset(state.copyPath), "Deleted only this run's scene copy and Unity meta");
            }
            else
            {
                // Only an unimported file exclusively created above can reach this fallback.
                Require(!File.Exists(path + ".meta"), "Refuse cleanup of an unverified meta/GUID");
                File.Delete(path);
            }
            Expect(!File.Exists(path) && !File.Exists(path + ".meta"), "Owned copy and meta are gone");
            state.ownsCopy = false;
            SaveState();
        }

        private static string Absolute(string assetPath)
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
        }

        private static string FileHash(string path)
        {
            if (!File.Exists(path)) return string.Empty;
            using (SHA256 hash = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
                return Convert.ToBase64String(hash.ComputeHash(stream));
        }

        private static void Expect(bool condition, string label)
        {
            state.checks++;
            if (!condition) RecordFailure("FAIL: " + label);
        }

        private static void Require(bool condition, string label)
        {
            Expect(condition, label);
            if (!condition) throw new InvalidOperationException(label);
        }

        private static void RecordFailure(string message)
        {
            state.failures.Add(message);
            SaveState();
        }

        private static void ReportAndExit(int exitCode)
        {
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= PlayModeChanged;
            Application.logMessageReceived -= CaptureError;
            AssemblyReloadEvents.beforeAssemblyReload -= SaveState;
            state.active = false;
            SessionState.EraseString(Key);
            string result = "KITCHEN SCENE AUTHORING: " + state.checks + " checks, " + state.failures.Count + " failures\n";
            result += state.failures.Count == 0 ? "ALL PASS" : string.Join("\n", state.failures);
            Debug.Log(result);
            Debug.Log("AUTHORING COVERAGE: " + state.samples + " distinct-frame samples; no bake calls. " +
                "Generation regressions are checked through saved pose/stock sentinels and hierarchy/component counts, not method-call instrumentation. " +
                "Runtime audio/FX pools and intentional material instances are excluded from baked-layout immutability. " +
                "Headless tests do not cover rendered appearance, user input, or recovery from a blocked Editor main thread.");
            EditorApplication.Exit(exitCode);
        }
    }
}
