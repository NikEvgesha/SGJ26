using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UpgradeSlotUI : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private TMP_Text valueText;
    [SerializeField] private Button button;

    private UpgradeDefinition _upgrade;
    private UpgradePanel _owner;
    private int _level;
    private bool _canBuy;

    public UpgradeDefinition Upgrade => _upgrade;
    public int Level => _level;

    private void Awake()
    {
        ResolveReferences();
    }

    public void Initialize(UpgradeDefinition upgrade, int level = 0)
    {
        Initialize(null, upgrade, level);
    }

    public void Initialize(UpgradePanel owner, UpgradeDefinition upgrade, int level = 0)
    {
        _owner = owner;
        _upgrade = upgrade;
        _level = Mathf.Max(0, level);
        ResolveReferences();
        RefreshStaticInfo();
    }

    public void SetLevel(int level)
    {
        _level = Mathf.Max(0, level);
        RefreshLevel();
    }

    public void SetState(int level, int price, string currentValue, string nextValue, bool canBuy, bool isMaxLevel)
    {
        _level = Mathf.Max(0, level);
        _canBuy = canBuy;
        ResolveReferences();
        RefreshStaticInfo();
        RefreshLevel();

        if (priceText != null)
        {
            priceText.text = isMaxLevel ? "MAX" : price.ToString();
        }

        if (valueText != null)
        {
            valueText.text = isMaxLevel ? currentValue : $"{currentValue} -> {nextValue}";
        }

        if (button != null)
        {
            button.interactable = canBuy;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!_canBuy)
        {
            return;
        }

        _owner?.BuyUpgrade(this);
    }

    private void RefreshStaticInfo()
    {
        if (iconImage != null)
        {
            iconImage.sprite = _upgrade != null ? _upgrade.Icon : null;
            iconImage.enabled = iconImage.sprite != null;
        }

        if (nameText != null)
        {
            nameText.text = _upgrade != null ? _upgrade.UpgradeName : string.Empty;
        }

        if (descriptionText != null)
        {
            descriptionText.text = _upgrade != null ? _upgrade.Description : string.Empty;
        }

        RefreshLevel();
    }

    private void RefreshLevel()
    {
        if (levelText != null)
        {
            var maxLevel = _upgrade != null ? _upgrade.MaxLevel : 0;
            levelText.text = $"{Mathf.Clamp(_level, 0, maxLevel)} / {maxLevel}";
        }
    }

    private void ResolveReferences()
    {
        iconImage ??= FindImage("Icon");
        levelText ??= FindText("Lvl");
        nameText ??= FindText("name");
        descriptionText ??= FindText("description");
        priceText ??= FindText("price");
        valueText ??= FindText("value");
        button ??= GetComponent<Button>();
    }

    private TMP_Text FindText(string childName)
    {
        var child = FindChildByName(transform, childName);
        return child != null ? child.GetComponent<TMP_Text>() : null;
    }

    private Image FindImage(string childName)
    {
        var child = FindChildByName(transform, childName);
        return child != null ? child.GetComponent<Image>() : GetComponentInChildren<Image>(true);
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
