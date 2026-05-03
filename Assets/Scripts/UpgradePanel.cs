using System.Collections.Generic;
using System;
using LittlePlanet.HybridTerraform;
using LittlePlanet.PlanetSystem;
using LittlePlanet.RuntimeInput;
using LittlePlanet.UI;
using UnityEngine;
using UnityEngine.UI;

public class UpgradePanel : MonoBehaviour, IManagedWindow
{
    [Header("References")]
    [SerializeField] private Button upgradeButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private string closeButtonObjectName = "CloseArea";
    [SerializeField] private UpgradeSlotUI slotPrefab;
    [SerializeField] private Transform slotsRoot;
    [SerializeField] private string slotsRootObjectName = "UpgradesRoot";
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private WindowManager windowManager;
    [SerializeField] private Planet planet;
    [SerializeField] private PlanetFlyTerraformSkill flySkill;

    [Header("Upgrades")]
    [SerializeField] private List<UpgradeDefinition> upgrades = new();
    [SerializeField] private bool startOpen;

    [Header("Hotkeys")]
    [SerializeField] private bool enableHotkey = true;
    [SerializeField] private KeyCode openHotkey = KeyCode.U;

    [Header("Tutorial")]
    [SerializeField] private UpgradeType tutorialTrainingUpgradeType = UpgradeType.FlyPower;
    [SerializeField, Min(0)] private int tutorialTrainingPrice = 10;

    private readonly List<UpgradeSlotUI> _slots = new();
    private readonly Dictionary<UpgradeDefinition, int> _levels = new();
    private bool _isOpen;
    private UpgradeType? _tutorialAllowedUpgradeType;
    private bool _tutorialTrainingPriceEnabled;
    private bool _tutorialTrainingPurchaseDone;

    public bool IsWindowOpen => _isOpen;
    public event Action<bool> WindowStateChanged;
    public event Action<UpgradeDefinition, int> UpgradePurchased;

    private void Awake()
    {
        ResolveReferences();
        BuildSlots();
        SetWindowOpen(startOpen);
    }

    private void OnEnable()
    {
        ResolveReferences();
        BindButtons();
        windowManager?.Register(this);
        BindCurrencyEvents();
        RefreshSlots();
    }

    private void OnDisable()
    {
        UnbindCurrencyEvents();
        UnbindButtons();
        windowManager?.Unregister(this);
    }

    private void Update()
    {
        if (enableHotkey && InputCompat.WasKeyPressedThisFrame(openHotkey))
        {
            Toggle();
        }
    }

    public void Toggle()
    {
        ResolveReferences();
        if (windowManager != null)
        {
            windowManager.ToggleExclusive(this);
            return;
        }

        SetWindowOpen(!_isOpen);
    }

    public void SetWindowOpen(bool isOpen)
    {
        _isOpen = isOpen;
        EnsureCanvasGroup();
        canvasGroup.alpha = isOpen ? 1f : 0f;
        canvasGroup.interactable = isOpen;
        canvasGroup.blocksRaycasts = isOpen;
        WindowStateChanged?.Invoke(isOpen);
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
            slot.Initialize(this, upgrade, GetLevel(upgrade));
            _slots.Add(slot);
        }

