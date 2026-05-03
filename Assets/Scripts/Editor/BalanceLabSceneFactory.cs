using System.Collections.Generic;
using LittlePlanet.Balance;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LittlePlanet.EditorTools
{
    public static class BalanceLabSceneFactory
    {
        private const string ScenePath = "Assets/Scenes/BalanceLab.unity";

        [MenuItem("Tools/Little Planet/Balance/Create Balance Lab Scene")]
        public static void CreateBalanceLabScene()
        {
            RecreateBalanceLabScene();
        }

        [MenuItem("Tools/Little Planet/Balance/Recreate Balance Lab Scene")]
        public static void RecreateBalanceLabScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "BalanceLab";

            var cameraObject = new GameObject("Main Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.06f, 0.08f, 0.1f, 1f);
            camera.orthographic = true;
            cameraObject.tag = "MainCamera";

            var canvasObject = new GameObject("Canvas");
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            var root = CreatePanel("BalanceToolRoot", canvasObject.transform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Color(0.05f, 0.07f, 0.09f, 1f));
            var tool = root.gameObject.AddComponent<BalanceDiagramTool>();

            var title = CreateText("Title", root, "Balance Lab", 32, TextAlignmentOptions.Left);
            SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(36f, -72f), new Vector2(-36f, -18f));

            var dropdown = CreateDropdown("UpgradeDropdown", root);
            SetRect(dropdown.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(36f, -136f), new Vector2(600f, -88f));

            var logToggle = CreateToggle("LogScaleToggle", root, "Log scale");
            SetRect(logToggle.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(628f, -128f), new Vector2(850f, -92f));

            var controls = CreatePanel("Controls", root, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(36f, 36f), new Vector2(600f, -162f), new Color(0.08f, 0.11f, 0.14f, 0.96f));
            var baseCostInput = CreateLabeledInput(controls, "BaseCost", "Base cost", 0);
            var multiplierInput = CreateLabeledInput(controls, "Multiplier", "Cost multiplier", 1);
            var maxLevelInput = CreateLabeledInput(controls, "MaxLevel", "Max level", 2);
            var valueInput = CreateLabeledInput(controls, "ValuePerLevel", "Value / level", 3);
            var durationInput = CreateLabeledInput(controls, "Duration", "Duration, sec", 4);
            var incomeInput = CreateLabeledInput(controls, "Income", "Base terraform / sec", 5);
            var screenRewardInput = CreateLabeledInput(controls, "ScreenReward", "Screen reward", 6);
            var includeToggle = CreateToggle("IncludeInTotalToggle", controls, "Include in total sim");
            SetRect(includeToggle.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(18f, -514f), new Vector2(-18f, -482f));

            var pullButton = CreateButton("PullButton", controls, "Pull From Asset");
            SetRect(pullButton.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0.5f, 1f), new Vector2(18f, -584f), new Vector2(-8f, -534f));
            var applyButton = CreateButton("ApplyButton", controls, "Apply To Asset");
            SetRect(applyButton.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(1f, 1f), new Vector2(8f, -584f), new Vector2(-18f, -534f));

            var summary = CreateText("Summary", controls, "", 19, TextAlignmentOptions.TopLeft);
            summary.textWrappingMode = TextWrappingModes.Normal;
            SetRect(summary.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(18f, -780f), new Vector2(-18f, -608f));

            var chartPanel = CreatePanel("ChartPanel", root, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(632f, 330f), new Vector2(-36f, -162f), new Color(0.07f, 0.095f, 0.12f, 0.96f));
            var chart = CreateChart("Chart", chartPanel);

            var tablePanel = CreatePanel("TablePanel", root, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(632f, 36f), new Vector2(-36f, 306f), new Color(0.08f, 0.11f, 0.14f, 0.96f));
            var table = CreateText("Table", tablePanel, "", 18, TextAlignmentOptions.TopLeft);
            table.textWrappingMode = TextWrappingModes.NoWrap;
            SetRect(table.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(18f, 14f), new Vector2(-18f, -14f));

            ConfigureTool(tool, dropdown, baseCostInput, multiplierInput, maxLevelInput, valueInput, durationInput, incomeInput, screenRewardInput, includeToggle, logToggle, pullButton, applyButton, chart, summary, table);

            EnsureFolder("Assets", "Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        }

        private static void ConfigureTool(
            BalanceDiagramTool tool,
            TMP_Dropdown dropdown,
            TMP_InputField baseCost,
            TMP_InputField multiplier,
            TMP_InputField maxLevel,
            TMP_InputField valuePerLevel,
            TMP_InputField duration,
            TMP_InputField income,
            TMP_InputField screenReward,
            Toggle includeToggle,
            Toggle logToggle,
            Button pullButton,
            Button applyButton,
            BalanceChartGraphic chart,
            TMP_Text summary,
            TMP_Text table)
        {
            var serializedTool = new SerializedObject(tool);
            serializedTool.FindProperty("curveDropdown").objectReferenceValue = dropdown;
            serializedTool.FindProperty("baseCostInput").objectReferenceValue = baseCost;
            serializedTool.FindProperty("multiplierInput").objectReferenceValue = multiplier;
            serializedTool.FindProperty("maxLevelInput").objectReferenceValue = maxLevel;
            serializedTool.FindProperty("valuePerLevelInput").objectReferenceValue = valuePerLevel;
            serializedTool.FindProperty("durationInput").objectReferenceValue = duration;
            serializedTool.FindProperty("incomeInput").objectReferenceValue = income;
            serializedTool.FindProperty("screenRewardInput").objectReferenceValue = screenReward;
            serializedTool.FindProperty("includeInTotalToggle").objectReferenceValue = includeToggle;
            serializedTool.FindProperty("logScaleToggle").objectReferenceValue = logToggle;
            serializedTool.FindProperty("pullButton").objectReferenceValue = pullButton;
            serializedTool.FindProperty("applyButton").objectReferenceValue = applyButton;
            serializedTool.FindProperty("chart").objectReferenceValue = chart;
            serializedTool.FindProperty("summaryText").objectReferenceValue = summary;
            serializedTool.FindProperty("tableText").objectReferenceValue = table;

            var curves = serializedTool.FindProperty("curves");
            var upgrades = FindUpgradeDefinitions();
            curves.arraySize = upgrades.Count;
            for (var i = 0; i < upgrades.Count; i++)
            {
                var curve = curves.GetArrayElementAtIndex(i);
                var upgrade = upgrades[i];
                curve.FindPropertyRelative("label").stringValue = upgrade.UpgradeName;
                curve.FindPropertyRelative("target").objectReferenceValue = upgrade;
                curve.FindPropertyRelative("baseCost").intValue = upgrade.BaseCost;
                curve.FindPropertyRelative("costMultiplier").floatValue = upgrade.CostMultiplier;
                curve.FindPropertyRelative("maxLevel").intValue = upgrade.MaxLevel;
                curve.FindPropertyRelative("valuePerLevel").floatValue = upgrade.ValuePerLevel;
                curve.FindPropertyRelative("includeInTotal").boolValue = true;
            }

            serializedTool.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(tool);
        }

        private static List<UpgradeDefinition> FindUpgradeDefinitions()
        {
            var result = new List<UpgradeDefinition>();
            var guids = AssetDatabase.FindAssets("t:UpgradeDefinition");
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var upgrade = AssetDatabase.LoadAssetAtPath<UpgradeDefinition>(path);
                if (upgrade != null && upgrade.UpgradeType != UpgradeType.FlyDuration && upgrade.UpgradeType != UpgradeType.FlyCooldown)
                {
                    result.Add(upgrade);
                }
            }

            return result;
        }

        private static RectTransform CreatePanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Color color)
        {
            var panel = new GameObject(name);
            panel.transform.SetParent(parent, false);
            var rect = panel.AddComponent<RectTransform>();
            SetRect(rect, anchorMin, anchorMax, offsetMin, offsetMax);
            var image = panel.AddComponent<Image>();
            image.color = color;
            return rect;
        }

        private static TMP_Text CreateText(string name, Transform parent, string text, int fontSize, TextAlignmentOptions alignment)
        {
            var textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);
            var rect = textObject.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var tmp = textObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = alignment;
            tmp.color = new Color(0.88f, 0.94f, 1f, 1f);
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            return tmp;
        }

        private static TMP_InputField CreateLabeledInput(Transform parent, string name, string label, int row)
        {
            var yTop = -24f - row * 68f;
            var labelText = CreateText(name + "Label", parent, label, 18, TextAlignmentOptions.Left);
            SetRect(labelText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(18f, yTop - 24f), new Vector2(-18f, yTop));

            var input = CreateInput(name + "Input", parent);
            SetRect(input.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(18f, yTop - 62f), new Vector2(-18f, yTop - 30f));
            return input;
        }

        private static TMP_InputField CreateInput(string name, Transform parent)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            var rect = root.AddComponent<RectTransform>();
            var image = root.AddComponent<Image>();
            image.color = new Color(0.03f, 0.05f, 0.07f, 1f);
            var input = root.AddComponent<TMP_InputField>();

            var text = CreateText("Text", root.transform, "", 18, TextAlignmentOptions.Left);
            SetRect(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(10f, 3f), new Vector2(-10f, -3f));
            input.textComponent = text;
            input.targetGraphic = image;
            rect.sizeDelta = new Vector2(280f, 32f);
            return input;
        }

        private static BalanceChartGraphic CreateChart(string name, Transform parent)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            var rect = root.AddComponent<RectTransform>();
            SetRect(rect, Vector2.zero, Vector2.one, new Vector2(24f, 20f), new Vector2(-20f, -24f));
            var chart = root.AddComponent<BalanceChartGraphic>();
            chart.raycastTarget = false;
            return chart;
        }

        private static TMP_Dropdown CreateDropdown(string name, Transform parent)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            var rect = root.AddComponent<RectTransform>();
            var image = root.AddComponent<Image>();
            image.color = new Color(0.03f, 0.05f, 0.07f, 1f);
            var dropdown = root.AddComponent<TMP_Dropdown>();
            var label = CreateText("Label", root.transform, "Upgrade", 18, TextAlignmentOptions.Left);
            SetRect(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(10f, 4f), new Vector2(-10f, -4f));
            dropdown.captionText = label;
            dropdown.targetGraphic = image;
            CreateDropdownTemplate(dropdown, root.transform);
            rect.sizeDelta = new Vector2(480f, 48f);
            return dropdown;
        }

        private static void CreateDropdownTemplate(TMP_Dropdown dropdown, Transform parent)
        {
            var template = new GameObject("Template");
            template.SetActive(false);
            template.transform.SetParent(parent, false);
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

            var itemLabel = CreateText("Item Label", item.transform, "Option", 18, TextAlignmentOptions.Left);
            SetRect(itemLabel.rectTransform, Vector2.zero, Vector2.one, new Vector2(32f, 3f), new Vector2(-10f, -3f));

            itemToggle.targetGraphic = itemBackgroundImage;
            itemToggle.graphic = itemCheckmarkImage;
            scrollRect.content = contentRect;
            scrollRect.viewport = viewportRect;
            scrollRect.horizontal = false;
            dropdown.template = templateRect;
            dropdown.itemText = itemLabel;
        }

        private static Toggle CreateToggle(string name, Transform parent, string label)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.AddComponent<RectTransform>();
            var toggle = root.AddComponent<Toggle>();
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
            toggle.targetGraphic = bgImage;
            toggle.graphic = checkImage;
            var text = CreateText("Label", root.transform, label, 18, TextAlignmentOptions.Left);
            SetRect(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(32f, 0f), Vector2.zero);
            return toggle;
        }

        private static Button CreateButton(string name, Transform parent, string label)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            var image = root.AddComponent<Image>();
            image.color = new Color(0.13f, 0.24f, 0.34f, 1f);
            var button = root.AddComponent<Button>();
            button.targetGraphic = image;
            var text = CreateText("Text", root.transform, label, 18, TextAlignmentOptions.Center);
            SetRect(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return button;
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static void EnsureFolder(string parent, string child)
        {
            var path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }
    }
}
