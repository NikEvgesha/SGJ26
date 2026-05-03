using System;
using System.Collections;
using LittlePlanet.HybridTerraform;
using LittlePlanet.PlanetSystem;
using LittlePlanet.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(-200)]
public sealed class TutorialManager : MonoBehaviour
{
    private enum TutorialStep
    {
        IntroFly = 0,
        CollectTenScience = 1,
        BuyFirstUpgrade = 2,
        CloseUpgradeAndCollectHundred = 3,
        OpenBuildAndSelectAirGenerator = 4,
        PlaceAirGenerator = 5,
        ReachWaterFivePercent = 6,
        WaitForFirstVolcanoEvent = 7,
        ExplainCataclysmUi = 8,
        Final = 9
    }

    [Header("General")]
    [SerializeField] private bool enableTutorial = true;
    [SerializeField] private bool hideObjectivePanelOnComplete = true;
    [SerializeField, Min(0f)] private float finalMessageDurationSeconds = 8f;

    [Header("Targets")]
    [SerializeField, Min(0)] private int starterScienceTarget = 10;
    [SerializeField, Min(0)] private int postUpgradeScienceTarget = 100;
    [SerializeField, Min(0f)] private float tutorialStartTemperature = 50f;
    [SerializeField, Min(0f)] private float tutorialStartAtmosphere = 0f;
    [SerializeField, Min(0f)] private float targetWaterPercent = 5f;
    [SerializeField, Min(1)] private int airGeneratorHotkeyNumber = 4;
    [SerializeField, Min(0)] private int airGeneratorSlotIndex = 3;
    [SerializeField] private UpgradeType requiredUpgradeType = UpgradeType.FlyPower;
    [SerializeField] private EventType firstForcedEventType = EventType.Volcano;

    [Header("References")]
    [SerializeField] private Planet planet;
    [SerializeField] private PlanetFlyTerraformSkill flySkill;
    [SerializeField] private UpgradePanel upgradePanel;
    [SerializeField] private BuildPanel buildPanel;
    [SerializeField] private RandomEventsManager randomEventsManager;
    [SerializeField] private WindowManager windowManager;
    [SerializeField] private SettingsPanel settingsPanel;
    [SerializeField] private SoundManager soundManager;
    [SerializeField] private Canvas uiCanvas;

    [Header("UI Names")]
    [SerializeField] private string uiRootObjectName = "UI";
    [SerializeField] private string currencyObjectName = "Currency";
    [SerializeField] private string upgradeButtonObjectName = "Upgrade";
    [SerializeField] private string buildButtonObjectName = "BuildButton";
    [SerializeField] private string terraformingIndexObjectName = "TerraformingIndex";

    private TutorialStep _step = TutorialStep.IntroFly;
    private bool _isTutorialActive;
    private bool _isInitialized;
    private bool _forcedVolcanoPrepared;
    private float _finalStepEndTime;
    private int _scienceCheckpointAmount;
    private RandomEventDefinition _activeFirstEvent;

    private GameObject _uiRoot;
    private GameObject _currencyRoot;
    private GameObject _upgradeButtonRoot;
    private GameObject _buildButtonRoot;
    private GameObject _terraformingIndexRoot;
    private Button _flyActivateButton;

    private TutorialObjectivePanel _objectivePanel;

    private RectTransform _primaryArrow;
    private TMP_Text _primaryArrowText;
    private RectTransform _secondaryArrow;
    private TMP_Text _secondaryArrowText;
    private RectTransform _primaryUiTarget;
    private RectTransform _secondaryUiTarget;
    private Func<Vector3?> _primaryWorldTarget;
    private Func<Vector3?> _secondaryWorldTarget;
    private Vector2 _primaryOffset;
    private Vector2 _secondaryOffset;
    private bool _isPrimaryArrowVisible;
    private bool _isSecondaryArrowVisible;

    private Camera UiCameraForCanvas => uiCanvas != null && uiCanvas.renderMode != RenderMode.ScreenSpaceOverlay
        ? uiCanvas.worldCamera
        : null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstanceExists()
    {
        if (FindFirstObjectByType<TutorialManager>(FindObjectsInactive.Include) != null)
        {
            return;
        }

        var managerObject = new GameObject(nameof(TutorialManager));
        managerObject.AddComponent<TutorialManager>();
    }

