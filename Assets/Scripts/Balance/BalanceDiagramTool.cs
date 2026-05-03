using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LittlePlanet.Balance
{
    [ExecuteAlways]
    public sealed class BalanceDiagramTool : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private List<BalanceUpgradeCurve> curves = new();
        [SerializeField] private int selectedCurveIndex;

        [Header("Layout")]
        [SerializeField] private bool autoArrangeLayout = true;
        [SerializeField] private bool logLayoutDebug;

        [Header("Simulation")]
        [SerializeField, Min(10f)] private float simulationDurationSeconds = 300f;
        [SerializeField, Min(0f)] private float baseTerraformPerSecond = 1f;
        [SerializeField, Min(0f)] private float baseScreenReward = 25f;

        [Header("Controls")]
        [SerializeField] private TMP_Dropdown curveDropdown;
        [SerializeField] private TMP_InputField baseCostInput;
        [SerializeField] private TMP_InputField multiplierInput;
        [SerializeField] private TMP_InputField maxLevelInput;
        [SerializeField] private TMP_InputField valuePerLevelInput;
        [SerializeField] private TMP_InputField durationInput;
        [SerializeField] private TMP_InputField incomeInput;
        [SerializeField] private TMP_InputField screenRewardInput;
        [SerializeField] private Toggle includeInTotalToggle;
        [SerializeField] private Toggle logScaleToggle;
        [SerializeField] private Button pullButton;
        [SerializeField] private Button applyButton;

        [Header("Output")]
        [SerializeField] private BalanceChartGraphic chart;
        [SerializeField] private TMP_Text summaryText;
        [SerializeField] private TMP_Text tableText;

        private readonly List<float> _costSeries = new();
        private readonly List<float> _valueSeries = new();
        private readonly List<float> _purchaseMarkers01 = new();
        private readonly List<int> _dropdownCurveIndices = new();
        private bool _isRefreshingUi;

        private BalanceUpgradeCurve SelectedCurve =>
            curves.Count == 0 ? null : curves[Mathf.Clamp(selectedCurveIndex, 0, curves.Count - 1)];

        private void Awake()
        {
            AutoBindSceneReferences();
            ApplyDefaultLayoutIfNeeded();
            BindControls();
            RefreshDropdown();
            RefreshUiFromCurve();
            Recalculate();
        }

        private void OnEnable()
        {
            AutoBindSceneReferences();
            ApplyDefaultLayoutIfNeeded();
            BindControls();
            RefreshDropdown();
            RefreshUiFromCurve();
            Recalculate();
        }

        private void OnValidate()
        {
            selectedCurveIndex = Mathf.Clamp(selectedCurveIndex, 0, Mathf.Max(0, curves.Count - 1));
            ApplyDefaultLayoutIfNeeded();
        }

        public void SetCurves(List<BalanceUpgradeCurve> nextCurves)
        {
            curves = nextCurves ?? new List<BalanceUpgradeCurve>();
            selectedCurveIndex = Mathf.Clamp(selectedCurveIndex, 0, Mathf.Max(0, curves.Count - 1));
            RefreshDropdown();
            RefreshUiFromCurve();
            Recalculate();
        }

        public void SelectCurve(int index)
        {
            if (_dropdownCurveIndices.Count > 0)
            {
                selectedCurveIndex = _dropdownCurveIndices[Mathf.Clamp(index, 0, _dropdownCurveIndices.Count - 1)];
            }
            else
            {
                selectedCurveIndex = Mathf.Clamp(index, 0, Mathf.Max(0, curves.Count - 1));
            }

            RefreshUiFromCurve();
            Recalculate();
        }

        public void PullSelectedFromAsset()
        {
            var curve = SelectedCurve;
            if (curve == null)
            {
                return;
            }

            curve.PullFromTarget();
            RefreshDropdown();
            RefreshUiFromCurve();
            Recalculate();
        }

        public void ApplySelectedToAsset()
        {
            ReadUiIntoCurve();
            var curve = SelectedCurve;
            if (curve == null || curve.Target == null)
            {
                return;
            }

            curve.ApplyToTarget();
#if UNITY_EDITOR
            EditorUtility.SetDirty(curve.Target);
            AssetDatabase.SaveAssets();
#endif
            Recalculate();
        }

        public void Recalculate()
        {
            ReadUiIntoCurve();
            var curve = SelectedCurve;
            _costSeries.Clear();
            _valueSeries.Clear();
            _purchaseMarkers01.Clear();

            if (curve == null)
            {
                SetText(summaryText, "No upgrade curve selected.");
                SetText(tableText, string.Empty);
                chart?.SetData(_costSeries, _valueSeries, logScaleToggle != null && logScaleToggle.isOn);
                return;
            }

            var levels = new int[curves.Count];
            var totalSpent = 0;
            var money = 0f;
            var stepSeconds = Mathf.Max(1f, simulationDurationSeconds / 80f);
            var table = "Time | Bought | Cost | Money After | Income/s After | Payback\n";
            var purchaseCount = 0;
            var firstPurchaseTime = -1f;
            var lastPurchaseTime = -1f;

            for (var time = 0f; time <= simulationDurationSeconds + 0.001f; time += stepSeconds)
            {
                var income = GetIncomePerSecond(levels);
                money += income * stepSeconds;

                while (TryFindBestAffordableUpgrade(levels, money, out var bestIndex, out var bestCost, out var incomeGain))
                {
                    var previousIncome = income;
                    money -= bestCost;
                    totalSpent += bestCost;
                    levels[bestIndex]++;
                    income = GetIncomePerSecond(levels);
                    purchaseCount++;
                    firstPurchaseTime = firstPurchaseTime < 0f ? time : firstPurchaseTime;
                    lastPurchaseTime = time;
                    _purchaseMarkers01.Add(simulationDurationSeconds <= 0f ? 0f : time / simulationDurationSeconds);

                    var payback = incomeGain <= 0.0001f ? "n/a" : $"{bestCost / incomeGain:0}s";
                    table += $"{time:0}s | {curves[bestIndex].Label} L{levels[bestIndex]} | {bestCost} | {money:0} | {income:0.##} | {payback}\n";
                }

                _costSeries.Add(money);
                _valueSeries.Add(income);
            }

            var mode = logScaleToggle != null && logScaleToggle.isOn ? "Log" : "Linear";
            var includedCount = CountIncludedCurves();
            var firstPurchase = firstPurchaseTime < 0f ? "none" : $"{firstPurchaseTime:0}s";
            var lastPurchase = lastPurchaseTime < 0f ? "none" : $"{lastPurchaseTime:0}s";
            var useLogScale = logScaleToggle != null && logScaleToggle.isOn;
            var maxMoney = GetMaxValue(_costSeries);
            var maxIncome = GetMaxValue(_valueSeries);
            SetText(summaryText,
                $"{curve.Label}\n" +
                $"Chart: cyan=money, green=income/s. Fly is continuous, no cooldown in this sim.\n" +
                $"Yellow markers=purchases. Total sim uses {includedCount} included upgrades.\n" +
                $"Rule: buy affordable upgrade with highest income gain.\n" +
                $"Duration: {simulationDurationSeconds:0}s  Base terraform/s: {baseTerraformPerSecond:0.###}  Screen reward: {baseScreenReward:0.###}  Chart: {mode}\n" +
                $"Purchases: {purchaseCount}  First: {firstPurchase}  Last: {lastPurchase}\n" +
                $"Money left: {money:0}  Spent: {totalSpent}  Final income/s: {GetIncomePerSecond(levels):0.##}");
            SetText(tableText, table);
            if (chart != null && chart.transform.parent is RectTransform chartPanel)
            {
                EnsureChartLabels(chartPanel, maxMoney, maxIncome, useLogScale);
            }

            chart?.SetData(_costSeries, _valueSeries, _purchaseMarkers01, useLogScale);
            LogDebug($"Recalculated. chart={(chart != null ? chart.name : "null")}, points={_costSeries.Count}, included={includedCount}");
        }

        private void BindControls()
        {
            if (curveDropdown != null)
            {
                EnsureDropdownTemplate(curveDropdown);
                curveDropdown.onValueChanged.RemoveListener(SelectCurve);
                curveDropdown.onValueChanged.AddListener(SelectCurve);
            }

            BindInput(baseCostInput);
            BindInput(multiplierInput);
            BindInput(maxLevelInput);
            BindInput(valuePerLevelInput);
            BindInput(durationInput);
            BindInput(incomeInput);
            BindInput(screenRewardInput);

            if (includeInTotalToggle != null)
            {
                includeInTotalToggle.onValueChanged.RemoveListener(HandleToggleChanged);
                includeInTotalToggle.onValueChanged.AddListener(HandleToggleChanged);
            }

            if (logScaleToggle != null)
            {
                logScaleToggle.onValueChanged.RemoveListener(HandleToggleChanged);
                logScaleToggle.onValueChanged.AddListener(HandleToggleChanged);
            }

            if (pullButton != null)
            {
                pullButton.onClick.RemoveListener(PullSelectedFromAsset);
                pullButton.onClick.AddListener(PullSelectedFromAsset);
            }

            if (applyButton != null)
            {
                applyButton.onClick.RemoveListener(ApplySelectedToAsset);
                applyButton.onClick.AddListener(ApplySelectedToAsset);
            }
        }

        private bool TryFindBestAffordableUpgrade(int[] levels, float money, out int bestIndex, out int bestCost)
        {
            return TryFindBestAffordableUpgrade(levels, money, out bestIndex, out bestCost, out _);
        }

        private bool TryFindBestAffordableUpgrade(int[] levels, float money, out int bestIndex, out int bestCost, out float bestIncomeGain)
        {
            bestIndex = -1;
            bestCost = 0;
            bestIncomeGain = float.MinValue;

            for (var i = 0; i < curves.Count; i++)
            {
                var candidate = curves[i];
                if (candidate == null || !candidate.IsBalanceRelevant || !candidate.IncludeInTotal || levels[i] >= candidate.MaxLevel)
                {
                    continue;
                }

                var cost = candidate.GetCostForLevel(levels[i]);
                if (cost > money)
                {
                    continue;
                }

                var incomeGain = GetIncomeGainForUpgrade(candidate, levels);
                if (incomeGain > bestIncomeGain || Mathf.Approximately(incomeGain, bestIncomeGain) && cost < bestCost)
                {
                    bestIncomeGain = incomeGain;
                    bestIndex = i;
                    bestCost = cost;
                }
            }

            return bestIndex >= 0;
        }

        private float GetTotalPower(int[] levels)
        {
            var totalPower = 0f;
            for (var i = 0; i < curves.Count && i < levels.Length; i++)
            {
                var curve = curves[i];
                if (curve == null || !curve.IsBalanceRelevant || !curve.IncludeInTotal)
                {
                    continue;
                }

                if (curve.UpgradeType == UpgradeType.FlyPower || curve.UpgradeType == UpgradeType.FlyRadius)
                {
                    totalPower += curve.ValuePerLevel * levels[i];
                }
            }

            return totalPower;
        }

        private float GetScreenReward(int[] levels)
        {
            var reward = baseScreenReward;
            if (levels == null)
            {
                return reward;
            }

            for (var i = 0; i < curves.Count && i < levels.Length; i++)
            {
                var curve = curves[i];
                if (curve != null && curve.IsBalanceRelevant && curve.IncludeInTotal && curve.UpgradeType == UpgradeType.GreenReward)
                {
                    reward += curve.ValuePerLevel * levels[i];
                }
            }

            return Mathf.Max(0f, reward);
        }

        private float GetIncomePerSecond(int[] levels)
        {
            return baseTerraformPerSecond * (1f + Mathf.Max(0f, GetTotalPower(levels))) * GetScreenReward(levels);
        }

        private float GetIncomeGainForUpgrade(BalanceUpgradeCurve candidate, int[] levels)
        {
            if (candidate == null || levels == null)
            {
                return 0f;
            }

            var before = GetIncomePerSecond(levels);
            var index = curves.IndexOf(candidate);
            if (index < 0 || index >= levels.Length)
            {
                return 0f;
            }

            levels[index]++;
            var after = GetIncomePerSecond(levels);
            levels[index]--;
            return Mathf.Max(0f, after - before);
        }

        private int CountIncludedCurves()
        {
            var count = 0;
            for (var i = 0; i < curves.Count; i++)
            {
                var curve = curves[i];
                if (curve != null && curve.IsBalanceRelevant && curve.IncludeInTotal)
                {
                    count++;
                }
            }

            return count;
        }

        private static float GetMaxValue(IReadOnlyList<float> values)
        {
            var max = 0f;
            if (values == null)
            {
                return max;
            }

            for (var i = 0; i < values.Count; i++)
            {
                max = Mathf.Max(max, values[i]);
            }

            return max;
        }

        private void BindInput(TMP_InputField input)
        {
            if (input == null)
            {
                return;
            }

            input.onEndEdit.RemoveListener(HandleInputChanged);
            input.onEndEdit.AddListener(HandleInputChanged);
        }

        private void HandleInputChanged(string value)
        {
            if (!_isRefreshingUi)
            {
                Recalculate();
            }
        }

        private void HandleToggleChanged(bool value)
        {
            Recalculate();
        }

        private void RefreshDropdown()
        {
            if (curveDropdown == null)
            {
                return;
            }

            _isRefreshingUi = true;
            curveDropdown.ClearOptions();
            var options = new List<string>(curves.Count);
            _dropdownCurveIndices.Clear();
            for (var i = 0; i < curves.Count; i++)
            {
                var curve = curves[i];
                if (curve == null || !curve.IsBalanceRelevant)
                {
                    continue;
                }

                options.Add(curve.Label);
                _dropdownCurveIndices.Add(i);
            }

            curveDropdown.AddOptions(options);
            if (_dropdownCurveIndices.Count > 0 && !_dropdownCurveIndices.Contains(selectedCurveIndex))
            {
                selectedCurveIndex = _dropdownCurveIndices[0];
            }

            var dropdownIndex = Mathf.Max(0, _dropdownCurveIndices.IndexOf(selectedCurveIndex));
            curveDropdown.SetValueWithoutNotify(Mathf.Clamp(dropdownIndex, 0, Mathf.Max(0, options.Count - 1)));
            _isRefreshingUi = false;
        }

        private void RefreshUiFromCurve()
        {
            var curve = SelectedCurve;
            if (curve == null)
            {
                return;
            }

            _isRefreshingUi = true;
            SetInput(baseCostInput, curve.BaseCost.ToString());
            SetInput(multiplierInput, curve.CostMultiplier.ToString("0.###"));
            SetInput(maxLevelInput, curve.MaxLevel.ToString());
            SetInput(valuePerLevelInput, curve.ValuePerLevel.ToString("0.###"));
            SetInput(durationInput, simulationDurationSeconds.ToString("0"));
            SetInput(incomeInput, baseTerraformPerSecond.ToString("0.###"));
            SetInput(screenRewardInput, baseScreenReward.ToString("0.###"));
            includeInTotalToggle?.SetIsOnWithoutNotify(curve.IncludeInTotal);
            _isRefreshingUi = false;
        }

        private void ReadUiIntoCurve()
        {
            if (_isRefreshingUi)
            {
                return;
            }

            var curve = SelectedCurve;
            if (curve == null)
            {
                return;
            }

            var baseCost = ReadInt(baseCostInput, curve.BaseCost);
            var multiplier = ReadFloat(multiplierInput, curve.CostMultiplier);
            var maxLevel = ReadInt(maxLevelInput, curve.MaxLevel);
            var valuePerLevel = ReadFloat(valuePerLevelInput, curve.ValuePerLevel);
            curve.SetValues(baseCost, multiplier, maxLevel, valuePerLevel);
            if (includeInTotalToggle != null)
            {
                curve.SetIncludedInTotal(includeInTotalToggle.isOn);
            }

            simulationDurationSeconds = Mathf.Max(10f, ReadFloat(durationInput, simulationDurationSeconds));
            baseTerraformPerSecond = Mathf.Max(0f, ReadFloat(incomeInput, baseTerraformPerSecond));
            baseScreenReward = Mathf.Max(0f, ReadFloat(screenRewardInput, baseScreenReward));
        }

        private static int ReadInt(TMP_InputField input, int fallback)
        {
            return input != null && int.TryParse(input.text, out var value) ? value : fallback;
        }

        private static float ReadFloat(TMP_InputField input, float fallback)
        {
            return input != null && float.TryParse(input.text, out var value) ? value : fallback;
        }

        private static void SetInput(TMP_InputField input, string value)
        {
            input?.SetTextWithoutNotify(value);
        }

        private static void SetText(TMP_Text text, string value)
        {
            if (text != null)
            {
                text.text = value;
            }
        }

        private void ApplyDefaultLayoutIfNeeded()
        {
            if (!autoArrangeLayout)
            {
                return;
            }

            var root = transform as RectTransform;
            if (root == null)
            {
                return;
            }

            ConfigureCanvas();
            SetRect(root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            EnsureOutputPanelsExist();
            SetChildRect("Title", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(36f, -72f), new Vector2(-36f, -18f));
            SetChildRect("UpgradeDropdown", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(36f, -136f), new Vector2(600f, -88f));
            SetChildRect("LogScaleToggle", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(628f, -128f), new Vector2(850f, -92f));
            SetChildRect("Controls", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(36f, 36f), new Vector2(600f, -162f));
            SetChildRect("ChartPanel", new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(632f, 330f), new Vector2(-36f, -162f));

            var tablePanel = FindDirectChild("TablePanel") as RectTransform;
            if (tablePanel != null)
            {
                SetRect(tablePanel, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(632f, 36f), new Vector2(-36f, 306f));
                var nestedTable = FindChildByName(tablePanel, "Table") as RectTransform;
                if (nestedTable != null)
                {
                    SetRect(nestedTable, Vector2.zero, Vector2.one, new Vector2(18f, 14f), new Vector2(-18f, -14f));
                }
            }
            else
            {
                SetChildRect("Table", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(632f, 36f), new Vector2(-36f, 306f));
            }

            EnsureSimulationControlsExist();
            EnsureChartGraphicExists();
            ArrangeControls();
            ConfigureTextWrapping();
        }

        private void ConfigureCanvas()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }

            var scaler = GetComponentInParent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1600f, 900f);
                scaler.matchWidthOrHeight = 0.5f;
            }
        }

        private void ArrangeControls()
        {
            var controls = FindDirectChild("Controls");
            if (controls == null)
            {
                return;
            }

            ArrangeInput(controls, "BaseCost", 0);
            ArrangeInput(controls, "Multiplier", 1);
            ArrangeInput(controls, "MaxLevel", 2);
            ArrangeInput(controls, "ValuePerLevel", 3);
            ArrangeInput(controls, "Duration", 4);
            ArrangeInput(controls, "Income", 5);
            ArrangeInput(controls, "ScreenReward", 6);
            SetNamedRect(controls, "IncludeInTotalToggle", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(18f, -514f), new Vector2(-18f, -482f));

            SetNamedRect(controls, "PullButton", new Vector2(0f, 1f), new Vector2(0.5f, 1f), new Vector2(18f, -584f), new Vector2(-8f, -534f));
            SetNamedRect(controls, "ApplyButton", new Vector2(0.5f, 1f), new Vector2(1f, 1f), new Vector2(8f, -584f), new Vector2(-18f, -534f));
            SetNamedRect(controls, "Summary", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(18f, -780f), new Vector2(-18f, -608f));
        }

        private void EnsureSimulationControlsExist()
        {
            var controls = FindDirectChild("Controls");
            if (controls == null)
            {
                return;
            }

            durationInput ??= EnsureLabeledInput(controls, "Duration", "Duration, sec");
            incomeInput ??= EnsureLabeledInput(controls, "Income", "Base terraform / sec");
            screenRewardInput ??= EnsureLabeledInput(controls, "ScreenReward", "Screen reward");
            includeInTotalToggle ??= EnsureToggle(controls, "IncludeInTotalToggle", "Include in total sim");
        }

        private static Toggle EnsureToggle(Transform parent, string name, string label)
        {
            var toggle = FindChildByName(parent, name)?.GetComponent<Toggle>();
            if (toggle != null)
            {
                return toggle;
            }

            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.AddComponent<RectTransform>();
            toggle = root.AddComponent<Toggle>();

            var background = new GameObject("Background");
            background.transform.SetParent(root.transform, false);
            var bgRect = background.AddComponent<RectTransform>();
            SetRect(bgRect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, -11f), new Vector2(22f, 11f));
            var bgImage = background.AddComponent<Image>();
            bgImage.color = new Color(0.03f, 0.05f, 0.07f, 1f);

            var check = new GameObject("Checkmark");
            check.transform.SetParent(background.transform, false);
            var checkRect = check.AddComponent<RectTransform>();
            SetRect(checkRect, Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f));
            var checkImage = check.AddComponent<Image>();
            checkImage.color = new Color(0.2f, 0.85f, 1f, 1f);

            var textObject = new GameObject("Label");
            textObject.transform.SetParent(root.transform, false);
            var textRect = textObject.AddComponent<RectTransform>();
            SetRect(textRect, Vector2.zero, Vector2.one, new Vector2(32f, 0f), Vector2.zero);
            var text = textObject.AddComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = 18;
            text.alignment = TextAlignmentOptions.Left;
            text.color = new Color(0.88f, 0.94f, 1f, 1f);

            toggle.targetGraphic = bgImage;
            toggle.graphic = checkImage;
            return toggle;
        }

        private static TMP_InputField EnsureLabeledInput(Transform parent, string name, string label)
        {
            var input = FindChildByName(parent, name + "Input")?.GetComponent<TMP_InputField>();
            if (input != null)
            {
                return input;
            }

            if (FindChildByName(parent, name + "Label") == null)
            {
                var labelObject = new GameObject(name + "Label");
                labelObject.transform.SetParent(parent, false);
                labelObject.AddComponent<RectTransform>();
                var labelText = labelObject.AddComponent<TextMeshProUGUI>();
                labelText.text = label;
                labelText.fontSize = 18;
                labelText.alignment = TextAlignmentOptions.Left;
                labelText.color = new Color(0.88f, 0.94f, 1f, 1f);
            }

            var inputObject = new GameObject(name + "Input");
            inputObject.transform.SetParent(parent, false);
            var inputRect = inputObject.AddComponent<RectTransform>();
            inputRect.sizeDelta = new Vector2(280f, 32f);
            var inputImage = inputObject.AddComponent<Image>();
            inputImage.color = new Color(0.03f, 0.05f, 0.07f, 1f);
            input = inputObject.AddComponent<TMP_InputField>();

            var textObject = new GameObject("Text");
            textObject.transform.SetParent(inputObject.transform, false);
            var textRect = textObject.AddComponent<RectTransform>();
            SetRect(textRect, Vector2.zero, Vector2.one, new Vector2(10f, 3f), new Vector2(-10f, -3f));
            var text = textObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = 18;
            text.alignment = TextAlignmentOptions.Left;
            text.color = new Color(0.88f, 0.94f, 1f, 1f);

            input.textComponent = text;
            input.targetGraphic = inputImage;
            return input;
        }

        private void ArrangeInput(Transform controls, string name, int row)
        {
            var yTop = -24f - row * 68f;
            SetNamedRect(controls, name + "Label", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(18f, yTop - 24f), new Vector2(-18f, yTop));
            SetNamedRect(controls, name + "Input", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(18f, yTop - 62f), new Vector2(-18f, yTop - 30f));
        }

        private void AutoBindSceneReferences()
        {
            curveDropdown ??= FindChildByName(transform, "UpgradeDropdown")?.GetComponent<TMP_Dropdown>();
            baseCostInput ??= FindChildByName(transform, "BaseCostInput")?.GetComponent<TMP_InputField>();
            multiplierInput ??= FindChildByName(transform, "MultiplierInput")?.GetComponent<TMP_InputField>();
            maxLevelInput ??= FindChildByName(transform, "MaxLevelInput")?.GetComponent<TMP_InputField>();
            valuePerLevelInput ??= FindChildByName(transform, "ValuePerLevelInput")?.GetComponent<TMP_InputField>();
            durationInput ??= FindChildByName(transform, "DurationInput")?.GetComponent<TMP_InputField>();
            incomeInput ??= FindChildByName(transform, "IncomeInput")?.GetComponent<TMP_InputField>();
            screenRewardInput ??= FindChildByName(transform, "ScreenRewardInput")?.GetComponent<TMP_InputField>();
            includeInTotalToggle ??= FindChildByName(transform, "IncludeInTotalToggle")?.GetComponent<Toggle>();
            logScaleToggle ??= FindChildByName(transform, "LogScaleToggle")?.GetComponent<Toggle>();
            pullButton ??= FindChildByName(transform, "PullButton")?.GetComponent<Button>();
            applyButton ??= FindChildByName(transform, "ApplyButton")?.GetComponent<Button>();
            chart ??= FindChildByName(transform, "Chart")?.GetComponent<BalanceChartGraphic>();
            chart ??= FindChildByName(transform, "ChartPanel")?.GetComponent<BalanceChartGraphic>();
            summaryText ??= FindChildByName(transform, "Summary")?.GetComponent<TMP_Text>();
            tableText ??= FindChildByName(transform, "Table")?.GetComponent<TMP_Text>();
        }

        private void EnsureChartGraphicExists()
        {
            var chartPanel = FindDirectChild("ChartPanel") as RectTransform;
            if (chartPanel == null)
            {
                LogDebug("ChartPanel is missing, chart was not created.");
                return;
            }

            var childChart = FindChildByName(chartPanel, "Chart")?.GetComponent<BalanceChartGraphic>();
            if (childChart == null)
            {
                var chartObject = new GameObject("Chart");
                chartObject.transform.SetParent(chartPanel, false);
                var chartRect = chartObject.AddComponent<RectTransform>();
                SetRect(chartRect, Vector2.zero, Vector2.one, new Vector2(24f, 34f), new Vector2(-20f, -32f));
                childChart = chartObject.AddComponent<BalanceChartGraphic>();
                LogDebug("Created Chart under ChartPanel.");
            }

            var childRect = childChart.transform as RectTransform;
            if (childRect != null)
            {
                SetRect(childRect, Vector2.zero, Vector2.one, new Vector2(24f, 34f), new Vector2(-20f, -32f));
            }

            childChart.raycastTarget = false;
            childChart.color = Color.white;
            if (chart != null && chart != childChart && chart.transform == chartPanel)
            {
                chart.enabled = false;
            }

            chart = childChart;
            EnsureChartLabels(chartPanel, GetMaxValue(_costSeries), GetMaxValue(_valueSeries), logScaleToggle != null && logScaleToggle.isOn);
            LogDebug($"Chart ready. rect={((RectTransform)childChart.transform).rect}");
        }

        private void EnsureChartLabels(RectTransform chartPanel, float maxMoney, float maxIncome, bool useLogScale)
        {
            var moneyMaxLabel = FormatAxisValue(maxMoney, useLogScale);
            var incomeMaxLabel = FormatAxisValue(maxIncome, useLogScale);
            var minLabel = useLogScale ? "log 1" : "0";

            EnsureChartLabel(chartPanel, "ChartLegend", "cyan: money   green: income/s   yellow: purchases", 18,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -22f), new Vector2(-24f, 0f), TextAlignmentOptions.Left);
            EnsureChartLabel(chartPanel, "ChartX0", "0s", 16,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24f, -24f), new Vector2(90f, 0f), TextAlignmentOptions.Left);
            EnsureChartLabel(chartPanel, "ChartXMid", $"{simulationDurationSeconds * 0.5f:0}s", 16,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-45f, -24f), new Vector2(45f, 0f), TextAlignmentOptions.Center);
            EnsureChartLabel(chartPanel, "ChartXEnd", $"{simulationDurationSeconds:0}s", 16,
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-90f, -24f), new Vector2(-20f, 0f), TextAlignmentOptions.Right);
            EnsureChartLabel(chartPanel, "ChartYNote", "each line uses its own normalized scale", 15,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-300f, -22f), new Vector2(-20f, 0f), TextAlignmentOptions.Right);
            EnsureChartLabel(chartPanel, "ChartMoneyMax", $"money {moneyMaxLabel}", 16,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -50f), new Vector2(180f, -26f), TextAlignmentOptions.Left);
            EnsureChartLabel(chartPanel, "ChartMoneyMin", $"money {minLabel}", 16,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24f, 8f), new Vector2(180f, 32f), TextAlignmentOptions.Left);
            EnsureChartLabel(chartPanel, "ChartIncomeMax", $"income {incomeMaxLabel}", 16,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-190f, -50f), new Vector2(-20f, -26f), TextAlignmentOptions.Right);
            EnsureChartLabel(chartPanel, "ChartIncomeMin", $"income {minLabel}", 16,
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-190f, 8f), new Vector2(-20f, 32f), TextAlignmentOptions.Right);
        }

        private static string FormatAxisValue(float value, bool useLogScale)
        {
            var clamped = Mathf.Max(1f, value);
            if (useLogScale)
            {
                return $"log {Mathf.Log10(clamped):0.##} ({FormatCompact(value)})";
            }

            return FormatCompact(value);
        }

        private static string FormatCompact(float value)
        {
            if (value >= 1000000f)
            {
                return $"{value / 1000000f:0.##}m";
            }

            if (value >= 1000f)
            {
                return $"{value / 1000f:0.##}k";
            }

            return value.ToString("0.##");
        }

        private static void EnsureChartLabel(
            Transform parent,
            string name,
            string text,
            int fontSize,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax,
            TextAlignmentOptions alignment)
        {
            var existing = FindChildByName(parent, name);
            TMP_Text label;
            RectTransform rect;
            if (existing != null)
            {
                label = existing.GetComponent<TMP_Text>();
                rect = existing as RectTransform;
            }
            else
            {
                var labelObject = new GameObject(name);
                labelObject.transform.SetParent(parent, false);
                rect = labelObject.AddComponent<RectTransform>();
                label = labelObject.AddComponent<TextMeshProUGUI>();
            }

            if (label == null || rect == null)
            {
                return;
            }

            label.text = text;
            label.fontSize = fontSize;
            label.alignment = alignment;
            label.color = new Color(0.86f, 0.94f, 1f, 1f);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            SetRect(rect, anchorMin, anchorMax, offsetMin, offsetMax);
        }

        private void EnsureOutputPanelsExist()
        {
            if (FindDirectChild("ChartPanel") == null)
            {
                var chartPanel = new GameObject("ChartPanel");
                chartPanel.transform.SetParent(transform, false);
                chartPanel.AddComponent<RectTransform>();
                var image = chartPanel.AddComponent<Image>();
                image.color = new Color(0.07f, 0.095f, 0.12f, 0.96f);
                LogDebug("Created missing ChartPanel.");
            }

            if (FindDirectChild("TablePanel") == null && FindDirectChild("Table") == null)
            {
                var tablePanel = new GameObject("TablePanel");
                tablePanel.transform.SetParent(transform, false);
                tablePanel.AddComponent<RectTransform>();
                var image = tablePanel.AddComponent<Image>();
                image.color = new Color(0.08f, 0.11f, 0.14f, 0.96f);

                var tableObject = new GameObject("Table");
                tableObject.transform.SetParent(tablePanel.transform, false);
                tableObject.AddComponent<RectTransform>();
                tableText = tableObject.AddComponent<TextMeshProUGUI>();
                tableText.fontSize = 18;
                tableText.alignment = TextAlignmentOptions.TopLeft;
                tableText.color = new Color(0.88f, 0.94f, 1f, 1f);
                LogDebug("Created missing TablePanel.");
            }
        }

        private void ConfigureTextWrapping()
        {
            if (summaryText != null)
            {
                summaryText.textWrappingMode = TextWrappingModes.Normal;
                summaryText.overflowMode = TextOverflowModes.Ellipsis;
            }

            if (tableText != null)
            {
                tableText.textWrappingMode = TextWrappingModes.NoWrap;
                tableText.overflowMode = TextOverflowModes.Ellipsis;
            }
        }

        private void SetChildRect(string childName, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var child = FindDirectChild(childName) as RectTransform;
            if (child != null)
            {
                SetRect(child, anchorMin, anchorMax, offsetMin, offsetMax);
            }
        }

        private static void SetNamedRect(Transform parent, string childName, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var child = FindChildByName(parent, childName) as RectTransform;
            if (child != null)
            {
                SetRect(child, anchorMin, anchorMax, offsetMin, offsetMax);
            }
        }

        private Transform FindDirectChild(string childName)
        {
            for (var i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                if (child != null && string.Equals(child.name, childName, System.StringComparison.Ordinal))
                {
                    return child;
                }
            }

            return null;
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

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private void LogDebug(string message)
        {
            if (logLayoutDebug)
            {
                Debug.Log($"[BalanceDiagramTool] {message}", this);
            }
        }

        private static void EnsureDropdownTemplate(TMP_Dropdown dropdown)
        {
            if (dropdown == null || dropdown.template != null)
            {
                return;
            }

            var template = new GameObject("Template");
            template.SetActive(false);
            template.transform.SetParent(dropdown.transform, false);
            var templateRect = template.AddComponent<RectTransform>();
            SetRect(templateRect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -240f), new Vector2(0f, -48f));
            var templateImage = template.AddComponent<Image>();
            templateImage.color = new Color(0.025f, 0.04f, 0.055f, 1f);
            var scrollRect = template.AddComponent<ScrollRect>();

            var viewport = new GameObject("Viewport");
            viewport.transform.SetParent(template.transform, false);
            var viewportRect = viewport.AddComponent<RectTransform>();
            SetRect(viewportRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var viewportImage = viewport.AddComponent<Image>();
            viewportImage.color = new Color(0.025f, 0.04f, 0.055f, 1f);
            viewport.AddComponent<Mask>().showMaskGraphic = false;

            var content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            var contentRect = content.AddComponent<RectTransform>();
            SetRect(contentRect, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            contentRect.pivot = new Vector2(0.5f, 1f);

            var item = new GameObject("Item");
            item.transform.SetParent(content.transform, false);
            var itemRect = item.AddComponent<RectTransform>();
            SetRect(itemRect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -32f), Vector2.zero);
            var itemToggle = item.AddComponent<Toggle>();

            var itemBackground = new GameObject("Item Background");
            itemBackground.transform.SetParent(item.transform, false);
            var itemBackgroundRect = itemBackground.AddComponent<RectTransform>();
            SetRect(itemBackgroundRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var itemBackgroundImage = itemBackground.AddComponent<Image>();
            itemBackgroundImage.color = new Color(0.05f, 0.08f, 0.105f, 1f);

            var itemCheckmark = new GameObject("Item Checkmark");
            itemCheckmark.transform.SetParent(item.transform, false);
            var itemCheckmarkRect = itemCheckmark.AddComponent<RectTransform>();
            SetRect(itemCheckmarkRect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(8f, -7f), new Vector2(22f, 7f));
            var itemCheckmarkImage = itemCheckmark.AddComponent<Image>();
            itemCheckmarkImage.color = new Color(0.2f, 0.85f, 1f, 1f);

            var itemLabelObject = new GameObject("Item Label");
            itemLabelObject.transform.SetParent(item.transform, false);
            var itemLabelRect = itemLabelObject.AddComponent<RectTransform>();
            SetRect(itemLabelRect, Vector2.zero, Vector2.one, new Vector2(32f, 3f), new Vector2(-10f, -3f));
            var itemLabel = itemLabelObject.AddComponent<TextMeshProUGUI>();
            itemLabel.text = "Option";
            itemLabel.fontSize = 18;
            itemLabel.alignment = TextAlignmentOptions.Left;
            itemLabel.color = new Color(0.88f, 0.94f, 1f, 1f);

            itemToggle.targetGraphic = itemBackgroundImage;
            itemToggle.graphic = itemCheckmarkImage;
            scrollRect.content = contentRect;
            scrollRect.viewport = viewportRect;
            scrollRect.horizontal = false;
            dropdown.template = templateRect;
            dropdown.itemText = itemLabel;
        }
    }
}
