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
        public event Action<Order> OrderSpawned;
        public event Action<Order> OrderCritical;

        private readonly List<Recipe> unlockedRecipes = new List<Recipe>();
        private float spawnTimer;
        private float patienceBonus;
        private int day = 1;
        private int spawnedToday;
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
            spawnedToday = 0;
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
                if (!order.Tick(deltaTime))
                {
                    CheckCritical(order);
                    continue;
                }
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

        private void CheckCritical(Order order)
        {
            if (order.warned || order.urgency != OrderUrgency.Critical || order.TimeRatio > 0.22f) return;
            order.warned = true;
            OrderCritical?.Invoke(order);
        }

        /// <summary>菜单按剩余时间自动排队，越急的订单越靠上（交付时也优先匹配最急的一张）。</summary>
        public void SortByRemaining()
        {
            ActiveOrders.Sort((a, b) => a.timeRemaining.CompareTo(b.timeRemaining));
        }

        private void SpawnOrder()
        {
            int index = UnityEngine.Random.Range(0, unlockedRecipes.Count);
            if (lastRecipe == null) index = unlockedRecipes.Count - 1;
            else if (unlockedRecipes.Count > 1 && unlockedRecipes[index] == lastRecipe)
                index = (index + 1) % unlockedRecipes.Count;
            Recipe recipe = unlockedRecipes[index];
            lastRecipe = recipe;
            OrderUrgency urgency = spawnedToday == 0
                ? (day >= 3 ? OrderUrgency.Critical : OrderUrgency.Express)
                : RollUrgency();
            spawnedToday++;
            Order order = new Order(recipe, CampaignRules.OrderTimeLimit(recipe, day, patienceBonus, urgency), urgency);
            ActiveOrders.Add(order);
            SortByRemaining();
            OrdersChanged?.Invoke();
            OrderSpawned?.Invoke(order);
        }

        private OrderUrgency RollUrgency()
        {
            int roll = UnityEngine.Random.Range(0, 100);
            if (day <= 2) return roll < 18 ? OrderUrgency.Express : OrderUrgency.Normal;
            if (day <= 4) return roll < 12 ? OrderUrgency.Critical : roll < 48 ? OrderUrgency.Express : OrderUrgency.Normal;
            return roll < 24 ? OrderUrgency.Critical : roll < 66 ? OrderUrgency.Express : OrderUrgency.Normal;
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
            return new DeliveryResult
            {
                order = best,
                baseScore = best.recipe.score,
                speedBonus = best.SpeedBonus * best.UrgencyScoreMultiplier
            };
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
