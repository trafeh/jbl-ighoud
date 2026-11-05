using System;
using UnityEngine;

namespace JbelIghoud.Survival
{
    public enum StatType
    {
        Health,
        Respiration,
        Hunger,
        Sleep
    }

    [System.Serializable]
    public class SurvivalStat
    {
        [SerializeField] private StatType statType;
        [SerializeField] private float currentValue;
        [SerializeField] private float maxValue;
        [SerializeField] private float depletionRate;
        [SerializeField] private float recoveryRate;
        [SerializeField] private float warningThreshold;
        [SerializeField] private float criticalThreshold;

        // Events
        public event Action<SurvivalStat> OnValueChanged;
        public event Action<SurvivalStat> OnDepleted;
        public event Action<SurvivalStat> OnCritical;
        public event Action<SurvivalStat> OnRecovered;

        public StatType Type => statType;
        public float CurrentValue => currentValue;
        public float MaxValue => maxValue;
        public float DepletionRate { get => depletionRate; set => depletionRate = value; }
        public float RecoveryRate { get => recoveryRate; set => recoveryRate = value; }
        public float WarningThreshold { get => warningThreshold; set => warningThreshold = value; }
        public float CriticalThreshold { get => criticalThreshold; set => criticalThreshold = value; }

        public float Normalized => Mathf.Clamp01(currentValue / Mathf.Max(0.0001f, maxValue));
        public bool IsDepleted => currentValue <= 0.001f;
        public bool IsLow => currentValue <= warningThreshold;
        public bool IsCritical => currentValue <= criticalThreshold;
        public bool IsFull => currentValue >= maxValue - 0.001f;

        public SurvivalStat(StatType type, float max, float depletion = 0f, float recovery = 0f, float warning = 25f, float critical = 10f)
        {
            statType = type;
            maxValue = max;
            currentValue = max;
            depletionRate = depletion;
            recoveryRate = recovery;
            warningThreshold = warning;
            criticalThreshold = critical;
        }

        public void Initialize()
        {
            if (maxValue <= 0f) maxValue = 100f;
            if (currentValue <= 0f && statType != StatType.Health) currentValue = maxValue;
            else if (currentValue > maxValue) currentValue = maxValue;
            if (warningThreshold <= 0f) warningThreshold = 25f;
            if (criticalThreshold <= 0f) criticalThreshold = 10f;
        }

        public void Set(float value)
        {
            float previous = currentValue;
            currentValue = Mathf.Clamp(value, 0f, maxValue);

            if (Mathf.Abs(previous - currentValue) > 0.0001f)
            {
                OnValueChanged?.Invoke(this);

                if (currentValue <= 0.001f && previous > 0.001f)
                {
                    OnDepleted?.Invoke(this);
                }
                else if (currentValue <= criticalThreshold && previous > criticalThreshold)
                {
                    OnCritical?.Invoke(this);
                }
                else if (previous <= criticalThreshold && currentValue > criticalThreshold)
                {
                    OnRecovered?.Invoke(this);
                }
            }
        }

        public void Modify(float delta)
        {
            Set(currentValue + delta);
        }

        public void Deplete(float amount)
        {
            if (amount > 0f)
            {
                Modify(-amount);
            }
        }

        public void Recover(float amount)
        {
            if (amount > 0f)
            {
                Modify(amount);
            }
        }

        public void RestoreFull()
        {
            Set(maxValue);
        }
    }
}
