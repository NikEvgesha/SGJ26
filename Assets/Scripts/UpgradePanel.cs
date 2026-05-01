using System.Collections.Generic;
using LittlePlanet.HybridTerraform;
using LittlePlanet.PlanetSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UpgradePanel : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Button upgradeButton;
    [SerializeField] private UpgradeSlotUI slotPrefab;
    [SerializeField] private Transform slotsRoot;
    [SerializeField] private string slotsRootObjectName = "UpgradesRoot";
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Planet planet;
    [SerializeField] private PlanetFlyTerraformSkill flySkill;
    [SerializeField] private Button buyButton;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text currentValueText;
    [SerializeField] private TMP_Text newValueText;

    [Header("Upgrades")]
    [SerializeField] private List<UpgradeDefinition> upgrades = new();
    [SerializeField] private bool startOpen;

    private readonly List<UpgradeSlotUI> _slots = new();
    private readonly Dictionary<UpgradeDefinition, int> _levels = new();
    private UpgradeSlotUI _selectedSlot;
    private UpgradeDefinition _selectedUpgrade;
    private bool _isOpen;

    private void Awake()
    {
        ResolveReferences();
        BuildSlots();
        SetOpen(startOpen);
    }

    private void OnEnable()
    {
        ResolveReferences();
        BindUpgradeButton();
        BindBuyButton();
        BindCurrencyEvents();
        RefreshInfo();
    }

    private void OnDisable()
    {
        UnbindCurrencyEvents();
        UnbindBuyButton();
        UnbindUpgradeButton();
    }

    public void Toggle()
    {
        SetOpen(!_isOpen);
    }

    private void SetOpen(bool isOpen)
    {
        _isOpen = isOpen;
        EnsureCanvasGroup();
        canvasGroup.alpha = isOpen ? 1f : 0f;
        canvasGroup.interactable = isOpen;
        canvasGroup.blocksRaycasts = isOpen;
    }

    private void BuildSlots()
    {
        if (slotPrefab == null || slotsRoot == null)
        {
            return;
        }

        for (var i = slotsRoot.childCount - 1; i >= 0; i--)
        {
            var child = slotsRoot.GetChild(i);
            if (child == null || child.GetComponent<UpgradeSlotUI>() == null)
            {
                continue;
            }

            Destroy(child.gameObject);
        }

        _slots.Clear();
        for (var i = 0; i < upgrades.Count; i++)
        {
            var upgrade = upgrades[i];
            if (upgrade == null)
            {
                continue;
            }

            var slot = Instantiate(slotPrefab, slotsRoot);
            var level = GetLevel(upgrade);
            slot.Initialize(this, upgrade, level);
            _slots.Add(slot);
        }
    }

    public void SelectUpgrade(UpgradeSlotUI slot)
    {
        if (slot == null || slot.Upgrade == null)
        {
            return;
        }

        _selectedSlot = slot;
        _selectedUpgrade = slot.Upgrade;
        RefreshInfo();
    }

    private void BuySelectedUpgrade()
    {
        if (_selectedUpgrade == null || planet == null || flySkill == null)
        {
            return;
        }

        var currentLevel = GetLevel(_selectedUpgrade);
        if (currentLevel >= _selectedUpgrade.MaxLevel)
        {
            RefreshInfo();
            return;
        }

        var price = _selectedUpgrade.GetCostForLevel(currentLevel);
        if (!planet.Currency.TrySpend(price))
        {
            RefreshInfo();
            return;
        }

        flySkill.AddUpgradeValue(_selectedUpgrade.UpgradeType, _selectedUpgrade.ValuePerLevel);
        SetLevel(_selectedUpgrade, currentLevel + 1);
        RefreshInfo();
    }

    private void ResolveReferences()
    {
        if (slotsRoot == null)
        {
            var root = FindChildByName(transform, slotsRootObjectName);
            slotsRoot = root != null ? root : transform;
        }

        EnsureCanvasGroup();
        if (planet == null)
        {
            planet = FindFirstObjectByType<Planet>();
        }

        if (flySkill == null)
        {
            flySkill = FindFirstObjectByType<PlanetFlyTerraformSkill>();
        }

        if (upgradeButton == null)
        {
            var buttonObject = GameObject.Find("Upgrade");
            if (buttonObject != null)
            {
                upgradeButton = buttonObject.GetComponent<Button>();
            }
        }

        var infoRoot = FindChildByName(transform, "Info");
        if (nameText == null)
        {
            nameText = FindText(infoRoot, "name");
        }

        if (descriptionText == null)
        {
            descriptionText = FindText(infoRoot, "Description");
        }

        if (currentValueText == null)
        {
            currentValueText = FindText(infoRoot, "CurrentValue");
        }

        if (newValueText == null)
        {
            newValueText = FindText(infoRoot, "NewValue");
        }

        if (buyButton == null)
        {
            buyButton = FindSelectable<Button>(infoRoot, "Button");
        }

        if (priceText == null)
        {
            priceText = FindText(buyButton != null ? buyButton.transform : infoRoot, "price");
        }
    }

    private void EnsureCanvasGroup()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }

    private void BindUpgradeButton()
    {
        if (upgradeButton == null)
        {
            return;
        }

        upgradeButton.onClick.RemoveListener(Toggle);
        upgradeButton.onClick.AddListener(Toggle);
    }

    private void BindBuyButton()
    {
        if (buyButton == null)
        {
            return;
        }

        buyButton.onClick.RemoveListener(BuySelectedUpgrade);
        buyButton.onClick.AddListener(BuySelectedUpgrade);
    }

    private void UnbindBuyButton()
    {
        if (buyButton != null)
        {
            buyButton.onClick.RemoveListener(BuySelectedUpgrade);
        }
    }

    private void BindCurrencyEvents()
    {
        if (planet != null)
        {
            planet.Currency.AmountChanged -= HandleCurrencyChanged;
            planet.Currency.AmountChanged += HandleCurrencyChanged;
        }
    }

    private void UnbindCurrencyEvents()
    {
        if (planet != null)
        {
            planet.Currency.AmountChanged -= HandleCurrencyChanged;
        }
    }

    private void HandleCurrencyChanged(int amount)
    {
        RefreshInfo();
    }

    private int GetLevel(UpgradeDefinition upgrade)
    {
        return upgrade != null && _levels.TryGetValue(upgrade, out var level) ? level : 0;
    }

    private void SetLevel(UpgradeDefinition upgrade, int level)
    {
        if (upgrade == null)
        {
            return;
        }

        var clamped = Mathf.Clamp(level, 0, upgrade.MaxLevel);
        _levels[upgrade] = clamped;

        for (var i = 0; i < _slots.Count; i++)
        {
            if (_slots[i] != null && _slots[i].Upgrade == upgrade)
            {
                _slots[i].SetLevel(clamped);
            }
        }
    }

    private void RefreshInfo()
    {
        var hasUpgrade = _selectedUpgrade != null;
        if (nameText != null)
        {
            nameText.text = hasUpgrade ? _selectedUpgrade.UpgradeName : string.Empty;
        }

        if (descriptionText != null)
        {
            descriptionText.text = hasUpgrade ? _selectedUpgrade.Description : string.Empty;
        }

        if (!hasUpgrade || flySkill == null)
        {
            if (currentValueText != null)
            {
                currentValueText.text = string.Empty;
            }

            if (newValueText != null)
            {
                newValueText.text = string.Empty;
            }

            if (priceText != null)
            {
                priceText.text = string.Empty;
            }

            if (buyButton != null)
            {
                buyButton.interactable = false;
            }

            return;
        }

        var level = GetLevel(_selectedUpgrade);
        var currentValue = flySkill.GetUpgradeValue(_selectedUpgrade.UpgradeType);
        var isMaxLevel = level >= _selectedUpgrade.MaxLevel;
        var nextValue = isMaxLevel ? currentValue : GetNextValue(_selectedUpgrade, currentValue);
        var price = _selectedUpgrade.GetCostForLevel(level);

        if (currentValueText != null)
        {
            currentValueText.text = $"Current: {FormatValue(currentValue, _selectedUpgrade.UpgradeType)}";
        }

        if (newValueText != null)
        {
            newValueText.text = isMaxLevel
                ? "New: Max level"
                : $"New: {FormatValue(nextValue, _selectedUpgrade.UpgradeType)}";
        }

        if (priceText != null)
        {
            priceText.text = isMaxLevel ? "MAX" : price.ToString();
        }

        if (buyButton != null)
        {
            buyButton.interactable = !isMaxLevel && planet != null && planet.Currency.Amount >= price;
        }
    }

    private static float GetNextValue(UpgradeDefinition upgrade, float currentValue)
    {
        if (upgrade == null)
        {
            return currentValue;
        }

        return upgrade.UpgradeType switch
        {
            UpgradeType.FlyRadius => Mathf.RoundToInt(currentValue + upgrade.ValuePerLevel),
            UpgradeType.FlyCooldown => Mathf.Max(0f, currentValue - upgrade.ValuePerLevel),
            _ => currentValue + upgrade.ValuePerLevel
        };
    }

    private static string FormatValue(float value, UpgradeType type)
    {
        return type == UpgradeType.FlyRadius
            ? Mathf.RoundToInt(value).ToString()
            : value.ToString("0.##");
    }

    private void UnbindUpgradeButton()
    {
        if (upgradeButton != null)
        {
            upgradeButton.onClick.RemoveListener(Toggle);
        }
    }

    private static Transform FindChildByName(Transform root, string childName)
    {
        if (root == null || string.IsNullOrWhiteSpace(childName))
        {
            return null;
        }

        var children = root.GetComponentsInChildren<Transform>(true);
        for (var i = 0; i < children.Length; i++)
        {
            var child = children[i];
            if (child != null && string.Equals(child.name, childName, System.StringComparison.Ordinal))
            {
                return child;
            }
        }

        return null;
    }

    private static TMP_Text FindText(Transform root, string childName)
    {
        var child = FindChildByName(root, childName);
        return child != null ? child.GetComponent<TMP_Text>() : null;
    }

    private static T FindSelectable<T>(Transform root, string childName) where T : Selectable
    {
        var child = FindChildByName(root, childName);
        return child != null ? child.GetComponent<T>() : null;
    }
}