    private void Awake()
    {
        ResolveReferences();
        EnsureTutorialUi();

        if (!enableTutorial || TutorialSave.IsCompleted())
        {
            _isTutorialActive = false;
            ApplyUnlockedState();
            return;
        }

        randomEventsManager?.StopEvents();
        PrepareInitialUiState();
    }

    private IEnumerator Start()
    {
        if (!enableTutorial || TutorialSave.IsCompleted())
        {
            if (TutorialSave.IsCompleted())
            {
                randomEventsManager?.StartEvents();
            }

            yield break;
        }

        yield return null;
        ResolveReferences();
        yield return WaitForStartupReady();

        if (planet == null || flySkill == null || upgradePanel == null || buildPanel == null || randomEventsManager == null)
        {
            yield break;
        }

        var savedStep = TutorialSave.LoadStepIndex();
        _step = (TutorialStep)Mathf.Clamp(savedStep, 0, (int)TutorialStep.Final);
        _isTutorialActive = true;

        if (savedStep <= (int)TutorialStep.IntroFly)
        {
            planet.Currency.SetAmount(0);
            ApplyTutorialPlanetBaseline();
        }

        EnterStep(_step, isStepChange: true);
        _isInitialized = true;
    }

    private void OnEnable()
    {
        ResolveReferences();
        SubscribeEvents();
    }

    private void OnDisable()
    {
        UnsubscribeEvents();
    }

    private void Update()
    {
        if (!_isInitialized || !_isTutorialActive)
        {
            return;
        }

        RefreshStepArrows();
        UpdateArrowPositions();
        EvaluateStepCompletion();
    }

    private void SubscribeEvents()
    {
        if (flySkill != null)
        {
            flySkill.SkillActivated -= HandleSkillActivated;
            flySkill.SkillActivated += HandleSkillActivated;
        }

        if (upgradePanel != null)
        {
            upgradePanel.UpgradePurchased -= HandleUpgradePurchased;
            upgradePanel.UpgradePurchased += HandleUpgradePurchased;
            upgradePanel.WindowStateChanged -= HandleUpgradeWindowStateChanged;
            upgradePanel.WindowStateChanged += HandleUpgradeWindowStateChanged;
        }

        if (buildPanel != null)
        {
            buildPanel.BuildingSelected -= HandleBuildingSelected;
            buildPanel.BuildingSelected += HandleBuildingSelected;
            buildPanel.BuildingPlaced -= HandleBuildingPlaced;
            buildPanel.BuildingPlaced += HandleBuildingPlaced;
        }

        if (randomEventsManager != null)
        {
            randomEventsManager.EventWarningStarted -= HandleEventWarningStarted;
            randomEventsManager.EventWarningStarted += HandleEventWarningStarted;
            randomEventsManager.EventResolved -= HandleEventResolved;
            randomEventsManager.EventResolved += HandleEventResolved;
        }

        if (planet != null)
        {
            planet.PlanetGenerated -= HandlePlanetGenerated;
            planet.PlanetGenerated += HandlePlanetGenerated;
        }
    }

    private void UnsubscribeEvents()
    {
        if (flySkill != null)
        {
            flySkill.SkillActivated -= HandleSkillActivated;
        }

        if (upgradePanel != null)
        {
            upgradePanel.UpgradePurchased -= HandleUpgradePurchased;
            upgradePanel.WindowStateChanged -= HandleUpgradeWindowStateChanged;
        }

        if (buildPanel != null)
        {
            buildPanel.BuildingSelected -= HandleBuildingSelected;
            buildPanel.BuildingPlaced -= HandleBuildingPlaced;
        }

        if (randomEventsManager != null)
        {
            randomEventsManager.EventWarningStarted -= HandleEventWarningStarted;
            randomEventsManager.EventResolved -= HandleEventResolved;
        }

        if (planet != null)
        {
            planet.PlanetGenerated -= HandlePlanetGenerated;
        }
    }

    private IEnumerator WaitForStartupReady()
    {
        if (soundManager == null)
        {
            soundManager = FindFirstObjectByType<SoundManager>(FindObjectsInactive.Include);
        }

        if (soundManager == null)
        {
            yield break;
        }

        while (!soundManager.IsStartupReady)
        {
            yield return null;
        }
    }

