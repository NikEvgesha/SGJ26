using UnityEngine;

public sealed class TutorialArrowTarget : MonoBehaviour
{
    [SerializeField] private RectTransform uiTarget;
    [SerializeField] private Transform worldTarget;
    [SerializeField] private Vector3 worldOffset;

    public RectTransform UiTarget => uiTarget != null ? uiTarget : transform as RectTransform;
    public Transform WorldTarget => worldTarget != null ? worldTarget : transform;
    public Vector3 WorldOffset => worldOffset;
}
