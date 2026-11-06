using System.Collections.Generic;
using UnityEngine;
using KitchenChaos.Data;

namespace KitchenChaos.Visual
{
    /// <summary>
    /// 场景里的汉堡叠层样板。子物体是真实食材预制体，从下到上按本地 Y 排列。
    /// 在 Scene 视图里直接拖这些子物体，盘子上的叠层会照抄位置、旋转和缩放。
    /// 认这些名字：BunBottom、Lettuce、Tomato、Onion、Patty、Cheese、Mushroom、BunTop。
    /// </summary>
    public class BurgerStackLayout : MonoBehaviour
    {
        public struct Layer
        {
            public IngredientType type;
            public bool topBun;
            public Vector3 localPosition;
            public Quaternion localRotation;
            public Vector3 localScale;
        }

        public static BurgerStackLayout FindTemplate()
        {
            return Object.FindAnyObjectByType<BurgerStackLayout>(FindObjectsInactive.Include);
        }

        private void Awake()
        {
            if (!Application.isPlaying) return;
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
                renderer.enabled = false;
        }

        public Layer[] Capture()
        {
            var layers = new List<Layer>();
            foreach (Transform child in transform)
            {
                if (!TryMap(child.name, out IngredientType type, out bool topBun)) continue;
                layers.Add(new Layer
                {
                    type = type,
                    topBun = topBun,
                    localPosition = child.localPosition,
                    localRotation = child.localRotation,
                    localScale = child.localScale
                });
            }
            layers.Sort((a, b) => a.localPosition.y.CompareTo(b.localPosition.y));
            return layers.ToArray();
        }

        public static bool TryMap(string childName, out IngredientType type, out bool topBun)
        {
            topBun = false;
            switch (childName)
            {
                case "BunBottom": type = IngredientType.Bun; return true;
                case "BunTop": type = IngredientType.Bun; topBun = true; return true;
                case "Lettuce": type = IngredientType.Lettuce; return true;
                case "Tomato": type = IngredientType.Tomato; return true;
                case "Onion": type = IngredientType.Onion; return true;
                case "Patty": type = IngredientType.MeatPatty; return true;
                case "Cheese": type = IngredientType.Cheese; return true;
                case "Mushroom": type = IngredientType.Mushroom; return true;
                default:
                    type = default;
                    return false;
            }
        }
    }
}
