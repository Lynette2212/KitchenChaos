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
        [Header("移动参数")]
        public float moveSpeed = 5f;
        public float turnSpeed = 12f;

        [Header("交互")]
        public float interactRange = 1.4f;
        public LayerMask interactMask = ~0;

        private const float DashCooldown = 3f;
        private const float DashDuration = 0.18f;
        private const float DashSpeedMultiplier = 2.6f;
        private static readonly Vector3 CarryOffset = new Vector3(0f, 0.9f, 0.55f);
        private static readonly Vector3 PlateOffset = new Vector3(0f, 0.85f, 0.6f);
        private readonly Collider[] targetHits = new Collider[64];
        private CharacterController controller;
        private IngredientItem carried;
        private PlateHandle plate;
        private IInteractable currentTarget;
        private InteractableHighlighter currentHighlight;
        private Vector3 facingDirection;
        private Vector3 dashDirection;
        private float dashRemaining;
        private float dashCooldownRemaining;
        private Transform visualRig;
        private Vector3 visualBasePosition;
        private float walkPhase;
        private float lean;
        private float dustTimer;

        public bool HasItem => carried != null;
        public IngredientItem CarriedItem => carried;
        public PlateHandle Plate => plate;
        public bool HasPlate => plate != null;
        public bool HandsBusy => HasItem || HasPlate;
        public IInteractable CurrentTarget => currentTarget;
        public bool CanInteract => isActiveAndEnabled && GameManager.Instance != null && GameManager.Instance.IsPlaying;
        public float DashReady01 => Mathf.Clamp01(1f - dashCooldownRemaining / DashCooldown);
        public bool IsDashing => CanInteract && dashRemaining > 0f;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            facingDirection = transform.forward;
            visualRig = transform.Find("Visual");
            if (visualRig != null) visualBasePosition = visualRig.localPosition;
        }

        private void Update()
        {
            if (!CanInteract)
            {
                SetTarget(null);
                return;
            }

            Move();
            FindTarget();
            if (Input.GetKeyDown(KeyCode.X))
            {
                if (currentTarget is CuttingCounter cutter) cutter.TakeBack(this);
                else RejectInteraction("对准切菜台按 X，可取回未切好的原料");
            }
            else if (Input.GetKeyDown(KeyCode.E)) TryInteract();
        }

        private void LateUpdate() => AnimateVisual();

        private void AnimateVisual()
        {
            if (visualRig == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            float speed = controller != null ? new Vector2(controller.velocity.x, controller.velocity.z).magnitude : 0f;
            float speedFactor = Mathf.Clamp01(speed / Mathf.Max(0.01f, moveSpeed));
            walkPhase += (2f + speed * 2.1f) * dt;
            bool dashing = dashRemaining > 0f && CanInteract;
            lean = Mathf.Lerp(lean, dashing ? 17f : speedFactor * 7f, 12f * dt);
            float bob = Mathf.Abs(Mathf.Sin(walkPhase)) * 0.07f * speedFactor;
            float sway = Mathf.Sin(walkPhase * 0.5f) * 3.2f * speedFactor;
            float squash = 1f + Mathf.Sin(walkPhase * 2f) * 0.04f * speedFactor;
            visualRig.localPosition = visualBasePosition + Vector3.up * bob;
            visualRig.localRotation = Quaternion.Euler(lean, 0f, sway);
            visualRig.localScale = new Vector3(1f + (1f - squash) * 0.5f, squash, 1f + (1f - squash) * 0.5f);

            dustTimer -= dt;
            if (speedFactor > 0.3f && !dashing && dustTimer <= 0f)
            {
                dustTimer = 0.16f;
                FeedbackFX.Puff(transform.position + Vector3.up * 0.04f - transform.forward * 0.18f,
                    new Color(0.78f, 0.82f, 0.76f), 1, 0.1f, 0.35f, 0.4f);
            }
        }

        private void Move()
        {
            bool dashPressed = Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift)
                || Input.GetKeyDown(KeyCode.Space);
            AdvanceMovement(new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")), dashPressed, Time.deltaTime);
        }

        public void AdvanceMovement(Vector2 input, bool dashPressed, float dt)
        {
            if (!CanInteract || dt <= 0f) return;
            Vector3 dir = Vector3.ClampMagnitude(new Vector3(input.x, 0f, input.y), 1f);
            dashCooldownRemaining = Mathf.Max(0f, dashCooldownRemaining - dt);
            if (dashPressed && dashCooldownRemaining <= 0f)
            {
                dashDirection = dir.sqrMagnitude > 0.01f ? dir.normalized : facingDirection;
                dashRemaining = DashDuration;
                dashCooldownRemaining = DashCooldown;
                FeedbackFX.Puff(transform.position + Vector3.up * 0.06f, new Color(0.85f, 0.88f, 0.82f), 4, 0.16f, 0.5f, 0.5f);
                FeedbackFX.Burst(transform.position + Vector3.up * 0.45f, new Color(1f, 0.86f, 0.65f), 7, 2.4f, 0.08f, 3f, 0.35f);
                FeedbackFX.Shake(0.05f, 0.12f);
                if (AudioManager.Instance != null) AudioManager.Instance.Play(SfxType.Dash);
            }

            float dashDt = Mathf.Min(dt, dashRemaining);
            float speed = Mathf.Max(0f, moveSpeed * GameManager.Instance.MoveSpeedMultiplier);
            Vector3 motion = dashDirection * (speed * DashSpeedMultiplier * dashDt)
                + dir * (speed * (dt - dashDt));
            dashRemaining = Mathf.Max(0f, dashRemaining - dt);
            if (controller.enabled) controller.Move(motion + Physics.gravity * dt);

            Vector3 look = dashDt > 0f ? dashDirection : dir;
            if (look.sqrMagnitude > 0.01f)
            {
                facingDirection = look.normalized;
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(facingDirection, Vector3.up), turnSpeed * dt);
            }
        }

        private void FindTarget()
        {
            IInteractable found = null;
            float best = float.PositiveInfinity;
            int count = Physics.OverlapSphereNonAlloc(transform.position + Vector3.up * 0.5f,
                interactRange, targetHits, interactMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                IInteractable station = targetHits[i].GetComponentInParent<IInteractable>();
                if (!(station is MonoBehaviour behaviour) || !behaviour.isActiveAndEnabled) continue;
                Vector3 to = station.GetTransform().position - transform.position;
                to.y = 0f;
                float distance = to.magnitude;
                if (distance > interactRange) continue;
                float facing = distance > 0.001f ? Vector3.Dot(facingDirection, to / distance) : 1f;
                if (facing < 0.35f) continue;
                float score = distance + (1f - facing) * 0.85f;
                if (station == currentTarget) score -= 0.06f;
                if (score >= best) continue;
                best = score;
                found = station;
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
                RejectInteraction("靠近并面向柜台，再按 E 交互");
                return;
            }
            bool changed = currentTarget.Interact(this);
            if (changed && GameManager.Instance != null && GameManager.Instance.IsPlaying
                && AudioManager.Instance != null) AudioManager.Instance.Play(SfxType.ButtonClick);
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
            plate.SetPosition(transform, PlateOffset);
        }

        public PlateHandle DetachPlate()
        {
            PlateHandle handle = plate;
            plate = null;
            if (handle != null) handle.transform.SetParent(null, true);
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
            go.transform.SetParent(transform, false);
            go.transform.localPosition = CarryOffset;
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
            facingDirection = Vector3.forward;
            SetTarget(null);
            if (controller == null) controller = GetComponent<CharacterController>();
            bool wasEnabled = controller != null && controller.enabled;
            if (wasEnabled) controller.enabled = false;
            transform.SetPositionAndRotation(new Vector3(0f, 0.05f, 0f), Quaternion.identity);
            if (wasEnabled) controller.enabled = true;
        }

        private void OnDisable() => SetTarget(null);

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, interactRange);
        }
    }
}
