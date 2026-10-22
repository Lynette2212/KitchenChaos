using System.Collections.Generic;
using UnityEngine;
using KitchenChaos.Audio;
using KitchenChaos.Visual;

namespace KitchenChaos.Gameplay
{
    public class PlateCounter : MonoBehaviour, IInteractable
    {
        [SerializeField] private int plateCount = 4;
        [SerializeField] private Transform stackAnchor;
        private readonly List<GameObject> plateVisuals = new List<GameObject>();
        private Material plateMaterial;
        private int initialPlateCount;
        private bool initialized;

        public int PlateCount => plateCount;

        private void Awake() => InitializeStock();
        private void Start() => BuildVisualStack();

        private void InitializeStock()
        {
            if (initialized) return;
            initialPlateCount = Mathf.Max(0, plateCount);
            plateCount = initialPlateCount;
            initialized = true;
        }

        private void BuildVisualStack()
        {
            if (stackAnchor == null) stackAnchor = transform.Find("ItemAnchor");
            if (plateMaterial == null)
                plateMaterial = Visual.VisualFactory.MakeMaterial(new Color(0.95f, 0.95f, 0.97f), 0.6f);
            while (plateVisuals.Count > plateCount)
            {
                int last = plateVisuals.Count - 1;
                GameObject disk = plateVisuals[last];
                plateVisuals.RemoveAt(last);
                if (disk == null) continue;
                disk.SetActive(false);
                Destroy(disk);
            }
            for (int i = plateVisuals.Count; i < plateCount; i++)
            {
                GameObject disk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Collider col = disk.GetComponent<Collider>();
                if (col != null)
                {
                    col.enabled = false;
                    Destroy(col);
                }
                disk.name = "Plate";
                disk.transform.SetParent(stackAnchor != null ? stackAnchor : transform, false);
                disk.transform.localScale = new Vector3(0.9f, 0.06f, 0.9f);
                disk.transform.localPosition = new Vector3(0f, (stackAnchor != null ? 0.04f : 0.59f) + i * 0.08f, 0f);
                disk.GetComponent<Renderer>().sharedMaterial = plateMaterial;
                plateVisuals.Add(disk);
            }
        }

        public bool Interact(PlayerController player)
        {
            if (player == null || !player.CanInteract) return false;
            InitializeStock();
            if (player.HasPlate)
            {
                if (player.Plate.Count > 0)
                    return player.RejectInteraction("盘里还有食材，请先放到暂存台；餐盘台只能归还空盘");
                PlateHandle handle = player.DetachPlate();
                handle.ReturnToCounter();
                plateCount++;
                BuildVisualStack();
                FeedbackFX.Puff(StackTop() + Vector3.up * 0.1f, new Color(0.9f, 0.92f, 0.95f), 2, 0.1f, 0.4f, 0.35f);
                Play(SfxType.Drop);
                return true;
            }
            if (!player.CanTake()) return player.RejectInteraction("请先把食材放到暂存台，再来拿盘子");
            if (plateCount <= 0) return player.RejectInteraction("没有干净盘子了，请归还空盘或取回暂存的盘子");

            plateCount--;
            BuildVisualStack();
            GameObject go = new GameObject("HeldPlate");
            PlateHandle plate = go.AddComponent<PlateHandle>();
            plate.Init(Vector3.zero);
            player.AttachPlate(plate);
            FeedbackFX.Punch(plate.transform, 0.3f, 0.35f);
            FeedbackFX.Puff(StackTop() + Vector3.up * 0.1f, new Color(0.9f, 0.92f, 0.95f), 2, 0.1f, 0.4f, 0.35f);
            Play(SfxType.Pickup);
            return true;
        }

        private Vector3 StackTop()
        {
            if (plateVisuals.Count > 0 && plateVisuals[plateVisuals.Count - 1] != null)
                return plateVisuals[plateVisuals.Count - 1].transform.position;
            return transform.position + Vector3.up * 0.6f;
        }

        public void ResetStation()
        {
            InitializeStock();
            plateCount = initialPlateCount;
            BuildVisualStack();
        }

        private static void Play(SfxType sound)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.Play(sound);
        }

        public string GetPrompt() => "餐盘台：空手 E 端盘，E 归还空盘（剩 " + plateCount + " 个）";
        public Transform GetTransform() => transform;
    }
}
