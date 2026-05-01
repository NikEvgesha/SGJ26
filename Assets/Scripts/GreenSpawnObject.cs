using System;
using System.Collections.Generic;
using UnityEngine;

namespace LittlePlanet.PlanetSystem
{
    [DisallowMultipleComponent]
    public sealed class GreenSpawnObject : MonoBehaviour
    {
        [SerializeField] private List<GameObject> stages = new();
        [SerializeField] private List<float> stageActivationPercents = new();
        [SerializeField] private bool autoCollectStagesByName = true;
        [SerializeField] private string stageNamePrefix = "stage_";
        [SerializeField] private bool cumulativeStages = true;

        private bool _initialized;
        private int _lastHighestEnabledStage = int.MinValue;
        private float _lastAppliedPercent = -1f;

        private struct NamedStage
        {
            public int Index;
            public GameObject StageObject;
        }

        private void Awake()
        {
            EnsureInitialized();
            ApplyStagePercent(0f);
        }

        public void SetGrowth01(float value01)
        {
            var percent = Mathf.Clamp01(value01) * 100f;
            if (Mathf.Abs(percent - _lastAppliedPercent) <= 0.0001f)
            {
                return;
            }

            _lastAppliedPercent = percent;
            ApplyStagePercent(percent);
        }

        private void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            if (autoCollectStagesByName)
            {
                AutoCollectStages();
            }

            if (stages == null)
            {
                stages = new List<GameObject>();
            }

            if (stageActivationPercents == null)
            {
                stageActivationPercents = new List<float>();
            }

            for (var i = 0; i < stages.Count; i++)
            {
                if (i < stageActivationPercents.Count)
                {
                    stageActivationPercents[i] = Mathf.Clamp(stageActivationPercents[i], 0f, 100f);
                    continue;
                }

                var t = stages.Count <= 1 ? 1f : (float)i / (stages.Count - 1);
                stageActivationPercents.Add(t * 100f);
            }

            _initialized = true;
        }

        private void AutoCollectStages()
        {
            var found = new List<NamedStage>();
            for (var i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                if (child == null || string.IsNullOrWhiteSpace(child.name))
                {
                    continue;
                }

                if (!child.name.StartsWith(stageNamePrefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var indexPart = child.name.Substring(stageNamePrefix.Length);
                if (!int.TryParse(indexPart, out var stageIndex))
                {
                    continue;
                }

                found.Add(new NamedStage
                {
                    Index = stageIndex,
                    StageObject = child.gameObject
                });
            }

            if (found.Count == 0)
            {
                return;
            }

            found.Sort((a, b) => a.Index.CompareTo(b.Index));
            stages.Clear();
            for (var i = 0; i < found.Count; i++)
            {
                stages.Add(found[i].StageObject);
            }
        }

        private void ApplyStagePercent(float percent)
        {
            EnsureInitialized();
            if (stages == null || stages.Count == 0)
            {
                return;
            }

            var highestEnabledStage = -1;
            for (var i = 0; i < stages.Count; i++)
            {
                var threshold = i < stageActivationPercents.Count
                    ? stageActivationPercents[i]
                    : 100f;
                if (percent + 0.0001f >= threshold)
                {
                    highestEnabledStage = i;
                }
            }

            if (highestEnabledStage == _lastHighestEnabledStage)
            {
                return;
            }

            _lastHighestEnabledStage = highestEnabledStage;

            for (var i = 0; i < stages.Count; i++)
            {
                var stageObject = stages[i];
                if (stageObject == null)
                {
                    continue;
                }

                var shouldBeActive = cumulativeStages
                    ? i <= highestEnabledStage
                    : i == highestEnabledStage;
                if (stageObject.activeSelf != shouldBeActive)
                {
                    stageObject.SetActive(shouldBeActive);
                }
            }
        }
    }
}
