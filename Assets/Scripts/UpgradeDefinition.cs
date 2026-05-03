using UnityEngine;

[CreateAssetMenu(fileName = "Upgrade", menuName = "Little Planet/Upgrade")]
public class UpgradeDefinition : ScriptableObject
{
    [SerializeField] private string upgradeName = "Upgrade";
    [SerializeField, TextArea] private string description;
    [SerializeField, Min(0)] private int baseCost = 100;
    [SerializeField, Min(1f)] private float costMultiplier = 1.25f;
    [SerializeField] private UpgradeType upgradeType;
    [SerializeField, Min(1)] private int maxLevel = 1;
    [SerializeField] private float valuePerLevel = 1f;
    [SerializeField] private Sprite icon;

    public string UpgradeName => string.IsNullOrWhiteSpace(upgradeName) ? name : upgradeName;
    public string Description => description;
    public int BaseCost => baseCost;
    public float CostMultiplier => costMultiplier;
    public UpgradeType UpgradeType => upgradeType;
    public int MaxLevel => maxLevel;
    public float ValuePerLevel => valuePerLevel;
    public Sprite Icon => icon;

    public int GetCostForLevel(int currentLevel)
    {
        var clampedLevel = Mathf.Clamp(currentLevel, 0, maxLevel);
        return Mathf.Max(0, Mathf.RoundToInt(baseCost * Mathf.Pow(costMultiplier, clampedLevel)));
    }

    public void ApplyBalance(int nextBaseCost, float nextCostMultiplier, int nextMaxLevel, float nextValuePerLevel)
    {
        baseCost = Mathf.Max(0, nextBaseCost);
        costMultiplier = Mathf.Max(1f, nextCostMultiplier);
        maxLevel = Mathf.Max(1, nextMaxLevel);
        valuePerLevel = nextValuePerLevel;
    }
}
