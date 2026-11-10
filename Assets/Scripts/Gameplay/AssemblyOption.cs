using System;
using UnityEngine;
using KitchenChaos.Data;
using KitchenChaos.Visual;

namespace KitchenChaos.Gameplay
{
    /// <summary>
    /// 组装近景里的一份可拿食材。对准后按 E，把它放到面前的汉堡上。
    /// </summary>
    public class AssemblyOption : MonoBehaviour, IInteractable
    {
        private PlateHandle plate;
        private Ingredient data;
        private Func<Ingredient> take;
        private PrepCounter station;

        public void Setup(PrepCounter owner, PlateHandle target, Ingredient ingredient, Func<Ingredient> takeFromSource)
        {
            station = owner;
            plate = target;
            data = ingredient != null ? ingredient.Clone() : null;
            take = takeFromSource;
            GameObject visual = VisualFactory.CreateIngredientVisual(data.type, data.state);
            visual.transform.SetParent(transform, false);
            visual.transform.localScale = Vector3.one * 0.55f;
            BoxCollider box = gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.08f, 0f);
            box.size = new Vector3(0.28f, 0.24f, 0.28f);
        }

        public bool Interact(PlayerController player)
        {
            if (player == null || !player.CanInteract || plate == null || take == null) return false;
            string reason = plate.GetAddFailureReason(data);
            if (reason != null) return player.RejectInteraction(reason);
            Ingredient got = take();
            if (got == null) return player.RejectInteraction("这份食材刚刚被拿走了");
            if (!plate.Add(got)) return false;
            FeedbackFX.Punch(transform, 0.25f, 0.3f);
            if (station != null) station.RefreshAssembly();
            return true;
        }

        public string GetPrompt() => "按 E 把" + IngredientCatalog.GetName(data.type) + "放上汉堡";

        public Transform GetTransform() => transform;
    }
}
