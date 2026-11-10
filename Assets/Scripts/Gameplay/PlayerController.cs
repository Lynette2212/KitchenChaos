using System.Collections.Generic;
using UnityEngine;
using KitchenChaos.Data;
using KitchenChaos.Core;
using KitchenChaos.Audio;
using KitchenChaos.Visual;

namespace KitchenChaos.Gameplay
{
    [DefaultExecutionOrder(-10)]
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        public float moveSpeed = 3.2f;
        public float interactRange = 1.45f;

        private const float DashCooldown = 3f;
        private const float DashDuration = 0.18f;
        private const float DashSpeedMultiplier = 2.6f;
        public const int HeldItemLayer = 2;
        private static readonly Vector3 CarryAnchorPosition = new Vector3(0.22f, 1.62f, 0.02f);
        private static readonly Vector3 PlateAnchorPosition = new Vector3(-0.22f, 1.60f, 0.02f);

        private CharacterController controller;
        private IngredientItem carried;
        private PlateHandle plate;
        private IInteractable currentTarget;
        private InteractableHighlighter currentHighlight;
        private readonly List<IInteractable> interactables = new List<IInteractable>();
        private Transform carryAnchor;
        private Transform plateAnchor;
        private GameManager manager;
        private Vector3 moveForward = Vector3.forward;
        private Vector3 moveRight = Vector3.right;
        private Vector3 dashDirection;
        private float dashRemaining;
        private float dashCooldownRemaining;
        private float footstepDistance;
        private const float FootstepStride = 1.45f;
        private bool hasFocus = true;
        private int inputBlockedFrame = -1;
        private float lastRejectSound = -10f;

        public bool HasItem => carried != null;
        public IngredientItem CarriedItem => carried;
        public PlateHandle Plate => plate;
        public bool HasPlate => plate != null;
        public bool HandsBusy => HasItem || HasPlate;
        public IInteractable CurrentTarget => currentTarget;
        public bool CanInteract => isActiveAndEnabled && GameManager.Instance != null && GameManager.Instance.IsPlaying;
        public float DashReady01 => Mathf.Clamp01(1f - dashCooldownRemaining / DashCooldown);
        public bool IsDashing => CanInteract && dashRemaining > 0f;
        public Camera ViewCamera { get; private set; }
        public ChefModel Chef { get; private set; }

        private void Awake() => controller = GetComponent<CharacterController>();

        private void OnEnable()
        {
            manager = GameManager.Instance;
            if (manager != null) manager.StateChanged += OnGameStateChanged;
            ApplyCursorState();
        }

        public void Initialize(Camera viewCamera, ChefModel chef)
        {
            ViewCamera = viewCamera;
            Chef = chef;
            if (viewCamera != null)
            {
                moveForward = Vector3.ProjectOnPlane(viewCamera.transform.forward, Vector3.up).normalized;
                moveRight = Vector3.ProjectOnPlane(viewCamera.transform.right, Vector3.up).normalized;
            }
            carryAnchor = new GameObject("HeldItems").transform;
            carryAnchor.SetParent(transform, false);
            carryAnchor.localPosition = CarryAnchorPosition;
            carryAnchor.gameObject.layer = HeldItemLayer;
            plateAnchor = new GameObject("HeldPlateAnchor").transform;
            plateAnchor.SetParent(transform, false);
            plateAnchor.localPosition = PlateAnchorPosition;
            plateAnchor.gameObject.layer = HeldItemLayer;
            CacheInteractables();
            transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        }

        private void CacheInteractables()
        {
            interactables.Clear();
            foreach (MonoBehaviour behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (behaviour == null || behaviour.gameObject.scene != gameObject.scene) continue;
                if (behaviour is IInteractable target) interactables.Add(target);
            }
        }

        private void OnGameStateChanged(GameState state)
        {
            if (state != GameState.Playing) SetTarget(null);
            ApplyCursorState();
        }

        private void ApplyCursorState()
        {
            bool locked = CanInteract && hasFocus;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
            if (locked) inputBlockedFrame = Time.frameCount;
        }

        private void OnApplicationFocus(bool focused)
        {
            hasFocus = focused;
            ApplyCursorState();
        }

