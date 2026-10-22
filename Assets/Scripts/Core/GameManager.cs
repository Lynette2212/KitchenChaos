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
        public int DayStars { get; private set; }
        public int TotalStars { get; private set; }
        public int Reputation { get; private set; }
        public int MaxReputation => Mathf.Max(1, startReputation) + extraHearts;
        public string EndReason { get; private set; } = string.Empty;
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
                int gained = Score.RegisterSuccess(Mathf.RoundToInt(result.baseScore * TipMultiplier), result.speedBonus);
                PlaySound(SfxType.DeliverSuccess);
                if (Score.Combo >= 3) PlaySound(SfxType.Combo);
                ShowToast("出餐成功！+" + gained + " 分 · " + Score.Combo + " 连单", true);
                if (DayCompletedOrders >= DayTarget) CompleteDay();
                return true;
            }
            Score.RegisterFailure();
            PlaySound(SfxType.DeliverFail);
            LoseReputation("配方不匹配，连击已中断");
            return false;
        }

        private void OnOrderExpired(Order order)
        {
            if (!IsPlaying) return;
            Score.RegisterFailure();
            PlaySound(SfxType.OrderExpire);
            FeedbackFX.Shake(0.16f, 0.28f);
            LoseReputation("订单超时：" + order.recipe.recipeName);
        }

        private void LoseReputation(string reason)
        {
            Reputation = Mathf.Max(0, Reputation - 1);
            ReputationChanged?.Invoke(Reputation);
            FeedbackFX.Shake(0.2f, 0.32f);
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
            DayStars = CampaignRules.Stars(Timer.TimeRemaining / Timer.RoundDuration, DayFailedOrders);
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
            if (Orders != null) Orders.OrderExpired -= OnOrderExpired;
            if (Timer != null) Timer.Expired -= OnTimeUp;
            if (Instance == this)
            {
                Instance = null;
                Time.timeScale = 1f;
            }
        }
    }
}
