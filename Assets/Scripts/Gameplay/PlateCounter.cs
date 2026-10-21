using UnityEngine;
using KitchenChaos.Audio;

namespace KitchenChaos.Gameplay
{
    /// <summary>
    /// 餐盘工作台：摆放干净盘子。
    /// 空手按 E 端起空盘；手持盘子按 E 把盘子（连同里面未出餐的食材一并清空）放回。
    /// </summary>
    public class PlateCounter : MonoBehaviour, IInteractable
    {
        [SerializeField] private int plateCount = 4;
        [SerializeField] private Transform stackAnchor;

        private readonly System.Collections.Generic.List<GameObject> plateVisuals =
            new System.Collections.Generic.List<GameObject>();

        private void Start()
        {
            BuildVisualStack();
        }

        private void BuildVisualStack()
        {
            foreach (GameObject v in plateVisuals)
            {
                if (v != null) Destroy(v);
            }
            plateVisuals.Clear();

            for (int i = 0; i < plateCount; i++)
            {
                GameObject disk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Collider col = disk.GetComponent<Collider>();
                if (col != null) Destroy(col);
                disk.name = "Plate";
                disk.transform.SetParent(stackAnchor != null ? stackAnchor : transform, false);
                disk.transform.localScale = new Vector3(0.9f, 0.06f, 0.9f);
                disk.transform.localPosition = new Vector3(0f, 0.95f + i * 0.08f, 0f);
                Renderer r = disk.GetComponent<Renderer>();
                r.sharedMaterial = Visual.VisualFactory.MakeMaterial(new Color(0.95f, 0.95f, 0.97f), 0.6f, 0f);
                plateVisuals.Add(disk);
            }
        }

        public bool Interact(PlayerController player)
        {
            // 手持盘子：放回
            if (player.HasPlate)
            {
                PlateHandle handle = player.DetachPlate();
                if (handle != null) handle.ReturnToCounter();
                plateCount = Mathf.Min(6, plateCount + 1);
                BuildVisualStack();
                if (AudioManager.Instance != null) AudioManager.Instance.Play(SfxType.Drop);
                return true;
            }

            // 空手端盘
            if (player.CanTake() && plateCount > 0)
            {
                plateCount--;
                BuildVisualStack();

                GameObject go = new GameObject("HeldPlate");
                go.transform.SetParent(player.transform, false);
                PlateHandle plate = go.AddComponent<PlateHandle>();
                plate.Init(new Vector3(0f, 0.85f, 0.6f));
                player.AttachPlate(plate);

                if (AudioManager.Instance != null) AudioManager.Instance.Play(SfxType.Pickup);
                return true;
            }

            return false;
        }

        public string GetPrompt()
        {
            return "餐盘台：空手按 E 端盘，手持盘按 E 归还";
        }

        public Transform GetTransform() => transform;
    }
}
