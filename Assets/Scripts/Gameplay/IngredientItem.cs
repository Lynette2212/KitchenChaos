using UnityEngine;
using KitchenChaos.Data;
using KitchenChaos.Visual;

namespace KitchenChaos.Gameplay
{
    /// <summary>
    /// 玩家与工作台通用的“可携带食材”容器。
    /// 一个对象同一时刻最多携带一块食材；视觉模型跟随持有者位置。
    /// </summary>
    public class IngredientItem : MonoBehaviour
    {
        [SerializeField] private IngredientType type = IngredientType.Tomato;
        [SerializeField] private IngredientState state = IngredientState.Raw;

        public Ingredient Ingredient { get; private set; }

        private GameObject visual;
        private Transform followTarget;
        private Vector3 followOffset;

        public IngredientType Type => Ingredient != null ? Ingredient.type : type;
        public IngredientState State => Ingredient != null ? Ingredient.state : state;

        private void Awake()
        {
            if (Ingredient == null)
            {
                Ingredient = new Ingredient(type, state);
            }
        }

        public void Setup(Ingredient ingredient, Transform follow = null, Vector3 offset = default)
        {
            Ingredient = ingredient;
            type = ingredient.type;
            state = ingredient.state;
            followTarget = follow;
            followOffset = offset;
            RebuildVisual();
            FeedbackFX.Punch(transform, 0.25f, 0.3f);
        }

        public void SetFollow(Transform follow, Vector3 offset = default)
        {
            followTarget = follow;
            followOffset = offset;
            if (visual != null && follow != null)
            {
                visual.transform.SetParent(null, true);
            }
        }

        private void Update()
        {
            if (visual != null && followTarget != null)
            {
                visual.transform.position = followTarget.position + followOffset;
                visual.transform.rotation = followTarget.rotation;
            }
        }

        public void RebuildVisual()
        {
            if (visual != null) Destroy(visual);
            visual = Visual.VisualFactory.CreateIngredientVisual(Type, State);
            visual.transform.SetParent(transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            foreach (Transform part in visual.GetComponentsInChildren<Transform>(true))
                part.gameObject.layer = gameObject.layer;
        }

        public void SetState(IngredientState newState)
        {
            state = newState;
            if (Ingredient != null) Ingredient.state = newState;
            RebuildVisual();
            FeedbackFX.Punch(transform, 0.3f, 0.35f);
        }
    }
}