        private void Update()
        {
            if (!CanInteract || !hasFocus || Time.frameCount == inputBlockedFrame)
            {
                SetTarget(null);
                if (Chef != null)
                {
                    Chef.SetMoving(false, 0f);
                    Chef.SetWorking(false);
                }
                return;
            }
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                SetTarget(null);
                if (Input.GetMouseButtonDown(0)) ApplyCursorState();
                return;
            }

            bool dashPressed = Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift)
                || Input.GetKeyDown(KeyCode.Space);
            AdvanceMovement(new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")), dashPressed, Time.deltaTime);
            FindTarget();
            if (Chef != null)
                Chef.SetWorking(currentTarget is CuttingCounter cutter && cutter.IsProcessing);
            if (Input.GetKeyDown(KeyCode.X))
            {
                if (currentTarget is CuttingCounter cutter) cutter.TakeBack(this);
                else RejectInteraction("走近切菜台按 X，可取回未切好的原料");
            }
            else if (Input.GetKeyDown(KeyCode.E)) TryInteract();
        }

        public void AdvanceMovement(Vector2 input, bool dashPressed, float dt)
        {
            if (!CanInteract || dt <= 0f) return;
            Vector3 dir = Vector3.ClampMagnitude(moveRight * input.x + moveForward * input.y, 1f);
            if (dir.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), 900f * dt);
            dashCooldownRemaining = Mathf.Max(0f, dashCooldownRemaining - dt);
            if (dashPressed && dashCooldownRemaining <= 0f)
            {
                dashDirection = dir.sqrMagnitude > 0.01f ? dir.normalized : transform.forward;
                dashRemaining = DashDuration;
                dashCooldownRemaining = DashCooldown;
                if (AudioManager.Instance != null) AudioManager.Instance.Play(SfxType.Dash);
            }

            float dashDt = Mathf.Min(dt, dashRemaining);
            float speed = Mathf.Max(0f, moveSpeed * GameManager.Instance.MoveSpeedMultiplier);
            Vector3 motion = dashDirection * (speed * DashSpeedMultiplier * dashDt)
                + dir * (speed * (dt - dashDt));
            dashRemaining = Mathf.Max(0f, dashRemaining - dt);
            Vector3 beforeMove = transform.position;
            if (controller.enabled) controller.Move(motion + Physics.gravity * dt);
            AdvanceFootsteps(transform.position - beforeMove, dashDt > 0f);
            if (Chef != null) Chef.SetMoving(dir.sqrMagnitude > 0.01f, Mathf.Clamp01(dir.magnitude));
        }

        private void AdvanceFootsteps(Vector3 displacement, bool dashed)
        {
            // Measure the CC result, not requested motion: blocked movement makes no footsteps.
            if (dashed) { footstepDistance = 0f; return; }
            if (!controller.enabled || !controller.isGrounded) return;
            displacement.y = 0f;
            float distance = displacement.magnitude;
            if (float.IsNaN(distance) || float.IsInfinity(distance) || distance < 0.001f) return;
            footstepDistance += distance;
            if (footstepDistance < FootstepStride) return;
            footstepDistance %= FootstepStride; // At most one step per update, even after a long frame.
            if (AudioManager.Instance != null) AudioManager.Instance.PlayAt(SfxType.Footstep, transform.position);
        }

        private void FindTarget()
        {
            IInteractable found = null;
            float best = float.MaxValue;
            Vector3 origin = transform.position;
            foreach (IInteractable candidate in interactables)
            {
                if (!(candidate is MonoBehaviour behaviour) || behaviour == null || !behaviour.isActiveAndEnabled) continue;
                Vector3 delta = candidate.GetTransform().position - origin;
                delta.y = 0f;
                float distance = delta.magnitude;
                if (distance > interactRange || distance < 0.001f) continue;
                if (distance >= best) continue;
                best = distance;
                found = candidate;
            }
            SetTarget(found);
        }

        private void SetTarget(IInteractable target)
        {
            if (currentTarget == target) return;
            if (currentHighlight != null) currentHighlight.SetHighlight(false);
            currentTarget = target;
            currentHighlight = target is MonoBehaviour mb && mb != null
                ? mb.GetComponent<InteractableHighlighter>() : null;
            if (currentHighlight != null) currentHighlight.SetHighlight(true);
        }

        private void TryInteract()
        {
            if (!CanInteract) return;
            if (!(currentTarget is MonoBehaviour target) || target == null)
            {
                RejectInteraction("走近柜台，再按 E");
                return;
            }
            bool acted = currentTarget.Interact(this);
            if (acted && Chef != null) Chef.Pulse();
        }

        public bool RejectInteraction(string message)
        {
            if (CanInteract)
            {
                GameManager.Instance.ShowToast(message, false);
                // 无效操作：错误音 + 柜台前的红叉 + 厨师摇头，连续错误不叠加成噪音。
                if (Time.unscaledTime - lastRejectSound >= 0.45f)
                {
                    lastRejectSound = Time.unscaledTime;
                    if (AudioManager.Instance != null) AudioManager.Instance.Play(SfxType.Error);
                    Vector3 at = currentTarget != null
                        ? currentTarget.GetTransform().position + Vector3.up * 0.9f
                        : transform.position + Vector3.up * 1.85f;
                    Color red = new Color(1f, 0.34f, 0.28f);
                    FeedbackFX.Popup(at, "✕", red, true);
                    FeedbackFX.Burst(at, red, 8, 1.7f, 0.06f, 5f, 0.35f);
                    FeedbackFX.Shake(0.05f, 0.14f);
                    if (Chef != null) Chef.PlayMistake();
                }
            }
            return false;
        }

        /// <summary>能不能再拿一份食材：手里已经有一份食材才算忙，单拿盘子不影响另一只手。</summary>
        public bool CanTake() => !HasItem;

        public void AttachPlate(PlateHandle handle)
        {
            if (handle == null || plate != null) return;
            plate = handle;
            plate.SetPosition(plateAnchor, Vector3.zero);
            plate.transform.localScale = Vector3.one * 0.5f;
            if (Chef != null) Chef.SetCarrying(true);
        }

        public PlateHandle DetachPlate()
        {
            PlateHandle handle = plate;
            plate = null;
            if (handle != null) handle.SetPosition(null, handle.transform.position);
            if (Chef != null) Chef.SetCarrying(HasItem);
            return handle;
        }

        public Ingredient TakeItem()
        {
            if (carried == null) return null;
            Ingredient data = carried.Ingredient != null ? carried.Ingredient.Clone() : null;
            ClearCarried();
            return data;
        }

        public bool GiveItem(Ingredient data)
        {
            if (carried != null || data == null) return false;
            SpawnCarried(data);
            return true;
        }

        public void SpawnCarried(Ingredient data)
        {
            if (carried != null || data == null) return;
            GameObject go = new GameObject("CarriedItem");
            go.layer = HeldItemLayer;
            go.transform.SetParent(carryAnchor, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localScale = Vector3.one * 0.45f;
            carried = go.AddComponent<IngredientItem>();
            carried.Setup(data.Clone());
            if (Chef != null) Chef.SetCarrying(true);
        }

        public void ClearCarried()
        {
            if (carried == null) return;
            carried.gameObject.SetActive(false);
            Destroy(carried.gameObject);
            carried = null;
            if (Chef != null) Chef.SetCarrying(HasPlate);
        }

        public void ResetForDay()
        {
            ClearCarried();
            PlateHandle previousPlate = DetachPlate();
            if (previousPlate != null) previousPlate.ReturnToCounter();
            dashRemaining = 0f;
            dashCooldownRemaining = 0f;
            dashDirection = Vector3.zero;
            footstepDistance = 0f;
            SetTarget(null);
            if (controller == null) controller = GetComponent<CharacterController>();
            bool wasEnabled = controller != null && controller.enabled;
            if (wasEnabled) controller.enabled = false;
            transform.SetPositionAndRotation(new Vector3(0f, 0.05f, -0.6f), Quaternion.Euler(0f, 180f, 0f));
            if (wasEnabled) controller.enabled = true;
            if (Chef != null)
            {
                Chef.SetCarrying(false);
                Chef.SetMoving(false, 0f);
            }
        }

        private void OnDisable()
        {
            if (manager != null) manager.StateChanged -= OnGameStateChanged;
            SetTarget(null);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