        RefreshSlots();
    }

    public void BuyUpgrade(UpgradeSlotUI slot)
    {
        if (slot == null || slot.Upgrade == null || planet == null || flySkill == null)
        {
            return;
        }

        var upgrade = slot.Upgrade;
        var currentLevel = GetLevel(upgrade);
        if (currentLevel >= upgrade.MaxLevel)
        {
            RefreshSlots();
            return;
        }

        var price = GetPriceForLevel(upgrade, currentLevel);
        if (!planet.Currency.TrySpend(price))
        {
            RefreshSlots();
            return;
        }

        flySkill.AddUpgradeValue(upgrade.UpgradeType, upgrade.ValuePerLevel);
        if (_tutorialTrainingPriceEnabled
            && upgrade.UpgradeType == tutorialTrainingUpgradeType
            && currentLevel == 0
            && price == tutorialTrainingPrice)
        {
            _tutorialTrainingPurchaseDone = true;
        }

        SetLevel(upgrade, currentLevel + 1);
        RefreshSlots();
        UpgradePurchased?.Invoke(upgrade, currentLevel + 1);
    }

    public void SetHotkeyEnabled(bool isEnabled)
    {
        enableHotkey = isEnabled;
    }

    public void SetTutorialAllowedUpgrade(UpgradeType? allowedType)
    {
        _tutorialAllowedUpgradeType = allowedType;
        RefreshSlots();
    }

    public void SetTutorialTrainingPriceEnabled(bool isEnabled)
    {
        _tutorialTrainingPriceEnabled = isEnabled;
        RefreshSlots();
    }

    public RectTransform GetSlotRect(UpgradeType upgradeType)
    {
        for (var i = 0; i < _slots.Count; i++)
        {
            var slot = _slots[i];
            if (slot != null && slot.Upgrade != null && slot.Upgrade.UpgradeType == upgradeType)
            {
                return slot.transform as RectTransform;
            }
        }

        return null;
    }

    public int GetUpgradeLevel(UpgradeType upgradeType)
    {
        for (var i = 0; i < upgrades.Count; i++)
        {
            var upgrade = upgrades[i];
            if (upgrade != null && upgrade.UpgradeType == upgradeType)
            {
                return GetLevel(upgrade);
            }
        }

        return 0;
    }

    private void ResolveReferences()
    {
        if (slotsRoot == null)
        {
            var root = FindChildByName(transform, slotsRootObjectName);
            slotsRoot = root != null ? root : transform;
        }

        EnsureCanvasGroup();
        if (windowManager == null)
        {
            windowManager = WindowManager.Instance;
        }

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

        if (closeButton == null)
        {
            var closeButtonTransform = FindChildByName(transform, closeButtonObjectName);
            if (closeButtonTransform == null && transform.parent != null)
            {
                closeButtonTransform = FindChildByName(transform.parent, closeButtonObjectName);
            }

            if (closeButtonTransform != null)
            {
                closeButton = closeButtonTransform.GetComponent<Button>();
            }
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

    private void BindButtons()
    {
        if (upgradeButton == null && closeButton == null)
        {
            ResolveReferences();
        }

        if (upgradeButton != null)
        {
            upgradeButton.onClick.RemoveListener(Toggle);
            upgradeButton.onClick.AddListener(Toggle);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(CloseWindow);
            closeButton.onClick.AddListener(CloseWindow);
        }
    }

    private void UnbindButtons()
    {
        if (upgradeButton != null)
        {
            upgradeButton.onClick.RemoveListener(Toggle);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(CloseWindow);
        }
    }

    private void CloseWindow()
    {
        SetWindowOpen(false);
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
        RefreshSlots();
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

        _levels[upgrade] = Mathf.Clamp(level, 0, upgrade.MaxLevel);
    }

    private void RefreshSlots()
    {
        ResolveReferences();
        for (var i = 0; i < _slots.Count; i++)
        {
            RefreshSlot(_slots[i]);
        }
    }

    private void RefreshSlot(UpgradeSlotUI slot)
    {
        if (slot == null || slot.Upgrade == null)
        {
            return;
        }

        var upgrade = slot.Upgrade;
        var level = GetLevel(upgrade);
        var isMaxLevel = level >= upgrade.MaxLevel;
        var isAllowedByTutorial = !_tutorialAllowedUpgradeType.HasValue || upgrade.UpgradeType == _tutorialAllowedUpgradeType.Value;
        slot.gameObject.SetActive(isAllowedByTutorial);
        if (!isAllowedByTutorial)
        {
            return;
        }

        var price = GetPriceForLevel(upgrade, level);
        var currentValue = flySkill != null ? flySkill.GetUpgradeValue(upgrade.UpgradeType) : 0f;
        var nextValue = isMaxLevel ? currentValue : GetNextValue(upgrade, currentValue);
        var canBuy = !isMaxLevel && planet != null && flySkill != null && planet.Currency.Amount >= price;

        slot.SetState(
            level,
            price,
            FormatValue(currentValue, upgrade.UpgradeType),
            FormatValue(nextValue, upgrade.UpgradeType),
            canBuy,
            isMaxLevel);
    }

    private int GetPriceForLevel(UpgradeDefinition upgrade, int level)
    {
        if (upgrade == null)
        {
            return 0;
        }

        if (_tutorialTrainingPriceEnabled
            && upgrade.UpgradeType == tutorialTrainingUpgradeType
            && level == 0
            && !_tutorialTrainingPurchaseDone)
        {
            return tutorialTrainingPrice;
        }

        var priceLevel = upgrade.UpgradeType == tutorialTrainingUpgradeType && _tutorialTrainingPurchaseDone
            ? Mathf.Max(0, level - 1)
            : level;
        return upgrade.GetCostForLevel(priceLevel);
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
}
