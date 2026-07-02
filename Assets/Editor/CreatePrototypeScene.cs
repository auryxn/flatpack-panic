using FlatpackPanic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CreatePrototypeScene
{
    public static void Build()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("Flatpack Panic First Person Prototype").AddComponent<FlatpackGame>();
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/Prototype.unity");
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/Prototype.unity", true) };
        AssetDatabase.SaveAssets();
        Debug.Log("Flatpack Panic first-person scene created: Assets/Scenes/Prototype.unity");
    }
}
