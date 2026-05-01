using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UpgradeSlotUI : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text levelText;

    private UpgradeDefinition _upgrade;
    private UpgradePanel _owner;
    private int _level;

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
        Refresh();
    }

    public void SetLevel(int level)
    {
        _level = Mathf.Max(0, level);
        Refresh();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        _owner?.SelectUpgrade(this);
    }

    private void Refresh()
    {
        if (iconImage != null)
        {
            iconImage.sprite = _upgrade != null ? _upgrade.Icon : null;
            iconImage.enabled = iconImage.sprite != null;
        }

        if (levelText != null)
        {
            var maxLevel = _upgrade != null ? _upgrade.MaxLevel : 0;
            levelText.text = $"{Mathf.Clamp(_level, 0, maxLevel)} / {maxLevel}";
        }
    }

    private void ResolveReferences()
    {
        if (iconImage == null)
        {
            var icon = transform.Find("Icon");
            if (icon != null)
            {
                iconImage = icon.GetComponent<Image>();
            }

            if (iconImage == null)
            {
                iconImage = GetComponentInChildren<Image>(true);
            }
        }

        if (levelText == null)
        {
            var level = transform.Find("Lvl");
            if (level != null)
            {
                levelText = level.GetComponent<TMP_Text>();
            }

            if (levelText == null)
            {
                levelText = GetComponentInChildren<TMP_Text>(true);
            }
        }
    }
}
