using System;
using UnityEngine;

namespace KitchenChaos.Core
{
    /// <summary>
    /// 整局倒计时。时间归零触发 GameOver。
    /// </summary>
    public class TimerSystem : MonoBehaviour
    {
        public float RoundDuration = 150f;

        public float TimeRemaining { get; private set; }
        public bool IsRunning { get; private set; }

        public event Action<float> TimeChanged;
        public event Action Expired;

        public void BeginRound(float duration)
        {
            RoundDuration = duration;
            TimeRemaining = duration;
            IsRunning = true;
            TimeChanged?.Invoke(TimeRemaining);
        }

        private void Update() => AdvanceTime(Time.deltaTime);

        public void AdvanceTime(float deltaTime)
        {
            if (!IsRunning || deltaTime <= 0f || (GameManager.Instance != null && !GameManager.Instance.IsPlaying)) return;
            TimeRemaining = Mathf.Max(0f, TimeRemaining - deltaTime);
            TimeChanged?.Invoke(TimeRemaining);
            if (TimeRemaining == 0f)
            {
                IsRunning = false;
                Expired?.Invoke();
            }
        }

        public void Stop()
        {
            IsRunning = false;
        }
    }
}
