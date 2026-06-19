using System.Linq;
using UnityEditor;
using UnityEngine;

public static class RiggedVisualDiagnostics
{
    [MenuItem("CS275/Diagnose Rigged Visual Resources")]
    public static void DiagnoseRiggedVisualResources()
    {
        LogResource("Models/Fox/quaternius_animated_fox");
        LogFolder("Models/Squirrel");
    }

    private static void LogResource(string path)
    {
        GameObject prefab = Resources.Load<GameObject>(path);
        AnimationClip[] clips = Resources.LoadAll<AnimationClip>(path);
        Debug.Log($"[RiggedVisualDiagnostics] {path}: prefab={(prefab != null ? prefab.name : "null")}, clips={clips.Length} [{string.Join(", ", clips.Select(c => c.name))}]");
    }

    private static void LogFolder(string path)
    {
        GameObject[] prefabs = Resources.LoadAll<GameObject>(path);
        AnimationClip[] clips = Resources.LoadAll<AnimationClip>(path);
        Debug.Log($"[RiggedVisualDiagnostics] {path}: prefabs={prefabs.Length} [{string.Join(", ", prefabs.Select(p => p.name))}], clips={clips.Length} [{string.Join(", ", clips.Select(c => c.name))}]");
    }
}
