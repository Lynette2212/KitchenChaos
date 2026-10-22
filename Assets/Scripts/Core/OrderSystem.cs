using System;
using System.Collections.Generic;
using UnityEngine;
using KitchenChaos.Data;

namespace KitchenChaos.Core
{
    public class OrderSystem : MonoBehaviour
    {
        public float firstOrderDelay = 1.2f;
        public float minSpawnInterval = 12f;
        public float maxSpawnInterval = 16f;
        public int maxActiveOrders = 2;

        public List<Recipe> AvailableRecipes { get; private set; } = new List<Recipe>();
        public List<Order> ActiveOrders { get; private set; } = new List<Order>();
        public IReadOnlyList<Recipe> UnlockedRecipes => unlockedRecipes;
        public bool IsSpawning { get; private set; }
        public event Action OrdersChanged;
        public event Action<Order> OrderExpired;
        public event Action<Order> OrderDelivered;

        private readonly List<Recipe> unlockedRecipes = new List<Recipe>();
        private float spawnTimer;
        private float patienceBonus;
        private int day = 1;
        private Recipe lastRecipe;

        public void Initialize(List<Recipe> recipes)
        {
            StopSpawning();
            AvailableRecipes = recipes;
            ClearOrders();
            ConfigureDay(1, 0f);
        }

        public void ConfigureDay(int currentDay, float extraPatience)
        {
            day = currentDay;
            patienceBonus = extraPatience;
            minSpawnInterval = Mathf.Max(6f, 16f - day * 2f);
            maxSpawnInterval = minSpawnInterval + 3f;
            maxActiveOrders = day <= 2 ? 2 : day <= 4 ? 3 : 4;
            unlockedRecipes.Clear();
            foreach (Recipe recipe in AvailableRecipes)
                if (CampaignRules.UnlockDay(recipe) <= day) unlockedRecipes.Add(recipe);
            unlockedRecipes.Sort((a, b) => a.ingredients.Count.CompareTo(b.ingredients.Count));
            lastRecipe = null;
        }

        public void StartSpawning()
        {
            IsSpawning = true;
            spawnTimer = firstOrderDelay;
        }

        public void StopSpawning() => IsSpawning = false;

        private void Update() => AdvanceTime(Time.deltaTime);

        public void AdvanceTime(float deltaTime)
        {
            if (!IsSpawning || deltaTime <= 0f || (GameManager.Instance != null && !GameManager.Instance.IsPlaying)) return;
            for (int i = ActiveOrders.Count - 1; i >= 0; i--)
            {
                Order order = ActiveOrders[i];
                if (!order.Tick(deltaTime)) continue;
                ActiveOrders.RemoveAt(i);
                OrderExpired?.Invoke(order);
                OrdersChanged?.Invoke();
                if (!IsSpawning) return;
            }

            spawnTimer -= deltaTime;
            if (spawnTimer > 0f || ActiveOrders.Count >= maxActiveOrders || unlockedRecipes.Count == 0) return;
            SpawnOrder();
            spawnTimer = UnityEngine.Random.Range(minSpawnInterval, maxSpawnInterval);
        }

        private void SpawnOrder()
        {
            int index = UnityEngine.Random.Range(0, unlockedRecipes.Count);
            if (lastRecipe == null) index = unlockedRecipes.Count - 1;
            else if (unlockedRecipes.Count > 1 && unlockedRecipes[index] == lastRecipe)
                index = (index + 1) % unlockedRecipes.Count;
            Recipe recipe = unlockedRecipes[index];
            lastRecipe = recipe;
            ActiveOrders.Add(new Order(recipe, CampaignRules.OrderTimeLimit(recipe, day, patienceBonus)));
            OrdersChanged?.Invoke();
        }

        public DeliveryResult TryDeliver(List<Ingredient> dish)
        {
            Order best = null;
            foreach (Order order in ActiveOrders)
            {
                if (order.timeRemaining <= 0f || !order.recipe.Matches(dish)) continue;
                if (best == null || order.timeRemaining < best.timeRemaining) best = order;
            }
            if (best == null) return null;
            ActiveOrders.Remove(best);
            OrderDelivered?.Invoke(best);
            OrdersChanged?.Invoke();
            if (ActiveOrders.Count == 0) spawnTimer = Mathf.Min(spawnTimer, 2f);
            return new DeliveryResult { order = best, baseScore = best.recipe.score, speedBonus = best.SpeedBonus };
        }

        public void ClearOrders()
        {
            ActiveOrders.Clear();
            OrdersChanged?.Invoke();
        }
    }

    public class DeliveryResult
    {
        public Order order;
        public int baseScore;
        public float speedBonus;
    }
}
