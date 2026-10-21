using System.Collections.Generic;
using UnityEngine;
using KitchenChaos.Data;

namespace KitchenChaos.Gameplay
{
    /// <summary>
    /// 玩家手上端着的盘子。它是一个“可接收食材”的容器：
    /// 玩家手持盘子去对切菜台/灶台按 E 时，会优先把台面上处理好的食材装入盘中（而不是把盘子放到台面）。
    /// </summary>
    public class PlateHandle : MonoBehaviour
    {
        private readonly List<Ingredient> contents = new List<Ingredient>();
        private readonly List<GameObject> stacked = new List<GameObject>();
        private GameObject plateBody;
        private Vector3 offset;

        public IReadOnlyList<Ingredient> Contents => contents;
        public int Count => contents.Count;

        public void Init(Vector3 holdOffset)
        {
            offset = holdOffset;
            BuildBody();
        }

        private void BuildBody()
        {
            plateBody = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Collider col = plateBody.GetComponent<Collider>();
            if (col != null) Destroy(col);
            plateBody.name = "PlateBody";
            plateBody.transform.SetParent(transform, false);
            plateBody.transform.localScale = new Vector3(0.85f, 0.06f, 0.85f);
            plateBody.transform.localPosition = offset;
            Renderer r = plateBody.GetComponent<Renderer>();
            r.sharedMaterial = Visual.VisualFactory.MakeMaterial(new Color(0.96f, 0.96f, 0.98f), 0.6f);
        }

        public bool CanAdd(Ingredient data)
        {
            return data != null && data.state != IngredientState.Burned;
        }

        public bool Add(Ingredient data)
        {
            if (!CanAdd(data)) return false;
            contents.Add(data.Clone());
            RefreshStack();
            return true;
        }

        public List<Ingredient> Dump()
        {
            List<Ingredient> all = new List<Ingredient>(contents);
            contents.Clear();
            RefreshStack();
            return all;
        }

        private void RefreshStack()
        {
            foreach (GameObject v in stacked) Destroy(v);
            stacked.Clear();
            for (int i = 0; i < contents.Count; i++)
            {
                GameObject v = Visual.VisualFactory.CreateIngredientVisual(contents[i].type, contents[i].state);
                v.transform.SetParent(transform, false);
                v.transform.localPosition = offset + new Vector3(0f, 0.06f + i * 0.08f, 0f);
                v.transform.localRotation = Quaternion.Euler(0f, i * 40f, 0f);
                stacked.Add(v);
            }
        }

        /// <summary>把盘子放回餐盘台时调用，销毁手持对象。</summary>
        public void ReturnToCounter()
        {
            Destroy(gameObject);
        }
    }
}
