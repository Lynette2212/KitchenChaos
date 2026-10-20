using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using KitchenChaos.Data;

namespace KitchenChaos.Core
{
    public class OrderSystem : MonoBehaviour
    {
        public int MaxActiveOrders = 3;
        public IReadOnlyList<Order> ActiveOrders => activeOrders;

        public event Action OrdersChanged;

        private readonly List<Order> activeOrders = new List<Order>();
        private readonly List<Recipe> recipes = new List<Recipe>();
        private System.Random random = new System.Random();

        public void Initialize(List<Recipe> recipePool)
        {
            recipes.Clear();
            if (recipePool != null) recipes.AddRange(recipePool);
            if (recipes.Count == 0)
            {
                Debug.LogError("OrderSystem: 没有可用菜谱，无法生成订单。");
                return;
            }

            activeOrders.Clear();
            for (int i = 0; i < MaxActiveOrders; i++) AddRandomOrder();
            OrdersChanged?.Invoke();
        }

        public bool TryDeliver(Order order, IngredientType type, out bool completed, out int baseScore)
        {
            completed = false;
            baseScore = 0;

            if (order == null || !activeOrders.Contains(order)) return false;
            if (!order.TryDeliver(type)) return false;

            if (order.IsComplete)
            {
                completed = true;
                baseScore = order.recipe != null ? order.recipe.score : 0;
                activeOrders.Remove(order);
                StartCoroutine(AddOrderAfterDelay());
            }

            OrdersChanged?.Invoke();
            return true;
        }

        private IEnumerator AddOrderAfterDelay()
        {
            yield return new WaitForSeconds(1.2f);
            if (GameManager.Instance == null || !GameManager.Instance.IsPlaying) yield break;

            AddRandomOrder();
            OrdersChanged?.Invoke();
        }

        private void AddRandomOrder()
        {
            if (recipes.Count == 0) return;

            Recipe recipe = recipes[random.Next(recipes.Count)];
            activeOrders.Add(new Order(recipe));
        }
    }
}
