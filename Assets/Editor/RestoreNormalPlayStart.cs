using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Clean up the local setting left by the removed Neptune testing shortcut.
[InitializeOnLoad]
public static class RestoreNormalPlayStart
{
    static RestoreNormalPlayStart()
    {
        EditorApplication.delayCall += Restore;
    }

    private static void Restore()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += Restore;
            return;
        }
        string key = Application.dataPath + ".NeptuneQuickPlay";
        if (!EditorPrefs.GetBool(key, true)) return;
        if (AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene) ==
            "Assets/00_Scenes/b_NeptuneExploration.unity")
            EditorSceneManager.playModeStartScene = null;
        EditorPrefs.SetBool(key, false);
    }
}
