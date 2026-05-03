using UnityEditor;
using UnityEngine;

public static class TutorialSaveToolbar
{
    [MenuItem("Tools/Little Planet/Tutorial/Reset Tutorial Save")]
    private static void ResetTutorialSave()
    {
        var shouldReset = EditorUtility.DisplayDialog(
            "Reset Tutorial Save",
            "Clear saved tutorial progress for this editor/user?",
            "Reset",
            "Cancel");

        if (!shouldReset)
        {
            return;
        }

        TutorialSave.Clear();
        Debug.Log("[TutorialSaveToolbar] Tutorial save reset.");
    }
}
