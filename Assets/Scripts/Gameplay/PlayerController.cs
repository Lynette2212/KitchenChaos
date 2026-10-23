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
        public float interactRange = 2.2f;
        public LayerMask interactMask = Physics.DefaultRaycastLayers;

        private const float DashCooldown = 3f;
        private const float DashDuration = 0.18f;
        private const float DashSpeedMultiplier = 2.6f;
        public const int HeldItemLayer = 2;
        private static readonly Vector3 CarryOffset = new Vector3(0.3f, -0.33f, 0.6f);
        private static readonly Vector3 PlateOffset = new Vector3(0.28f, -0.4f, 0.65f);
        private CharacterController controller;
        private IngredientItem carried;
        private PlateHandle plate;
        private IInteractable currentTarget;
        private InteractableHighlighter currentHighlight;
        private CameraFollow view;
        private Transform carryAnchor;
        private GameManager manager;
        private Vector3 dashDirection;
        private float dashRemaining;
        private float dashCooldownRemaining;
        private float footstepDistance;
        private const float FootstepStride = 1.45f;
        private bool hasFocus = true;
        private int inputBlockedFrame = -1;

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

        private void Awake() => controller = GetComponent<CharacterController>();

        private void OnEnable()
        {
            manager = GameManager.Instance;
            if (manager != null) manager.StateChanged += OnGameStateChanged;
            ApplyCursorState();
        }

        public void InitializeView(CameraFollow cameraView)
        {
            view = cameraView;
            ViewCamera = view.GetComponent<Camera>();
            view.target = transform;
            carryAnchor = new GameObject("HeldItems").transform;
            carryAnchor.SetParent(view.transform, false);
            carryAnchor.gameObject.layer = HeldItemLayer;
            view.SetViewAngles(0f, 12f);
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
                return;
            }
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                SetTarget(null);
                if (Input.GetMouseButtonDown(0)) ApplyCursorState();
                return;
            }

            AdvanceLook(new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")));
            bool dashPressed = Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift)
                || Input.GetKeyDown(KeyCode.Space);
            AdvanceMovement(new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")), dashPressed, Time.deltaTime);
            FindTarget();
            if (Input.GetKeyDown(KeyCode.X))
            {
                if (currentTarget is CuttingCounter cutter) cutter.TakeBack(this);
                else RejectInteraction("对准切菜台按 X，可取回未切好的原料");
            }
            else if (Input.GetKeyDown(KeyCode.E)) TryInteract();
        }

        public void AdvanceLook(Vector2 delta)
        {
            if (!CanInteract || view == null) return;
            view.AdvanceLook(delta);
        }

        public void AdvanceMovement(Vector2 input, bool dashPressed, float dt)
        {
            if (!CanInteract || dt <= 0f) return;
            Vector3 dir = Vector3.ClampMagnitude(transform.right * input.x + transform.forward * input.y, 1f);
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
            if (view != null) view.RefreshPose();
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
            if (ViewCamera != null && Physics.Raycast(ViewCamera.transform.position, ViewCamera.transform.forward,
                out RaycastHit hit, interactRange, interactMask & ~(1 << HeldItemLayer), QueryTriggerInteraction.Ignore))
            {
                IInteractable candidate = hit.collider.GetComponentInParent<IInteractable>();
                if (candidate is MonoBehaviour behaviour && behaviour.isActiveAndEnabled) found = candidate;
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
                RejectInteraction("靠近柜台，用屏幕中央准星对准台面，再按 E");
                return;
            }
            currentTarget.Interact(this);
        }

        public bool RejectInteraction(string message)
        {
            if (CanInteract) GameManager.Instance.ShowToast(message, false);
            return false;
        }

        public bool CanTake() => !HandsBusy;

        public void AttachPlate(PlateHandle handle)
        {
            if (handle == null || HandsBusy) return;
            plate = handle;
            plate.SetPosition(carryAnchor, PlateOffset);
            plate.transform.localScale = Vector3.one * 0.55f;
        }

        public PlateHandle DetachPlate()
        {
            PlateHandle handle = plate;
            plate = null;
            if (handle != null) handle.SetPosition(null, handle.transform.position);
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
            if (HandsBusy || data == null) return false;
            SpawnCarried(data);
            return true;
        }

        public void SpawnCarried(Ingredient data)
        {
            if (HandsBusy || data == null) return;
            GameObject go = new GameObject("CarriedItem");
            go.layer = HeldItemLayer;
            go.transform.SetParent(carryAnchor, false);
            go.transform.localPosition = CarryOffset;
            go.transform.localScale = Vector3.one * 0.48f;
            carried = go.AddComponent<IngredientItem>();
            carried.Setup(data.Clone());
        }

        public void ClearCarried()
        {
            if (carried == null) return;
            carried.gameObject.SetActive(false);
            Destroy(carried.gameObject);
            carried = null;
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
            transform.SetPositionAndRotation(new Vector3(0f, 0.05f, 0f), Quaternion.identity);
            if (wasEnabled) controller.enabled = true;
            if (view != null) view.SetViewAngles(0f, 12f);
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
