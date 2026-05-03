using System;
using UnityEngine;

namespace LittlePlanet.Balance
{
    [Serializable]
    public sealed class BalanceUpgradeCurve
    {
        [SerializeField] private string label = "Upgrade";
        [SerializeField] private UpgradeDefinition target;
        [SerializeField, Min(0)] private int baseCost = 100;
        [SerializeField, Min(1f)] private float costMultiplier = 1.5f;
        [SerializeField, Min(1)] private int maxLevel = 6;
        [SerializeField] private float valuePerLevel = 1f;
        [SerializeField] private bool includeInTotal = true;

        public string Label => !string.IsNullOrWhiteSpace(label) ? label : target != null ? target.UpgradeName : "Upgrade";
        public UpgradeDefinition Target => target;
        public int BaseCost => baseCost;
        public float CostMultiplier => costMultiplier;
        public int MaxLevel => maxLevel;
        public float ValuePerLevel => valuePerLevel;
        public bool IncludeInTotal => includeInTotal;
        public bool IsActive => target != null && maxLevel > 0;
        public UpgradeType UpgradeType => target != null ? target.UpgradeType : default;
        public bool IsBalanceRelevant => IsActive && UpgradeType != UpgradeType.FlyDuration && UpgradeType != UpgradeType.FlyCooldown;

        public void PullFromTarget()
        {
            if (target == null)
            {
                return;
            }

            label = target.UpgradeName;
            baseCost = target.BaseCost;
            costMultiplier = target.CostMultiplier;
            maxLevel = target.MaxLevel;
            valuePerLevel = target.ValuePerLevel;
        }

        public void SetValues(int nextBaseCost, float nextCostMultiplier, int nextMaxLevel, float nextValuePerLevel)
        {
            baseCost = Mathf.Max(0, nextBaseCost);
            costMultiplier = Mathf.Max(1f, nextCostMultiplier);
            maxLevel = Mathf.Max(1, nextMaxLevel);
            valuePerLevel = nextValuePerLevel;
        }

        public void SetIncludedInTotal(bool included)
        {
            includeInTotal = included;
        }

        public int GetCostForLevel(int level)
        {
            var clampedLevel = Mathf.Clamp(level, 0, maxLevel);
            return Mathf.Max(0, Mathf.RoundToInt(baseCost * Mathf.Pow(costMultiplier, clampedLevel)));
        }

        public float GetAccumulatedValueForLevel(int level)
        {
            return Mathf.Max(0, level) * valuePerLevel;
        }

        public int GetTotalCost()
        {
            var total = 0;
            for (var i = 0; i < maxLevel; i++)
            {
                total += GetCostForLevel(i);
            }

            return total;
        }

        public void ApplyToTarget()
        {
            if (target == null)
            {
                return;
            }

            target.ApplyBalance(baseCost, costMultiplier, maxLevel, valuePerLevel);
        }
    }
}
