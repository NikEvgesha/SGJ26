using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct BuildingEffect
{
    public TerraformingType type;
    public float value;
}

public class Building : MonoBehaviour
{
    [SerializeField] private string buildingName = "Building";
    [SerializeField, TextArea] private string description = string.Empty;
    [SerializeField, Min(0)] private int price = 100;
    [SerializeField] private Sprite icon;
    [SerializeField] private List<BuildingEffect> effects = new();

    public string BuildingName => string.IsNullOrWhiteSpace(buildingName) ? name : buildingName;
    public string Description => description;
    public int Price => price;
    public Sprite Icon => icon;
    public IReadOnlyList<BuildingEffect> Effects => effects;
}
