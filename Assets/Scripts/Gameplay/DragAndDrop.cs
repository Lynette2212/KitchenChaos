using UnityEngine;
using KitchenChaos.Core;
using KitchenChaos.Data;
using KitchenChaos.UI;

namespace KitchenChaos.Gameplay
{
    public class DragAndDrop : MonoBehaviour
    {
        public Camera Camera;
        public OrderPanelManager OrderPanel;
        public GameManager GameManager;

        public bool IsDragging { get; private set; }
        public IngredientType DragType { get; private set; }
        public bool DragIsCooked { get; private set; }

        private Font font;
        private GUIStyle ghostStyle;

        public void Initialize(Camera targetCamera, OrderPanelManager orderPanel, GameManager gameManager)
        {
            Camera = targetCamera;
            OrderPanel = orderPanel;
            GameManager = gameManager;
        }

        private void Awake()
        {
            font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 16);
        }

        private void Update()
        {
            if (GameManager == null || !GameManager.IsPlaying || Camera == null) return;

            if (Input.GetMouseButtonDown(0)) BeginDrag();
            if (Input.GetMouseButtonUp(0) && IsDragging) EndDrag();
        }

        private void BeginDrag()
        {
            Ray ray = Camera.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, 100f)) return;

            IngredientItem ingredient = hit.collider.GetComponentInParent<IngredientItem>();
            if (ingredient != null && ingredient.IsSource)
            {
                IsDragging = true;
                DragType = ingredient.Type;
                DragIsCooked = false;
                return;
            }

            CookingStation station = hit.collider.GetComponentInParent<CookingStation>();
            if (station != null && station.IsReady && station.TryTakeCooked(out IngredientType cookedType))
            {
                IsDragging = true;
                DragType = cookedType;
                DragIsCooked = true;
            }
        }

        private void EndDrag()
        {
            Vector2 mousePosition = Input.mousePosition;

            if (OrderPanel != null && OrderPanel.TryDeliverAt(mousePosition, DragType, DragIsCooked))
            {
                IsDragging = false;
                return;
            }

            Ray ray = Camera.ScreenPointToRay(mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
            {
                CookingStation station = hit.collider.GetComponentInParent<CookingStation>();
                if (station != null)
                {
                    if (DragIsCooked)
                    {
                        ShowMessage("熟食要拖到右侧订单上。");
                    }
                    else if (station.TryAddIngredient(DragType))
                    {
                        ShowMessage("放入" + Recipe.GetIngredientName(DragType) + "，开始烹饪。");
                    }
                    else
                    {
                        ShowMessage("这个灶台正忙。");
                    }
                }
            }

            IsDragging = false;
        }

        private void ShowMessage(string message)
        {
            if (GameManager != null && GameManager.UI != null) GameManager.UI.ShowMessage(message);
        }

        private void OnDisable()
        {
            IsDragging = false;
        }

        private void OnGUI()
        {
            if (!IsDragging) return;

            if (ghostStyle == null)
            {
                ghostStyle = new GUIStyle(GUI.skin.label);
                ghostStyle.font = font;
                ghostStyle.fontSize = 16;
                ghostStyle.alignment = TextAnchor.MiddleCenter;
                ghostStyle.normal.textColor = Color.white;
            }

            GUI.depth = -100;
            Vector2 mouse = Event.current.mousePosition;
            Rect rect = new Rect(mouse.x + 14f, mouse.y + 14f, 120f, 34f);
            GUI.color = Recipe.GetIngredientColor(DragType);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            string label = DragIsCooked ? Recipe.GetCookedName(DragType) : Recipe.GetIngredientName(DragType);
            GUI.Label(rect, label, ghostStyle);
        }
    }
}