    private void ResolveReferences()
    {
        planet ??= FindFirstObjectByType<Planet>(FindObjectsInactive.Include);
        flySkill ??= FindFirstObjectByType<PlanetFlyTerraformSkill>(FindObjectsInactive.Include);
        upgradePanel ??= FindFirstObjectByType<UpgradePanel>(FindObjectsInactive.Include);
        buildPanel ??= FindFirstObjectByType<BuildPanel>(FindObjectsInactive.Include);
        randomEventsManager ??= FindFirstObjectByType<RandomEventsManager>(FindObjectsInactive.Include);
        windowManager ??= WindowManager.Instance;
        settingsPanel ??= FindFirstObjectByType<SettingsPanel>(FindObjectsInactive.Include);

        if (uiCanvas == null)
        {
            var uiRoot = GameObject.Find(uiRootObjectName);
            if (uiRoot != null)
            {
                uiCanvas = uiRoot.GetComponent<Canvas>();
            }

            uiCanvas ??= FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
        }

        _uiRoot = string.IsNullOrWhiteSpace(uiRootObjectName)
            ? null
            : GameObject.Find(uiRootObjectName);
        _currencyRoot = string.IsNullOrWhiteSpace(currencyObjectName)
            ? null
            : GameObject.Find(currencyObjectName);
        _upgradeButtonRoot = string.IsNullOrWhiteSpace(upgradeButtonObjectName)
            ? null
            : GameObject.Find(upgradeButtonObjectName);
        _buildButtonRoot = string.IsNullOrWhiteSpace(buildButtonObjectName)
            ? null
            : GameObject.Find(buildButtonObjectName);
        _terraformingIndexRoot = string.IsNullOrWhiteSpace(terraformingIndexObjectName)
            ? null
            : GameObject.Find(terraformingIndexObjectName);
        _flyActivateButton = flySkill != null ? flySkill.ActivateButton : null;
    }

    private void EnsureTutorialUi()
    {
        if (_objectivePanel == null && _uiRoot != null)
        {
            _objectivePanel = TutorialObjectivePanel.CreateOrFind(_uiRoot.transform);
        }

        if (uiCanvas == null)
        {
            return;
        }

        if (_primaryArrow == null)
        {
            (_primaryArrow, _primaryArrowText) = CreateArrow("TutorialArrowPrimary");
        }

        if (_secondaryArrow == null)
        {
            (_secondaryArrow, _secondaryArrowText) = CreateArrow("TutorialArrowSecondary");
        }

        HideArrows();
    }

