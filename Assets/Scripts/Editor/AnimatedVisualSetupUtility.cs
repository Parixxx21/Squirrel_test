using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class AnimatedVisualSetupUtility
{
    private const string ControllerFolder = "Assets/Generated/AnimationControllers";

    [MenuItem("CS275/Setup Selected Animated Visual")]
    public static void SetupSelectedAnimatedVisual()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            EditorUtility.DisplayDialog("Animated Visual Setup", "Select the imported visual child object in the scene first.", "OK");
            return;
        }

        if (selected.GetComponentInParent<SquirrelAgent>() == null && selected.GetComponentInParent<PredatorAgent>() == null)
        {
            EditorUtility.DisplayDialog(
                "Animated Visual Setup",
                "The selected object must be a visual child under a Squirrel or Predator root. This prevents accidentally modifying training objects.",
                "OK");
            return;
        }

        Animator animator = selected.GetComponentInChildren<Animator>(true);
        if (animator == null)
            animator = selected.AddComponent<Animator>();

        AnimationClip[] clips = FindClips(selected);
        if (clips.Length == 0)
        {
            EditorUtility.DisplayDialog("Animated Visual Setup", "No animation clips were found on this imported model.", "OK");
            return;
        }

        AnimationClip idle = PickClip(clips, "idle") ?? clips[0];
        AnimationClip walk = PickClip(clips, "walk") ?? idle;
        AnimationClip run = PickClip(clips, "run", "gallop", "sprint") ?? walk;

        Directory.CreateDirectory(ControllerFolder);
        string safeName = SanitizeFileName(selected.name);
        string path = AssetDatabase.GenerateUniqueAssetPath($"{ControllerFolder}/{safeName}_Locomotion.controller");

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Moving", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Running", AnimatorControllerParameterType.Bool);

        BlendTree blendTree;
        controller.CreateBlendTreeInController("Locomotion", out blendTree, 0);
        blendTree.blendType = BlendTreeType.Simple1D;
        blendTree.blendParameter = "Speed";
        blendTree.useAutomaticThresholds = false;
        blendTree.AddChild(idle, 0f);
        blendTree.AddChild(walk, 0.45f);
        blendTree.AddChild(run, 1f);

        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;

        CreatureAnimationDriver driver = selected.GetComponent<CreatureAnimationDriver>();
        if (driver == null)
            driver = selected.AddComponent<CreatureAnimationDriver>();

        driver.animator = animator;
        driver.disableRootMotion = true;
        driver.disableImportedPhysics = true;

        DisableImportedPhysics(selected);

        EditorUtility.SetDirty(selected);
        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog(
            "Animated Visual Setup",
            $"Created {Path.GetFileName(path)}\nIdle: {idle.name}\nWalk: {walk.name}\nRun: {run.name}",
            "OK");
    }

    private static AnimationClip[] FindClips(GameObject selected)
    {
        HashSet<AnimationClip> clips = new HashSet<AnimationClip>();

        foreach (AnimationClip clip in AnimationUtility.GetAnimationClips(selected))
        {
            if (clip != null)
                clips.Add(clip);
        }

        string assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(selected);
        if (!string.IsNullOrEmpty(assetPath))
        {
            foreach (AnimationClip clip in AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<AnimationClip>())
            {
                if (clip != null && !clip.name.StartsWith("__preview__", System.StringComparison.OrdinalIgnoreCase))
                    clips.Add(clip);
            }
        }

        return clips.ToArray();
    }

    private static AnimationClip PickClip(IEnumerable<AnimationClip> clips, params string[] keywords)
    {
        foreach (string keyword in keywords)
        {
            AnimationClip clip = clips.FirstOrDefault(c => c.name.ToLowerInvariant().Contains(keyword));
            if (clip != null)
                return clip;
        }

        return null;
    }

    private static void DisableImportedPhysics(GameObject selected)
    {
        Rigidbody rootBody = selected.GetComponentInParent<Rigidbody>();

        foreach (Rigidbody childBody in selected.GetComponentsInChildren<Rigidbody>(true))
        {
            if (childBody != rootBody)
            {
                childBody.isKinematic = true;
                childBody.detectCollisions = false;
            }
        }

        foreach (Collider childCollider in selected.GetComponentsInChildren<Collider>(true))
            childCollider.enabled = false;
    }

    private static string SanitizeFileName(string name)
    {
        foreach (char invalid in Path.GetInvalidFileNameChars())
            name = name.Replace(invalid, '_');

        return string.IsNullOrWhiteSpace(name) ? "AnimatedVisual" : name;
    }
}
