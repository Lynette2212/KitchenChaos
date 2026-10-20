using UnityEngine;
using KitchenChaos.Data;

namespace KitchenChaos.Gameplay
{
    public class CookingStation : MonoBehaviour
    {
        public string StationName = "灶台";
        public float CookTime = 2.5f;

        public bool IsBusy { get; private set; }
        public bool IsReady { get; private set; }
        public IngredientType CurrentType { get; private set; }

        private float timer;
        private Renderer targetRenderer;
        private readonly Color idleColor = new Color(0.35f, 0.36f, 0.4f);
        private readonly Color busyColor = new Color(0.85f, 0.45f, 0.15f);
        private readonly Color readyColor = new Color(0.2f, 0.8f, 0.35f);

        public float Progress01
        {
            get
            {
                if (IsReady) return 1f;
                if (!IsBusy || CookTime <= 0f) return 0f;
                return Mathf.Clamp01(1f - timer / CookTime);
            }
        }

        private void Awake()
        {
            targetRenderer = GetComponent<Renderer>();
        }

        public bool TryAddIngredient(IngredientType type)
        {
            if (IsBusy || IsReady) return false;

            CurrentType = type;
            IsBusy = true;
            timer = Mathf.Max(0.1f, CookTime);
            if (targetRenderer != null) targetRenderer.material.color = busyColor;
            return true;
        }

        public bool TryTakeCooked(out IngredientType type)
        {
            type = CurrentType;
            if (!IsReady) return false;

            IsReady = false;
            IsBusy = false;
            if (targetRenderer != null) targetRenderer.material.color = idleColor;
            return true;
        }

        private void Update()
        {
            if (!IsBusy) return;

            timer -= Time.deltaTime;
            if (timer <= 0f)
            {
                timer = 0f;
                IsBusy = false;
                IsReady = true;
                if (targetRenderer != null) targetRenderer.material.color = readyColor;
            }
        }

        public string GetStatusText()
        {
            if (IsReady) return "完成：" + Recipe.GetCookedName(CurrentType) + "（拖到订单）";
            if (IsBusy) return "烹饪中 " + Mathf.RoundToInt(Progress01 * 100f) + "%";
            return "空闲（拖入生食材）";
        }
    }
}
