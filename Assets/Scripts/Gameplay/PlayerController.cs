using UnityEngine;
using KitchenChaos.Data;
using KitchenChaos.Audio;

namespace KitchenChaos.Gameplay
{
    /// <summary>
    /// 玩家控制器：WASD/方向键移动，E 交互。
    /// 玩家手上可携带一块食材（IngredientItem），与工作台之间通过交互交换。
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("移动参数")]
        public float moveSpeed = 5f;
        public float turnSpeed = 12f;

        [Header("交互")]
        public float interactRange = 1.4f;
        public LayerMask interactMask = ~0;

        private CharacterController controller;
        private IngredientItem carried;
        private PlateHandle plate;
        private IInteractable currentTarget;
        private InteractableHighlighter currentHighlight;
        private Vector3 carryOffset = new Vector3(0f, 0.9f, 0.55f);

        public bool HasItem => carried != null;
        public IngredientItem CarriedItem => carried;
        public PlateHandle Plate => plate;
        public bool HasPlate => plate != null;
        public bool HandsBusy => carried != null || plate != null;
        public IInteractable CurrentTarget => currentTarget;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
        }

        private void Update()
        {
            Move();
            FindTarget();
            if (Input.GetKeyDown(KeyCode.E)) TryInteract();
        }

        private void Move()
        {
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            Vector3 dir = new Vector3(h, 0f, v);
            if (dir.sqrMagnitude > 1f) dir.Normalize();

            controller.Move(dir * moveSpeed * Time.deltaTime);
            // 重力，保证贴地
            controller.Move(Physics.gravity * Time.deltaTime);

            if (dir.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(dir, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, turnSpeed * Time.deltaTime);
            }
        }

        private void FindTarget()
        {
            IInteractable found = null;
            float best = interactRange;

            Collider[] hits = Physics.OverlapSphere(transform.position, interactRange, interactMask);
            foreach (Collider hit in hits)
            {
                IInteractable station = hit.GetComponentInParent<IInteractable>();
                if (station == null) continue;

                float d = Vector3.Distance(transform.position, station.GetTransform().position);
                if (d <= best)
                {
                    best = d;
                    found = station;
                }
            }

            if (found != currentTarget)
            {
                if (currentHighlight != null) currentHighlight.SetHighlight(false);
                currentTarget = found;
                currentHighlight = found is MonoBehaviour mb ? mb.GetComponent<InteractableHighlighter>() : null;
                if (currentHighlight != null) currentHighlight.SetHighlight(true);
            }
        }

        private void TryInteract()
        {
            if (currentTarget == null) return;
            bool changed = currentTarget.Interact(this);
            if (changed && AudioManager.Instance != null) AudioManager.Instance.Play(SfxType.ButtonClick);
        }

        // ------- 携带物品的高层接口，供工作台调用 -------

        public bool CanTake()
        {
            return carried == null && plate == null;
        }

        /// <summary>挂上一个盘子容器（由餐盘台调用）。</summary>
        public void AttachPlate(PlateHandle handle)
        {
            plate = handle;
        }

        public PlateHandle DetachPlate()
        {
            PlateHandle h = plate;
            plate = null;
            return h;
        }

        public Ingredient TakeItem()
        {
            if (carried == null) return null;
            Ingredient data = carried.Ingredient != null ? carried.Ingredient.Clone() : null;
            Destroy(carried.gameObject);
            carried = null;
            return data;
        }

        public bool GiveItem(Ingredient data)
        {
            if (carried != null || plate != null || data == null) return false;
            SpawnCarried(data);
            return true;
        }

        /// <summary>不销毁来源地把同一食材“拿到手上”（用于食材柜复制新食材时由来源自行处理）。</summary>
        public void SpawnCarried(Ingredient data)
        {
            GameObject go = new GameObject("CarriedItem");
            IngredientItem item = go.AddComponent<IngredientItem>();
            // 先 AddComponent 再 Setup，Awake 会创建默认 Ingredient，但 Setup 会覆盖
            go.transform.SetParent(transform, false);
            item.Setup(data, transform, carryOffset);
            carried = item;
        }

        public void ClearCarried()
        {
            if (carried != null)
            {
                Destroy(carried.gameObject);
                carried = null;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, interactRange);
        }
    }
}