    private (RectTransform, TMP_Text) CreateArrow(string arrowName)
    {
        var root = new GameObject(arrowName, typeof(RectTransform));
        var rect = root.GetComponent<RectTransform>();
        rect.SetParent(uiCanvas.transform, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(40f, 40f);

        var textObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        var textRect = textObject.GetComponent<RectTransform>();
        textRect.SetParent(rect, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        var text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = "v";
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(1f, 0.93f, 0.3f, 1f);
        text.fontSize = 40f;
        var anyText = FindFirstObjectByType<TextMeshProUGUI>(FindObjectsInactive.Include);
        if (anyText != null && anyText.font != null)
        {
            text.font = anyText.font;
        }

        rect.gameObject.SetActive(false);
        return (rect, text);
    }

    private void PrepareInitialUiState()
    {
        EnsureTutorialUi();
        _objectivePanel?.SetVisible(true);
        SetMainUiVisibility(showCurrency: false, showUpgradeButton: false, showBuildButton: false, showTerraforming: false);
        HideArrows();
        settingsPanel?.SetWindowOpen(false);
        windowManager?.CloseAll();
    }

    private void EnterStep(TutorialStep step, bool isStepChange)
    {
        _step = step;
        _objectivePanel?.SetVisible(true);
        if (isStepChange)
        {
            _objectivePanel?.ExpandForStepChange();
        }

        switch (_step)
        {
            case TutorialStep.IntroFly:
                randomEventsManager?.StopEvents();
                ApplyTutorialPlanetBaseline();
                SetMainUiVisibility(showCurrency: false, showUpgradeButton: false, showBuildButton: false, showTerraforming: false);
                flySkill?.SetTutorialHotkeyAllowed(true);
                upgradePanel?.SetHotkeyEnabled(false);
                upgradePanel?.SetTutorialTrainingPriceEnabled(true);
                upgradePanel?.SetTutorialAllowedUpgrade(null);
                buildPanel?.SetHotkeysEnabled(false, false);
                buildPanel?.ClearTutorialRestrictions();
                _objectivePanel?.SetObjective("The planet is in metamorphosis. Green the surface to earn science points.\nPress [F] or Fly to start flight.");
                break;
            case TutorialStep.CollectTenScience:
                SetMainUiVisibility(showCurrency: true, showUpgradeButton: false, showBuildButton: false, showTerraforming: false);
                flySkill?.SetTutorialHotkeyAllowed(true);
                upgradePanel?.SetHotkeyEnabled(false);
                buildPanel?.SetHotkeysEnabled(false, false);
                _objectivePanel?.SetObjective("Fly near the surface and collect 10 science points for the first upgrade.");
                break;
            case TutorialStep.BuyFirstUpgrade:
                SetMainUiVisibility(showCurrency: true, showUpgradeButton: true, showBuildButton: false, showTerraforming: false);
                upgradePanel?.SetHotkeyEnabled(true);
                upgradePanel?.SetTutorialAllowedUpgrade(requiredUpgradeType);
                upgradePanel?.SetTutorialTrainingPriceEnabled(true);
                buildPanel?.SetHotkeysEnabled(false, false);
                _objectivePanel?.SetObjective("Open upgrades with [U] and buy Greening Power.");
                break;
            case TutorialStep.CloseUpgradeAndCollectHundred:
                SetMainUiVisibility(showCurrency: true, showUpgradeButton: true, showBuildButton: false, showTerraforming: false);
                upgradePanel?.SetHotkeyEnabled(true);
                upgradePanel?.SetTutorialAllowedUpgrade(null);
                upgradePanel?.SetTutorialTrainingPriceEnabled(false);
                buildPanel?.SetHotkeysEnabled(false, false);
                _scienceCheckpointAmount = planet != null ? planet.Currency.Amount : 0;
                _objectivePanel?.SetObjective("Great. Close the upgrade window and collect 100 more science points.");
                break;
            case TutorialStep.OpenBuildAndSelectAirGenerator:
                SetMainUiVisibility(showCurrency: true, showUpgradeButton: true, showBuildButton: true, showTerraforming: false);
                upgradePanel?.SetHotkeyEnabled(true);
                buildPanel?.SetHotkeysEnabled(true, true);
                buildPanel?.SetTutorialAllowedBuildingIndex(airGeneratorSlotIndex);
                buildPanel?.SetTutorialAllowedNumberHotkey(airGeneratorHotkeyNumber);
                _objectivePanel?.SetObjective("Open building menu with [B] and select Air Generator with [4].");
                break;
            case TutorialStep.PlaceAirGenerator:
                SetMainUiVisibility(showCurrency: true, showUpgradeButton: true, showBuildButton: true, showTerraforming: false);
                buildPanel?.SetHotkeysEnabled(true, true);
                buildPanel?.SetTutorialAllowedBuildingIndex(airGeneratorSlotIndex);
                buildPanel?.SetTutorialAllowedNumberHotkey(airGeneratorHotkeyNumber);
                _objectivePanel?.SetObjective("Place the selected Air Generator on a free non-mountain tile.");
                break;
            case TutorialStep.ReachWaterFivePercent:
                SetMainUiVisibility(showCurrency: true, showUpgradeButton: true, showBuildButton: true, showTerraforming: true);
                buildPanel?.SetHotkeysEnabled(true, true);
                buildPanel?.ClearTutorialRestrictions();
                _objectivePanel?.SetObjective("Watch terraforming index and reach 5% water level.");
                break;
            case TutorialStep.WaitForFirstVolcanoEvent:
                SetMainUiVisibility(showCurrency: true, showUpgradeButton: true, showBuildButton: true, showTerraforming: true);
                buildPanel?.SetHotkeysEnabled(true, true);
                buildPanel?.ClearTutorialRestrictions();
                randomEventsManager?.StopEvents();
                if (!_forcedVolcanoPrepared && randomEventsManager != null)
                {
                    randomEventsManager.ForceNextEvent(firstForcedEventType);
                    _forcedVolcanoPrepared = true;
                }

                randomEventsManager?.StartEvents();
                _objectivePanel?.SetObjective("Nice. Water helps greening. Next goal is 100%, but first get ready for the first cataclysm.");
                break;
            case TutorialStep.ExplainCataclysmUi:
                SetMainUiVisibility(showCurrency: true, showUpgradeButton: true, showBuildButton: true, showTerraforming: true);
                _objectivePanel?.SetObjective("Cataclysms can destroy buildings and change temperature/atmosphere.\nCheck event icons and use GoToArea when needed.");
                break;
            case TutorialStep.Final:
                SetMainUiVisibility(showCurrency: true, showUpgradeButton: true, showBuildButton: true, showTerraforming: true);
                _objectivePanel?.SetObjective("You now know the basics. Final goal: fill the planet with greenery and water to 100%.");
                _finalStepEndTime = Time.unscaledTime + Mathf.Max(0f, finalMessageDurationSeconds);
                break;
        }

        RefreshStepArrows();
    }

    private void EvaluateStepCompletion()
    {
        switch (_step)
        {
            case TutorialStep.CollectTenScience:
                if (planet != null && planet.Currency.Amount >= starterScienceTarget)
                {
                    AdvanceStep();
                }
                break;
            case TutorialStep.CloseUpgradeAndCollectHundred:
                if (planet != null
                    && upgradePanel != null
                    && !upgradePanel.IsWindowOpen
                    && planet.Currency.Amount >= _scienceCheckpointAmount + postUpgradeScienceTarget)
                {
                    AdvanceStep();
                }
                break;
            case TutorialStep.ReachWaterFivePercent:
                if (planet != null && planet.GetOceanIndexPercent() >= targetWaterPercent)
                {
                    AdvanceStep();
                }
                break;
            case TutorialStep.Final:
                if (Time.unscaledTime >= _finalStepEndTime)
                {
                    CompleteTutorial();
                }
                break;
        }
    }

    private void AdvanceStep()
    {
        var nextIndex = (int)_step + 1;
        if (nextIndex > (int)TutorialStep.Final)
        {
            CompleteTutorial();
            return;
        }

        TutorialSave.SaveStepIndex(nextIndex);
        EnterStep((TutorialStep)nextIndex, isStepChange: true);
    }

    private void CompleteTutorial()
    {
        _isTutorialActive = false;
        ApplyUnlockedState();
        HideArrows();
        randomEventsManager?.StartEvents();
        TutorialSave.MarkCompleted();
        TutorialSave.SaveStepIndex((int)TutorialStep.Final + 1);
        if (hideObjectivePanelOnComplete)
        {
            _objectivePanel?.SetVisible(false);
        }
    }

    private void ApplyUnlockedState()
    {
        flySkill?.SetTutorialHotkeyAllowed(true);
        upgradePanel?.SetHotkeyEnabled(true);
        upgradePanel?.SetTutorialAllowedUpgrade(null);
        upgradePanel?.SetTutorialTrainingPriceEnabled(false);
        buildPanel?.SetHotkeysEnabled(true, true);
        buildPanel?.ClearTutorialRestrictions();
        SetMainUiVisibility(showCurrency: true, showUpgradeButton: true, showBuildButton: true, showTerraforming: true);
    }

    private void ApplyTutorialPlanetBaseline()
    {
        planet?.SetTutorialStartConditions(tutorialStartTemperature, tutorialStartAtmosphere);
    }

    private void SetMainUiVisibility(bool showCurrency, bool showUpgradeButton, bool showBuildButton, bool showTerraforming)
    {
        SetUiRootVisible(_currencyRoot, showCurrency);
        SetUiRootVisible(_upgradeButtonRoot, showUpgradeButton);
        SetUiRootVisible(_buildButtonRoot, showBuildButton);
        SetUiRootVisible(_terraformingIndexRoot, showTerraforming);

        if (_flyActivateButton != null)
        {
            _flyActivateButton.gameObject.SetActive(true);
        }

        if (!showUpgradeButton && upgradePanel != null && upgradePanel.IsWindowOpen)
        {
            upgradePanel.SetWindowOpen(false);
        }

        if (!showBuildButton && buildPanel != null && buildPanel.IsWindowOpen)
        {
            buildPanel.SetWindowOpen(false);
        }
    }

    private static void SetUiRootVisible(GameObject target, bool isVisible)
    {
        if (target == null)
        {
            return;
        }

        target.SetActive(isVisible);
    }

    private void HandleSkillActivated()
    {
        if (!_isTutorialActive || _step != TutorialStep.IntroFly)
        {
            return;
        }

        AdvanceStep();
    }

    private void HandleUpgradePurchased(UpgradeDefinition definition, int level)
    {
        if (!_isTutorialActive || _step != TutorialStep.BuyFirstUpgrade || definition == null)
        {
            return;
        }

        if (definition.UpgradeType == requiredUpgradeType && level > 0)
        {
            AdvanceStep();
        }
    }

    private void HandleUpgradeWindowStateChanged(bool isOpen)
    {
        if (!_isTutorialActive)
        {
            return;
        }

        if (_step == TutorialStep.CloseUpgradeAndCollectHundred && !isOpen)
        {
            EvaluateStepCompletion();
        }
    }

    private void HandleBuildingSelected(Building building, int slotIndex)
    {
        if (!_isTutorialActive || _step != TutorialStep.OpenBuildAndSelectAirGenerator)
        {
            return;
        }

        if (slotIndex == airGeneratorSlotIndex)
        {
            AdvanceStep();
        }
    }

    private void HandleBuildingPlaced(Building building, Tile tile)
    {
        if (!_isTutorialActive || _step != TutorialStep.PlaceAirGenerator)
        {
            return;
        }

        AdvanceStep();
    }

    private void HandleEventWarningStarted(RandomEventDefinition eventDefinition)
    {
        if (!_isTutorialActive || eventDefinition == null)
        {
            return;
        }

        if (_step == TutorialStep.WaitForFirstVolcanoEvent && eventDefinition.EventType == firstForcedEventType)
        {
            _activeFirstEvent = eventDefinition;
            AdvanceStep();
        }
    }

    private void HandleEventResolved(RandomEventDefinition eventDefinition)
    {
        if (!_isTutorialActive || _step != TutorialStep.ExplainCataclysmUi || eventDefinition == null)
        {
            return;
        }

        var isExpectedEvent = _activeFirstEvent == null
            ? eventDefinition.EventType == firstForcedEventType
            : eventDefinition == _activeFirstEvent;
        if (!isExpectedEvent)
        {
            return;
        }

        _activeFirstEvent = null;
        AdvanceStep();
    }

    private void HandlePlanetGenerated()
    {
        if (!_isTutorialActive)
        {
            return;
        }

        if (_step <= TutorialStep.CollectTenScience)
        {
            ApplyTutorialPlanetBaseline();
        }
    }

    private void RefreshStepArrows()
    {
        HideArrows();

        switch (_step)
        {
            case TutorialStep.IntroFly:
                SetPrimaryArrowToUi(_flyActivateButton != null ? _flyActivateButton.transform as RectTransform : null, new Vector2(0f, 70f));
                break;
            case TutorialStep.BuyFirstUpgrade:
                if (upgradePanel != null && upgradePanel.IsWindowOpen)
                {
                    SetPrimaryArrowToUi(upgradePanel.GetSlotRect(requiredUpgradeType), new Vector2(0f, 65f));
                }
                else
                {
                    SetPrimaryArrowToUi(_upgradeButtonRoot != null ? _upgradeButtonRoot.transform as RectTransform : null, new Vector2(0f, 70f));
                }
                break;
            case TutorialStep.CloseUpgradeAndCollectHundred:
                if (upgradePanel != null && upgradePanel.IsWindowOpen)
                {
                    SetPrimaryArrowToUi(upgradePanel.GetCloseButtonRect(), new Vector2(0f, 55f));
                }
                break;
            case TutorialStep.OpenBuildAndSelectAirGenerator:
                if (buildPanel != null && buildPanel.IsWindowOpen)
                {
                    SetPrimaryArrowToUi(buildPanel.GetSlotRect(airGeneratorSlotIndex), new Vector2(0f, 65f));
                }
                else
                {
                    SetPrimaryArrowToUi(buildPanel != null ? buildPanel.GetBuildButtonRect() : null, new Vector2(0f, 70f));
                }
                break;
            case TutorialStep.PlaceAirGenerator:
                SetPrimaryArrowToWorld(GetNearestBuildTileWorldPoint, new Vector2(0f, 55f));
                break;
            case TutorialStep.ExplainCataclysmUi:
                if (randomEventsManager != null)
                {
                    SetPrimaryArrowToUi(randomEventsManager.GetCautionRect(), new Vector2(0f, 70f));
                    SetSecondaryArrowToUi(randomEventsManager.GetGoToAreaButtonRect(), new Vector2(0f, 55f));
                }
                break;
        }
    }

    private Vector3? GetNearestBuildTileWorldPoint()
    {
        if (buildPanel == null || planet == null)
        {
            return null;
        }

        var tile = buildPanel.FindNearestBuildableTileToCamera();
        if (tile == null)
        {
            return null;
        }

        return planet.GetTileWorldSurfaceCenter(tile, 0.35f);
    }

    private void SetPrimaryArrowToUi(RectTransform target, Vector2 offset)
    {
        _primaryUiTarget = target;
        _primaryWorldTarget = null;
        _primaryOffset = offset;
        _isPrimaryArrowVisible = target != null;
        if (_primaryArrow != null)
        {
            _primaryArrow.gameObject.SetActive(_isPrimaryArrowVisible);
        }
    }

    private void SetSecondaryArrowToUi(RectTransform target, Vector2 offset)
    {
        _secondaryUiTarget = target;
        _secondaryOffset = offset;
        _isSecondaryArrowVisible = target != null;
        if (_secondaryArrow != null)
        {
            _secondaryArrow.gameObject.SetActive(_isSecondaryArrowVisible);
        }
    }

    private void SetPrimaryArrowToWorld(Func<Vector3?> worldTargetProvider, Vector2 offset)
    {
        _primaryWorldTarget = worldTargetProvider;
        _primaryUiTarget = null;
        _primaryOffset = offset;
        _isPrimaryArrowVisible = worldTargetProvider != null;
        if (_primaryArrow != null)
        {
            _primaryArrow.gameObject.SetActive(_isPrimaryArrowVisible);
        }
    }

    private void HideArrows()
    {
        _primaryUiTarget = null;
        _secondaryUiTarget = null;
        _primaryWorldTarget = null;
        _secondaryWorldTarget = null;
        _isPrimaryArrowVisible = false;
        _isSecondaryArrowVisible = false;
        if (_primaryArrow != null)
        {
            _primaryArrow.gameObject.SetActive(false);
        }

        if (_secondaryArrow != null)
        {
            _secondaryArrow.gameObject.SetActive(false);
        }
    }

    private void UpdateArrowPositions()
    {
        UpdateArrowPosition(
            _primaryArrow,
            _primaryUiTarget,
            _primaryWorldTarget,
            _primaryOffset,
            _isPrimaryArrowVisible);

        UpdateArrowPosition(
            _secondaryArrow,
            _secondaryUiTarget,
            _secondaryWorldTarget,
            _secondaryOffset,
            _isSecondaryArrowVisible);
    }

    private void UpdateArrowPosition(
        RectTransform arrow,
        RectTransform uiTarget,
        Func<Vector3?> worldTarget,
        Vector2 offset,
        bool isVisible)
    {
        if (arrow == null || uiCanvas == null)
        {
            return;
        }

        if (!isVisible)
        {
            arrow.gameObject.SetActive(false);
            return;
        }

        Vector3 screenPoint;
        if (uiTarget != null)
        {
            if (!uiTarget.gameObject.activeInHierarchy)
            {
                arrow.gameObject.SetActive(false);
                return;
            }

            screenPoint = RectTransformUtility.WorldToScreenPoint(UiCameraForCanvas, uiTarget.position);
        }
        else if (worldTarget != null)
        {
            var worldPoint = worldTarget.Invoke();
            if (!worldPoint.HasValue || Camera.main == null)
            {
                arrow.gameObject.SetActive(false);
                return;
            }

            var cameraToUse = Camera.main;
            var cameraSpace = cameraToUse.WorldToViewportPoint(worldPoint.Value);
            if (cameraSpace.z <= 0f)
            {
                arrow.gameObject.SetActive(false);
                return;
            }

            screenPoint = cameraToUse.WorldToScreenPoint(worldPoint.Value);
        }
        else
        {
            arrow.gameObject.SetActive(false);
            return;
        }

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                uiCanvas.transform as RectTransform,
                screenPoint,
                UiCameraForCanvas,
                out var localPoint))
        {
            arrow.anchoredPosition = localPoint + offset;
            if (!arrow.gameObject.activeSelf)
            {
                arrow.gameObject.SetActive(true);
            }
        }
        else
        {
            arrow.gameObject.SetActive(false);
        }
    }
}
