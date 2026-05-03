using UnityEngine;

public static class TutorialSave
{
    private const string StepKey = "Tutorial.StepIndex";
    private const string CompletedKey = "Tutorial.Completed";

    public static int LoadStepIndex()
    {
        return Mathf.Max(0, PlayerPrefs.GetInt(StepKey, 0));
    }

    public static void SaveStepIndex(int stepIndex)
    {
        PlayerPrefs.SetInt(StepKey, Mathf.Max(0, stepIndex));
    }

    public static bool IsCompleted()
    {
        return PlayerPrefs.GetInt(CompletedKey, 0) == 1;
    }

    public static void MarkCompleted()
    {
        PlayerPrefs.SetInt(CompletedKey, 1);
    }

    public static void Clear()
    {
        PlayerPrefs.DeleteKey(StepKey);
        PlayerPrefs.DeleteKey(CompletedKey);
        PlayerPrefs.Save();
    }
}
