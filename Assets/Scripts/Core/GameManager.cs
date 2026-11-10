using System;
using System.Collections.Generic;
using UnityEngine;
using KitchenChaos.Data;
using KitchenChaos.Audio;
using KitchenChaos.Visual;

namespace KitchenChaos.Core
{
    public enum GameState { Ready, Playing, Paused, DayComplete, GameOver, Victory }

    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }
        public TimerSystem Timer { get; private set; }
        public OrderSystem Orders { get; private set; }
        public ScoreManager Score { get; private set; }
        public GameState State { get; private set; } = GameState.Ready;
        public bool IsPlaying => State == GameState.Playing;
        public bool IsGameOver => State == GameState.GameOver || State == GameState.Victory;

        public int startReputation = 3;
        public float roundDuration = 150f;
        public bool SaveRecords { get; set; } = true;
        public int Day { get; private set; } = 1;
        public int DayTarget => CampaignRules.TargetForDay(Day);
        public int DayCompletedOrders => Score.CompletedOrders - completedAtDayStart;
        public int DayFailedOrders => Score.FailedOrders - failedAtDayStart;
        public int DayMistakes => Score.Mistakes - mistakesAtDayStart;
        public int DayStars { get; private set; }
        public int TotalStars { get; private set; }
        public int Reputation { get; private set; }
        public int MaxReputation => Mathf.Max(1, startReputation) + extraHearts;
        public string EndReason { get; private set; } = string.Empty;
        public string LastDeliveryError { get; private set; } = string.Empty;
        public int BestScore { get; private set; }
        public int BestDay { get; private set; }
        public int TotalWins { get; private set; }
        public KitchenUpgrade[] UpgradeChoices { get; private set; } = Array.Empty<KitchenUpgrade>();
        public string UpgradeSummary => upgradeNames.Count == 0 ? "尚未选择升级" : string.Join(" · ", upgradeNames);
        public float CookingSpeedMultiplier { get; private set; } = 1f;
        public float MoveSpeedMultiplier { get; private set; } = 1f;
        public float BurnGraceBonus { get; private set; }
        public float OrderPatienceBonus { get; private set; }
        public float TipMultiplier { get; private set; } = 1f;

        public event Action<GameState> StateChanged;
        public event Action<int> ReputationChanged;
        public event Action<string, bool> Toast;
        public event Action DayStarting;

        private const string RecordPrefix = "KitchenChaos.Campaign.";
        private readonly List<string> upgradeNames = new List<string>();
        private int completedAtDayStart;
        private int failedAtDayStart;
        private int mistakesAtDayStart;
        private int extraHearts;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            Time.timeScale = 1f;
            BestScore = PlayerPrefs.GetInt(RecordPrefix + "BestScore", 0);
            BestDay = PlayerPrefs.GetInt(RecordPrefix + "BestDay", 0);
            TotalWins = PlayerPrefs.GetInt(RecordPrefix + "Wins", 0);
        }

        public void Initialize(TimerSystem timer, OrderSystem orders, ScoreManager score)
        {
            Timer = timer;
            Orders = orders;
            Score = score;
            Orders.OrderExpired += OnOrderExpired;
            Orders.OrderSpawned += OnOrderSpawned;
            Orders.OrderCritical += OnOrderCritical;
            Timer.Expired += OnTimeUp;
        }

        public void StartGame()
        {
            if (State != GameState.Ready && !IsGameOver) return;
            Day = 1;
            TotalStars = 0;
            CookingSpeedMultiplier = MoveSpeedMultiplier = TipMultiplier = 1f;
            BurnGraceBonus = OrderPatienceBonus = 0f;
            extraHearts = 0;
            upgradeNames.Clear();
            Score.ResetState();
            StartDay();
        }

        private void StartDay()
        {
            Orders.StopSpawning();
            Orders.ClearOrders();
            Timer.Stop();
            DayStarting?.Invoke();
            Reputation = MaxReputation;
            DayStars = 0;
            EndReason = string.Empty;
            completedAtDayStart = Score.CompletedOrders;
            failedAtDayStart = Score.FailedOrders;
            mistakesAtDayStart = Score.Mistakes;
            Score.ResetCombo();
            Orders.ConfigureDay(Day, OrderPatienceBonus);
            UpgradeChoices = Array.Empty<KitchenUpgrade>();
            SetState(GameState.Playing);
            Timer.BeginRound(CampaignRules.DurationForDay(Day, roundDuration));
            Orders.StartSpawning();
            ReputationChanged?.Invoke(Reputation);
            ShowToast("第 " + Day + " 天：完成 " + DayTarget + " 单即可晋级！", true);
        }

        private void SetState(GameState state)
        {
            State = state;
            Time.timeScale = state == GameState.Playing || state == GameState.Ready ? 1f : 0f;
            StateChanged?.Invoke(state);
        }

        public void TogglePause()
        {
            if (State == GameState.Playing) SetState(GameState.Paused);
            else if (State == GameState.Paused) SetState(GameState.Playing);
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused && IsPlaying) SetState(GameState.Paused);
        }

        private void OnApplicationQuit()
        {
            if (Score != null && State != GameState.Ready) PersistRecords();
        }

        public void ReturnToMenu()
        {
            if (State != GameState.Ready) PersistRecords();
            Orders.StopSpawning();
            Orders.ClearOrders();
            Timer.Stop();
            DayStarting?.Invoke();
            SetState(GameState.Ready);
        }

        public bool TryDeliverDish(List<Ingredient> dish)
        {
            if (!IsPlaying || dish == null || dish.Count == 0) return false;
            DeliveryResult result = Orders.TryDeliver(dish);
            if (result != null)
            {
                LastDeliveryError = string.Empty;
                int gained = Score.RegisterSuccess(Mathf.RoundToInt(result.baseScore * TipMultiplier), result.speedBonus);
                PlaySound(SfxType.DeliverSuccess);
                if (Score.Combo >= 3) PlaySound(SfxType.Combo);
                ShowToast("出餐成功！+" + gained + " 分 · " + Score.Combo + " 连单", true);
                if (DayCompletedOrders >= DayTarget) CompleteDay();
                return true;
            }
            LastDeliveryError = ExplainDeliveryFailure(dish);
            PlaySound(SfxType.DeliverFail);
            Score.RegisterMistake();
            LoseReputation("出餐错误：" + LastDeliveryError, false);
            return false;
        }

        /// <summary>告诉玩家这盘菜和哪张订单对不上、差在哪里，而不是只说“配方不对”。</summary>
        private string ExplainDeliveryFailure(List<Ingredient> dish)
        {
            if (Orders == null || Orders.ActiveOrders.Count == 0)
                return "现在没有订单，上一张单可能已经超时";
            Order closest = null;
            int bestOverlap = -1;
            foreach (Order order in Orders.ActiveOrders)
            {
                if (order == null || order.recipe == null) continue;
                int overlap = 0;
                foreach (RecipeIngredient need in order.recipe.ingredients)
                    foreach (Ingredient item in dish)
                        if (item != null && item.type == need.type) { overlap++; break; }
                if (overlap > bestOverlap) { bestOverlap = overlap; closest = order; }
            }
            if (closest == null || closest.recipe == null) return "没有匹配的订单";
            List<string> problems = new List<string>();
            foreach (RecipeIngredient need in closest.recipe.ingredients)
            {
                Ingredient match = null;
                foreach (Ingredient item in dish)
                    if (item != null && item.type == need.type) { match = item; break; }
                if (match == null)
                    problems.Add("缺" + (need.requiredState == IngredientState.Raw ? string.Empty : StateLabel(need.requiredState)) + IngredientCatalog.GetName(need.type));
                else if (match.state != need.requiredState)
                    problems.Add(IngredientCatalog.GetName(need.type) + "要" + StateLabel(need.requiredState) + "，你的是" + StateLabel(match.state));
            }
            foreach (Ingredient item in dish)
            {
                if (item == null) continue;
                bool wanted = false;
                foreach (RecipeIngredient need in closest.recipe.ingredients)
                    if (need.type == item.type) { wanted = true; break; }
                if (!wanted) problems.Add("盘里多了" + IngredientCatalog.GetName(item.type));
            }
            if (problems.Count == 0) return "盘里的 " + dish.Count + " 样食材和「" + closest.recipe.recipeName + "」对不上";
            string detail = problems.Count <= 2 ? string.Join("；", problems) : problems[0] + "；" + problems[1] + "…";
            return "「" + closest.recipe.recipeName + "」" + detail;
        }

        private static string StateLabel(IngredientState state)
        {
            switch (state)
            {
                case IngredientState.Cut: return "切好的";
                case IngredientState.Cooked: return "煎熟的";
                case IngredientState.Burned: return "焦糊的";
                default: return "原料";
            }
        }

        private void OnOrderSpawned(Order order)
        {
            if (!IsPlaying || order == null || order.urgency != OrderUrgency.Critical) return;
            PlaySound(SfxType.Warning);
        }

        private void OnOrderCritical(Order order)
        {
            if (!IsPlaying || order == null || order.recipe == null) return;
            PlaySound(SfxType.Warning);
            ShowToast("特急订单快超时：" + order.recipe.recipeName + " · 剩余 "
                + Mathf.CeilToInt(Mathf.Max(0f, order.timeRemaining)) + " 秒", false);
        }

        /// <summary>煎糊属于操作失误：中断连击并计入当日失误，影响评星。</summary>
        public void RegisterBurntIngredient(IngredientType type)
        {
            if (!IsPlaying) return;
            Score.RegisterMistake();
            ShowToast(IngredientCatalog.GetName(type) + "煎糊了，计入本日失误", false);
        }

        private void OnOrderExpired(Order order)
        {
            if (!IsPlaying) return;
            Score.RegisterFailure();
            PlaySound(SfxType.OrderExpire);
            FeedbackFX.Shake(0.16f, 0.28f);
            LoseReputation("订单超时：" + order.recipe.recipeName);
        }

        private void LoseReputation(string reason, bool harsh = true)
        {
            Reputation = Mathf.Max(0, Reputation - 1);
            ReputationChanged?.Invoke(Reputation);
            FeedbackFX.Shake(harsh ? 0.2f : 0.08f, harsh ? 0.32f : 0.14f);
            ShowToast(reason + " · 声望 -1", false);
            if (Reputation == 0) FinishRun(false, "声望耗尽，客人们离开了。下次优先处理即将超时的订单。");
        }

        private void OnTimeUp()
        {
            if (IsPlaying) FinishRun(false, "打烊了！今日完成 " + DayCompletedOrders + "/" + DayTarget + " 单，未达到营业目标。");
        }

        private void CompleteDay()
        {
            Orders.StopSpawning();
            Timer.Stop();
            DayStars = CampaignRules.Stars(Timer.TimeRemaining / Timer.RoundDuration, DayMistakes);
            TotalStars += DayStars;
            BestDay = Mathf.Max(BestDay, Day);
            if (Day == CampaignRules.TotalDays)
            {
                FinishRun(true, "五天挑战全部完成！你已经是一位独当一面的大厨。");
                return;
            }
            List<KitchenUpgrade> pool = new List<KitchenUpgrade>((KitchenUpgrade[])Enum.GetValues(typeof(KitchenUpgrade)));
            UpgradeChoices = new KitchenUpgrade[3];
            for (int i = 0; i < UpgradeChoices.Length; i++)
            {
                int index = UnityEngine.Random.Range(0, pool.Count);
                UpgradeChoices[i] = pool[index];
                pool.RemoveAt(index);
            }
            PersistRecords();
            PlaySound(SfxType.DayComplete);
            SetState(GameState.DayComplete);
            ShowToast("今日目标达成！选择一项升级，准备下一天。", true);
        }

        public bool ChooseUpgrade(int index)
        {
            if (State != GameState.DayComplete || index < 0 || index >= UpgradeChoices.Length) return false;
            KitchenUpgrade upgrade = UpgradeChoices[index];
            switch (upgrade)
            {
                case KitchenUpgrade.QuickHands: CookingSpeedMultiplier += 0.25f; break;
                case KitchenUpgrade.RunningShoes: MoveSpeedMultiplier += 0.15f; break;
                case KitchenUpgrade.PatientGuests: OrderPatienceBonus += 15f; break;
                case KitchenUpgrade.SafeStove: BurnGraceBonus += 5f; break;
                case KitchenUpgrade.ExtraHeart: extraHearts++; break;
                case KitchenUpgrade.BonusTips: TipMultiplier += 0.25f; break;
            }
            upgradeNames.Add(CampaignRules.UpgradeName(upgrade));
            Day++;
            StartDay();
            return true;
        }

        private void FinishRun(bool victory, string reason)
        {
            Orders.StopSpawning();
            Timer.Stop();
            EndReason = reason;
            if (victory) TotalWins++;
            PersistRecords();
            SetState(victory ? GameState.Victory : GameState.GameOver);
            PlaySound(victory ? SfxType.Victory : SfxType.GameOver);
            FeedbackFX.Shake(victory ? 0.1f : 0.28f, 0.5f);
        }

        private void PersistRecords()
        {
            BestScore = Mathf.Max(BestScore, Score.Score);
            if (!SaveRecords) return;
            PlayerPrefs.SetInt(RecordPrefix + "BestScore", BestScore);
            PlayerPrefs.SetInt(RecordPrefix + "BestDay", BestDay);
            PlayerPrefs.SetInt(RecordPrefix + "Wins", TotalWins);
            PlayerPrefs.Save();
        }

        private static void PlaySound(SfxType sound)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.Play(sound);
        }

        public void ShowToast(string message, bool positive) => Toast?.Invoke(message, positive);

        private void OnDestroy()
        {
            if (Orders != null)
            {
                Orders.OrderExpired -= OnOrderExpired;
                Orders.OrderSpawned -= OnOrderSpawned;
                Orders.OrderCritical -= OnOrderCritical;
            }
            if (Timer != null) Timer.Expired -= OnTimeUp;
            if (Instance == this)
            {
                Instance = null;
                Time.timeScale = 1f;
            }
        }
    }
}
