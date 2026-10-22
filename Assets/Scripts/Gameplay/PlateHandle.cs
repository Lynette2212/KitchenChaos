using System.Collections.Generic;
using UnityEngine;
using KitchenChaos.Data;
using KitchenChaos.Visual;

namespace KitchenChaos.Gameplay
{
    public class PlateHandle : MonoBehaviour
    {
        public const int Capacity = 5;
        private readonly List<Ingredient> contents = new List<Ingredient>();
        private readonly List<GameObject> stacked = new List<GameObject>();
        private GameObject plateBody;
        private Material bodyMaterial;

        public IReadOnlyList<Ingredient> Contents => contents;
        public int Count => contents.Count;

        public void Init(Vector3 holdOffset)
        {
            SetPosition(transform.parent, holdOffset);
            if (plateBody != null) return;
            plateBody = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Collider col = plateBody.GetComponent<Collider>();
            if (col != null)
            {
                col.enabled = false;
                Destroy(col);
            }
            plateBody.name = "PlateBody";
            plateBody.transform.SetParent(transform, false);
            plateBody.transform.localScale = new Vector3(0.85f, 0.06f, 0.85f);
            bodyMaterial = Visual.VisualFactory.MakeMaterial(new Color(0.96f, 0.96f, 0.98f), 0.6f);
            plateBody.GetComponent<Renderer>().sharedMaterial = bodyMaterial;
        }

        public void SetPosition(Transform parent, Vector3 offset)
        {
            transform.SetParent(parent, false);
            transform.localPosition = offset;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
        }

        public string GetAddFailureReason(Ingredient data)
        {
            if (data == null) return "没有可装盘的食材";
            if (data.state == IngredientState.Burned) return "烤糊的食材不能装盘，请丢进垃圾桶";
            IngredientInfo info = IngredientCatalog.Get(data.type);
            if (info == null) return "无法识别这份食材";
            if (info.needsCook && data.state != IngredientState.Cooked) return "食材还没熟，需要先煎好才能装盘";
            if (!info.needsCook && info.needsCut && data.state != IngredientState.Cut)
                return "食材还没切好，请先在切菜台加工";
            if (!info.needsCook && !info.needsCut && data.state != IngredientState.Raw)
                return "这份食材的状态不适合装盘";
            if (contents.Count >= Capacity) return "一个盘子最多装 5 样食材，请换一个盘子";
            foreach (Ingredient existing in contents)
                if (existing.type == data.type) return "盘里已经有" + IngredientCatalog.GetName(data.type) + "了";
            return null;
        }

        public bool CanAdd(Ingredient data) => GetAddFailureReason(data) == null;

        public bool Add(Ingredient data)
        {
            if (!CanAdd(data)) return false;
            contents.Add(data.Clone());
            RefreshStack();
            if (stacked.Count > 0) FeedbackFX.Punch(stacked[stacked.Count - 1].transform, 0.35f, 0.3f);
            return true;
        }

        public List<Ingredient> Dump()
        {
            List<Ingredient> all = new List<Ingredient>(contents);
            contents.Clear();
            ReleaseStack();
            return all;
        }

        private void RefreshStack()
        {
            for (int i = stacked.Count; i < contents.Count; i++)
            {
                GameObject visual = Visual.VisualFactory.CreateIngredientVisual(contents[i].type, contents[i].state);
                visual.transform.SetParent(transform, false);
                stacked.Add(visual);
            }
            for (int i = 0; i < stacked.Count; i++)
            {
                stacked[i].transform.localPosition = new Vector3(0f, 0.06f + i * 0.08f, 0f);
                stacked[i].transform.localRotation = Quaternion.Euler(0f, i * 40f, 0f);
            }
        }

        private void ReleaseStack()
        {
            foreach (GameObject visual in stacked)
            {
                if (visual == null) continue;
                visual.SetActive(false);
                Destroy(visual);
            }
            stacked.Clear();
        }

        public void ReturnToCounter()
        {
            gameObject.SetActive(false);
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            ReleaseStack();
        }
    }
}
