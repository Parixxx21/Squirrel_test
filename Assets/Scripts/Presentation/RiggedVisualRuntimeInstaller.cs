using System.Collections.Generic;
using System.IO;
using System.Linq;
using GLTFast;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

#if UNITY_EDITOR
using UnityEditor;
#endif

// Presentation-only installer for rigged imported animal models.
// It only replaces visible children and never changes ML-Agent roots, colliders, Rigidbody, or rewards.
public static class RiggedVisualRuntimeInstaller
{
    private static readonly bool EnableRuntimeRiggedVisuals = true;
    private static readonly bool EnableFoxReplacement = true;
    private static readonly bool EnableSquirrelReplacement = true;
    private static readonly bool EnableGeneratedSquirrelFallback = false;

    private const string FoxResourcePath = "Models/Fox/quaternius_animated_fox";
    private const string FoxEditorAssetPath = "Assets/Resources/Models/Fox/quaternius_animated_fox.glb";
    private const string SquirrelResourceFolder = "Models/Squirrel";
    private const string FoxVisualName = "AnimatedFoxVisual";
    private const string SquirrelVisualName = "AnimatedSquirrelVisual";
    private const string ImportedModelChildName = "ImportedRiggedModel";
    private const string FoxPaletteMaterialSuffix = "_PresentationFoxPalette";
    private static readonly Color FoxOrange = new Color(0.95f, 0.38f, 0.08f, 1f);
    private static readonly Color FoxWarmWhite = new Color(0.96f, 0.9f, 0.78f, 1f);
    private static readonly Color FoxBlack = new Color(0.025f, 0.022f, 0.02f, 1f);
    private static readonly Color SquirrelOrange = new Color(0.76f, 0.31f, 0.09f, 1f);
    private static readonly Color SquirrelCream = new Color(0.92f, 0.82f, 0.63f, 1f);
    private static readonly Color SquirrelDark = new Color(0.12f, 0.07f, 0.035f, 1f);
    private static readonly Color SquirrelNose = new Color(0.035f, 0.025f, 0.02f, 1f);
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallRuntimeVisuals()
    {
        if (!EnableRuntimeRiggedVisuals)
            return;

        if (EnableFoxReplacement)
            InstallFoxVisuals();

        if (EnableSquirrelReplacement)
            InstallSquirrelVisuals();
    }

    private static void InstallFoxVisuals()
    {
        GameObject foxPrefab = LoadPrefab(FoxResourcePath, FoxEditorAssetPath);
        AnimationClip[] foxClips = LoadClips(FoxResourcePath, FoxEditorAssetPath);
        if (foxPrefab == null || foxClips.Length == 0)
        {
            InstallFoxVisualsFromGltfFile();
            return;
        }

        int installed = 0;
        foreach (PredatorAgent predator in Object.FindObjectsByType<PredatorAgent>(FindObjectsInactive.Exclude))
        {
            if (InstallRiggedVisual(
                predator.transform,
                foxPrefab,
                foxClips,
                FoxVisualName,
                targetHeight: 2.5f,
                walkSpeed: 0.2f,
                runSpeed: 2.6f,
                clearance: 0.02f,
                walkPlaybackSpeed: 1.12f,
                runPlaybackSpeed: 1.25f,
                idleFadeOutSpeed01: 0.12f,
                runBlendStartSpeed01: 0.72f,
                applyFoxPalette: true,
                runKeywords: new[] { "gallop", "run", "sprint" }))
            {
                installed++;
            }
        }

        Debug.Log($"Animated fox visual installer added {installed} visual(s). Clips: {string.Join(", ", foxClips.Select(clip => clip.name))}");
    }

    private static async void InstallFoxVisualsFromGltfFile()
    {
        string glbPath = Path.Combine(Application.dataPath, "Resources/Models/Fox/quaternius_animated_fox.glb");
        if (!File.Exists(glbPath))
        {
            Debug.LogWarning($"Animated fox visual was not installed because the GLB file was not found at {glbPath}.");
            return;
        }

        try
        {
            GltfImport gltf = new GltfImport();
            ImportSettings importSettings = new ImportSettings
            {
                AnimationMethod = AnimationMethod.Mecanim
            };

            bool loaded = await gltf.LoadFile(glbPath, importSettings: importSettings);
            AnimationClip[] foxClips = loaded
                ? gltf.GetAnimationClips().Where(IsUsableClip).ToArray()
                : new AnimationClip[0];

            if (!loaded || foxClips.Length == 0)
            {
                Debug.LogWarning($"Animated fox visual was not installed from GLB. Loaded: {loaded}, clips found: {foxClips.Length}.");
                return;
            }

            int installed = 0;
            foreach (PredatorAgent predator in Object.FindObjectsByType<PredatorAgent>(FindObjectsInactive.Exclude))
            {
                if (await InstallGltfRiggedVisual(
                    predator.transform,
                    gltf,
                    foxClips,
                    FoxVisualName,
                    targetHeight: 2.5f,
                    walkSpeed: 0.2f,
                    runSpeed: 2.6f,
                    clearance: 0.02f,
                    walkPlaybackSpeed: 1.12f,
                    runPlaybackSpeed: 1.25f,
                    idleFadeOutSpeed01: 0.12f,
                    runBlendStartSpeed01: 0.72f,
                    applyFoxPalette: true,
                    runKeywords: new[] { "gallop", "run", "sprint" }))
                {
                    installed++;
                }
            }

            Debug.Log($"Animated fox GLB installer added {installed} visual(s). Clips: {string.Join(", ", foxClips.Select(clip => clip.name))}");
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning($"Animated fox visual GLB install failed: {exception.Message}");
        }
    }

    private static void InstallSquirrelVisuals()
    {
        GameObject squirrelPrefab = LoadFirstResourcePrefab(SquirrelResourceFolder);
        if (squirrelPrefab == null)
        {
            InstallSquirrelVisualsFromGltfFile(
                $"No imported squirrel model was found in Resources/{SquirrelResourceFolder}; keeping the original squirrel visual and using its presentation animator.");
            return;
        }

        string assetPath = GetResourceAssetPath(squirrelPrefab);
        AnimationClip[] squirrelClips = LoadClips($"{SquirrelResourceFolder}/{squirrelPrefab.name}", assetPath);
        if (squirrelClips.Length == 0)
        {
            Debug.LogWarning($"Animated squirrel visual '{squirrelPrefab.name}' was found, but no animation clips were importable.");
            InstallSquirrelVisualsFromGltfFile(
                $"Imported squirrel model '{squirrelPrefab.name}' has no usable moving animation clip; keeping the original squirrel visual and using its presentation animator.");
            return;
        }

        int installed = 0;
        foreach (SquirrelAgent squirrel in Object.FindObjectsByType<SquirrelAgent>(FindObjectsInactive.Exclude))
        {
            if (InstallRiggedVisual(
                squirrel.transform,
                squirrelPrefab,
                squirrelClips,
                SquirrelVisualName,
                targetHeight: 0.65f,
                walkSpeed: 0.35f,
                runSpeed: 3.0f,
                clearance: 0.06f,
                walkPlaybackSpeed: 1.1f,
                runPlaybackSpeed: 1.25f,
                idleFadeOutSpeed01: 0.2f,
                runBlendStartSpeed01: 0.55f,
                applyFoxPalette: false,
                runKeywords: new[] { "run", "gallop", "sprint", "move", "moving" },
                runOnlyClipAcrossStates: true))
            {
                installed++;
            }
        }

        Debug.Log($"Animated squirrel visual installer added {installed} visual(s). Model: {squirrelPrefab.name}. Clips: {string.Join(", ", squirrelClips.Select(clip => clip.name))}");
    }

    private static async void InstallSquirrelVisualsFromGltfFile(string fallbackWarning)
    {
        string gltfPath = FindFirstModelFileInResources(SquirrelResourceFolder, ".glb", ".gltf");
        if (string.IsNullOrEmpty(gltfPath))
        {
            TryInstallGeneratedSquirrelFallback(fallbackWarning);
            return;
        }

        try
        {
            GltfImport gltf = new GltfImport();
            ImportSettings importSettings = new ImportSettings
            {
                AnimationMethod = AnimationMethod.Mecanim
            };

            bool loaded = await gltf.LoadFile(gltfPath, importSettings: importSettings);
            AnimationClip[] squirrelClips = loaded
                ? gltf.GetAnimationClips().Where(IsUsableClip).ToArray()
                : new AnimationClip[0];

            if (!loaded || squirrelClips.Length == 0)
            {
                TryInstallGeneratedSquirrelFallback(
                    $"Animated squirrel GLTF/GLB was unavailable. Loaded: {loaded}, clips found: {squirrelClips.Length}. {fallbackWarning}");
                return;
            }

            int installed = 0;
            foreach (SquirrelAgent squirrel in Object.FindObjectsByType<SquirrelAgent>(FindObjectsInactive.Exclude))
            {
                if (await InstallGltfRiggedVisual(
                    squirrel.transform,
                    gltf,
                    squirrelClips,
                    SquirrelVisualName,
                    targetHeight: 0.65f,
                    walkSpeed: 0.35f,
                    runSpeed: 3.0f,
                    clearance: 0.06f,
                    walkPlaybackSpeed: 1.1f,
                    runPlaybackSpeed: 1.25f,
                    idleFadeOutSpeed01: 0.2f,
                    runBlendStartSpeed01: 0.55f,
                    applyFoxPalette: false,
                    runKeywords: new[] { "run", "gallop", "sprint", "move", "moving" },
                    runOnlyClipAcrossStates: true))
                {
                    installed++;
                }
            }

            Debug.Log($"Animated squirrel GLTF/GLB installer added {installed} visual(s). File: {Path.GetFileName(gltfPath)}. Clips: {string.Join(", ", squirrelClips.Select(clip => clip.name))}");
        }
        catch (System.Exception exception)
        {
            TryInstallGeneratedSquirrelFallback($"Animated squirrel visual GLTF/GLB install failed: {exception.Message}. {fallbackWarning}");
        }
    }

    private static void TryInstallGeneratedSquirrelFallback(string warning)
    {
        Debug.Log(warning);
        if (!EnableGeneratedSquirrelFallback)
            return;

        InstallProceduralSquirrelVisuals();
    }

    private static void InstallProceduralSquirrelVisuals()
    {
        int installed = 0;
        foreach (SquirrelAgent squirrel in Object.FindObjectsByType<SquirrelAgent>(FindObjectsInactive.Exclude))
        {
            if (InstallProceduralSquirrelVisual(squirrel.transform))
                installed++;
        }

        Debug.Log($"Generated visual-only squirrel rig installer added {installed} visual(s). States: Idle, Walk, Run.");
    }

    private static bool InstallProceduralSquirrelVisual(Transform root)
    {
        if (root == null)
            return false;

        Transform existing = root.Find(SquirrelVisualName);
        if (existing != null)
        {
            FitVisualToRoot(existing, root, 0.82f);
            HideLegacyRenderers(root, existing);
            return false;
        }

        GameObject holder = new GameObject(SquirrelVisualName);
        holder.transform.SetParent(root, false);
        holder.transform.localPosition = Vector3.zero;
        holder.transform.localRotation = Quaternion.identity;
        holder.transform.localScale = Vector3.one;

        BuildProceduralSquirrelRig(holder.transform);
        DisableImportedPhysics(holder);
        FitVisualToRoot(holder.transform, root, 0.82f);
        HideLegacyRenderers(root, holder.transform);

        ProceduralSquirrelAnimationDriver driver = holder.AddComponent<ProceduralSquirrelAnimationDriver>();
        driver.rootRigidbody = root.GetComponent<Rigidbody>();
        driver.walkSpeed = 0.55f;
        driver.runSpeed = 2.85f;
        driver.visualGroundClearance = 0.025f;

        return true;
    }

    private static bool InstallRiggedVisual(
        Transform root,
        GameObject prefab,
        AnimationClip[] clips,
        string visualName,
        float targetHeight,
        float walkSpeed,
        float runSpeed,
        float clearance,
        float walkPlaybackSpeed,
        float runPlaybackSpeed,
        float idleFadeOutSpeed01,
        float runBlendStartSpeed01,
        bool applyFoxPalette,
        string[] runKeywords,
        bool runOnlyClipAcrossStates = false)
    {
        if (root == null || prefab == null)
            return false;

        if (TryRefreshExistingVisual(root, visualName, targetHeight, applyFoxPalette))
            return false;

        AnimationClip idle;
        AnimationClip walk;
        AnimationClip run = PickClip(clips, runKeywords);
        if (runOnlyClipAcrossStates)
        {
            AnimationClip runOnlyClip = run
                ?? PickClip(clips, "walk", "wander", "locomotion", "action")
                ?? clips.FirstOrDefault();
            idle = runOnlyClip;
            walk = runOnlyClip;
            run = runOnlyClip;
        }
        else
        {
            idle = PickClip(clips, "idle_2", "idle", "rest") ?? PickClip(clips, "walk", "gallop", "run");
            walk = PickClip(clips, "walk", "wander") ?? idle;
            run = run ?? walk;
        }

        if (idle == null && walk == null && run == null)
            return false;

        GameObject holder = new GameObject(visualName);
        holder.transform.SetParent(root, false);
        holder.transform.localPosition = Vector3.zero;
        holder.transform.localRotation = Quaternion.identity;
        holder.transform.localScale = Vector3.one;

        GameObject model = Object.Instantiate(prefab, holder.transform);
        model.name = ImportedModelChildName;
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        model.transform.localScale = Vector3.one;

        EnsureAnimator(model);
        DisableImportedPhysics(model);
        ConfigureSkinnedRenderers(model);
        if (applyFoxPalette)
            ApplyFoxPalette(model);
        AddPaletteEnforcerIfNeeded(holder, applyFoxPalette);
        FitVisualToRoot(holder.transform, root, targetHeight);
        HideLegacyRenderers(root, holder.transform);

        PlayableCreatureAnimationDriver driver = holder.GetComponent<PlayableCreatureAnimationDriver>();
        if (driver == null)
            driver = holder.AddComponent<PlayableCreatureAnimationDriver>();

        driver.rootRigidbody = root.GetComponent<Rigidbody>();
        driver.idleClip = idle;
        driver.walkClip = walk != null ? walk : idle;
        driver.runClip = run != null ? run : walk;
        driver.walkSpeed = walkSpeed;
        driver.runSpeed = runSpeed;
        driver.idlePlaybackSpeed = runOnlyClipAcrossStates ? runPlaybackSpeed : driver.idlePlaybackSpeed;
        driver.walkPlaybackSpeed = runOnlyClipAcrossStates ? runPlaybackSpeed : walkPlaybackSpeed;
        driver.runPlaybackSpeed = runPlaybackSpeed;
        if (runOnlyClipAcrossStates)
            driver.dynamicPlaybackInfluence = 0f;
        driver.idleFadeOutSpeed01 = idleFadeOutSpeed01;
        driver.runBlendStartSpeed01 = runBlendStartSpeed01;
        driver.keepVisualAboveGround = true;
        driver.visualGroundClearance = clearance;
        driver.RebuildAnimationGraph();

        return true;
    }

    private static async System.Threading.Tasks.Task<bool> InstallGltfRiggedVisual(
        Transform root,
        GltfImport gltf,
        AnimationClip[] clips,
        string visualName,
        float targetHeight,
        float walkSpeed,
        float runSpeed,
        float clearance,
        float walkPlaybackSpeed,
        float runPlaybackSpeed,
        float idleFadeOutSpeed01,
        float runBlendStartSpeed01,
        bool applyFoxPalette,
        string[] runKeywords,
        bool runOnlyClipAcrossStates = false)
    {
        if (root == null || gltf == null)
            return false;

        if (TryRefreshExistingVisual(root, visualName, targetHeight, applyFoxPalette))
            return false;

        AnimationClip idle;
        AnimationClip walk;
        AnimationClip run = PickClip(clips, runKeywords);
        if (runOnlyClipAcrossStates)
        {
            AnimationClip runOnlyClip = run
                ?? PickClip(clips, "walk", "wander", "locomotion", "action")
                ?? clips.FirstOrDefault();
            idle = runOnlyClip;
            walk = runOnlyClip;
            run = runOnlyClip;
        }
        else
        {
            idle = PickClip(clips, "idle_2", "idle", "rest") ?? PickClip(clips, "walk", "gallop", "run");
            walk = PickClip(clips, "walk", "wander") ?? idle;
            run = run ?? walk;
        }

        if (idle == null && walk == null && run == null)
            return false;

        GameObject holder = new GameObject(visualName);
        holder.transform.SetParent(root, false);
        holder.transform.localPosition = Vector3.zero;
        holder.transform.localRotation = Quaternion.identity;
        holder.transform.localScale = Vector3.one;

        bool instantiated;
        try
        {
            instantiated = await gltf.InstantiateMainSceneAsync(holder.transform);
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning($"Animated rigged visual '{visualName}' could not instantiate: {exception.Message}");
            Object.Destroy(holder);
            return false;
        }

        if (!instantiated)
        {
            Object.Destroy(holder);
            return false;
        }

        if (root == null)
        {
            Object.Destroy(holder);
            return false;
        }

        EnsureAnimator(holder);
        DisableImportedPhysics(holder);
        ConfigureSkinnedRenderers(holder);
        if (applyFoxPalette)
            ApplyFoxPalette(holder);
        AddPaletteEnforcerIfNeeded(holder, applyFoxPalette);
        FitVisualToRoot(holder.transform, root, targetHeight);
        HideLegacyRenderers(root, holder.transform);

        PlayableCreatureAnimationDriver driver = holder.GetComponent<PlayableCreatureAnimationDriver>();
        if (driver == null)
            driver = holder.AddComponent<PlayableCreatureAnimationDriver>();

        driver.rootRigidbody = root.GetComponent<Rigidbody>();
        driver.idleClip = idle;
        driver.walkClip = walk != null ? walk : idle;
        driver.runClip = run != null ? run : walk;
        driver.walkSpeed = walkSpeed;
        driver.runSpeed = runSpeed;
        driver.idlePlaybackSpeed = runOnlyClipAcrossStates ? runPlaybackSpeed : driver.idlePlaybackSpeed;
        driver.walkPlaybackSpeed = runOnlyClipAcrossStates ? runPlaybackSpeed : walkPlaybackSpeed;
        driver.runPlaybackSpeed = runPlaybackSpeed;
        if (runOnlyClipAcrossStates)
            driver.dynamicPlaybackInfluence = 0f;
        driver.idleFadeOutSpeed01 = idleFadeOutSpeed01;
        driver.runBlendStartSpeed01 = runBlendStartSpeed01;
        driver.keepVisualAboveGround = true;
        driver.visualGroundClearance = clearance;
        driver.RebuildAnimationGraph();

        return true;
    }

    private static void BuildProceduralSquirrelRig(Transform holder)
    {
        Material orange = CreatePresentationMaterial("GeneratedSquirrel_Orange", SquirrelOrange, 0.38f);
        Material cream = CreatePresentationMaterial("GeneratedSquirrel_Cream", SquirrelCream, 0.42f);
        Material dark = CreatePresentationMaterial("GeneratedSquirrel_Dark", SquirrelDark, 0.35f);
        Material nose = CreatePresentationMaterial("GeneratedSquirrel_Nose", SquirrelNose, 0.3f);

        GameObject rig = new GameObject("GeneratedRig");
        rig.transform.SetParent(holder, false);
        rig.transform.localPosition = Vector3.zero;
        rig.transform.localRotation = Quaternion.identity;
        rig.transform.localScale = Vector3.one;

        Transform body = CreateRigJoint("Body", rig.transform, new Vector3(0f, 0.34f, 0.02f), Quaternion.Euler(-7f, 0f, 0f));
        Transform head = CreateRigJoint("Head", rig.transform, new Vector3(0f, 0.59f, 0.35f), Quaternion.identity);
        Transform tailRoot = CreateRigJoint("TailRoot", rig.transform, new Vector3(0f, 0.35f, -0.35f), Quaternion.Euler(-58f, 0f, 0f));
        Transform tailBase = CreateRigJoint("TailBase", tailRoot, new Vector3(0f, 0.15f, 0f), Quaternion.Euler(-12f, 0f, 0f));
        Transform tailMid = CreateRigJoint("TailMid", tailRoot, new Vector3(0f, 0.39f, 0.075f), Quaternion.Euler(20f, 0f, 0f));
        Transform tailTip = CreateRigJoint("TailTip", tailRoot, new Vector3(0f, 0.68f, 0.15f), Quaternion.Euler(34f, 0f, 0f));

        Transform frontLeftLeg = CreateRigJoint("FrontLeftLeg", rig.transform, new Vector3(-0.16f, 0.29f, 0.26f), Quaternion.Euler(42f, 0f, 8f));
        Transform frontRightLeg = CreateRigJoint("FrontRightLeg", rig.transform, new Vector3(0.16f, 0.29f, 0.26f), Quaternion.Euler(42f, 0f, -8f));
        Transform rearLeftLeg = CreateRigJoint("RearLeftLeg", rig.transform, new Vector3(-0.19f, 0.31f, -0.19f), Quaternion.Euler(-34f, 0f, 7f));
        Transform rearRightLeg = CreateRigJoint("RearRightLeg", rig.transform, new Vector3(0.19f, 0.31f, -0.19f), Quaternion.Euler(-34f, 0f, -7f));
        Transform frontLeftPaw = CreateRigJoint("FrontLeftPaw", rig.transform, new Vector3(-0.16f, 0.045f, 0.39f), Quaternion.Euler(0f, -8f, 0f));
        Transform frontRightPaw = CreateRigJoint("FrontRightPaw", rig.transform, new Vector3(0.16f, 0.045f, 0.39f), Quaternion.Euler(0f, 8f, 0f));
        Transform rearLeftPaw = CreateRigJoint("RearLeftPaw", rig.transform, new Vector3(-0.2f, 0.047f, -0.36f), Quaternion.Euler(0f, -5f, 0f));
        Transform rearRightPaw = CreateRigJoint("RearRightPaw", rig.transform, new Vector3(0.2f, 0.047f, -0.36f), Quaternion.Euler(0f, 5f, 0f));

        Transform[] bones =
        {
            body,
            head,
            tailRoot,
            tailBase,
            tailMid,
            tailTip,
            frontLeftLeg,
            frontRightLeg,
            rearLeftLeg,
            rearRightLeg,
            frontLeftPaw,
            frontRightPaw,
            rearLeftPaw,
            rearRightPaw
        };

        GameObject meshObject = new GameObject("ContinuousSkinnedSquirrelMesh");
        meshObject.transform.SetParent(rig.transform, false);
        meshObject.transform.localPosition = Vector3.zero;
        meshObject.transform.localRotation = Quaternion.identity;
        meshObject.transform.localScale = Vector3.one;

        SkinnedMeshRenderer renderer = meshObject.AddComponent<SkinnedMeshRenderer>();
        renderer.sharedMesh = CreateContinuousSquirrelSkinnedMesh(meshObject.transform, bones, out Bounds meshBounds);
        renderer.bones = bones;
        renderer.rootBone = rig.transform;
        renderer.sharedMaterials = new[] { orange, cream, dark, nose };
        renderer.localBounds = meshBounds;
        renderer.updateWhenOffscreen = true;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        renderer.receiveShadows = true;
    }

    private const int SquirrelMaterialOrange = 0;
    private const int SquirrelMaterialCream = 1;
    private const int SquirrelMaterialDark = 2;
    private const int SquirrelMaterialNose = 3;

    private const int SquirrelBoneBody = 0;
    private const int SquirrelBoneHead = 1;
    private const int SquirrelBoneTailRoot = 2;
    private const int SquirrelBoneTailBase = 3;
    private const int SquirrelBoneTailMid = 4;
    private const int SquirrelBoneTailTip = 5;
    private const int SquirrelBoneFrontLeftLeg = 6;
    private const int SquirrelBoneFrontRightLeg = 7;
    private const int SquirrelBoneRearLeftLeg = 8;
    private const int SquirrelBoneRearRightLeg = 9;
    private const int SquirrelBoneFrontLeftPaw = 10;
    private const int SquirrelBoneFrontRightPaw = 11;
    private const int SquirrelBoneRearLeftPaw = 12;
    private const int SquirrelBoneRearRightPaw = 13;

    private struct SquirrelImplicitPrimitive
    {
        public readonly Vector3 center;
        public readonly Vector3 radius;
        public readonly Quaternion rotation;
        public readonly int materialIndex;
        public readonly int boneIndex;
        public readonly float strength;

        public SquirrelImplicitPrimitive(
            Vector3 center,
            Vector3 radius,
            Quaternion rotation,
            int materialIndex,
            int boneIndex,
            float strength = 1f)
        {
            this.center = center;
            this.radius = radius;
            this.rotation = rotation;
            this.materialIndex = materialIndex;
            this.boneIndex = boneIndex;
            this.strength = strength;
        }
    }

    private struct SquirrelFieldSample
    {
        public readonly float value;
        public readonly int materialIndex;
        public readonly int boneIndex;

        public SquirrelFieldSample(float value, int materialIndex, int boneIndex)
        {
            this.value = value;
            this.materialIndex = materialIndex;
            this.boneIndex = boneIndex;
        }
    }

    private static Mesh CreateContinuousSquirrelSkinnedMesh(
        Transform meshTransform,
        Transform[] bones,
        out Bounds localBounds)
    {
        SquirrelImplicitPrimitive[] primitives = CreateSquirrelImplicitPrimitives();
        List<Vector3> vertices = new List<Vector3>(24000);
        List<BoneWeight> boneWeights = new List<BoneWeight>(24000);
        List<int>[] submeshTriangles =
        {
            new List<int>(24000),
            new List<int>(8000),
            new List<int>(12000),
            new List<int>(2000)
        };

        Bounds samplingBounds = new Bounds(new Vector3(0f, 0.43f, -0.03f), new Vector3(0.92f, 0.96f, 1.34f));
        Vector3 min = samplingBounds.min;
        Vector3 size = samplingBounds.size;
        const int xResolution = 25;
        const int yResolution = 29;
        const int zResolution = 35;
        Vector3 step = new Vector3(
            size.x / (xResolution - 1),
            size.y / (yResolution - 1),
            size.z / (zResolution - 1));

        Vector3[] cubePoints = new Vector3[8];
        SquirrelFieldSample[] cubeSamples = new SquirrelFieldSample[8];
        int[,] tetrahedra =
        {
            { 0, 5, 1, 6 },
            { 0, 1, 2, 6 },
            { 0, 2, 3, 6 },
            { 0, 3, 7, 6 },
            { 0, 7, 4, 6 },
            { 0, 4, 5, 6 }
        };

        for (int x = 0; x < xResolution - 1; x++)
        {
            for (int y = 0; y < yResolution - 1; y++)
            {
                for (int z = 0; z < zResolution - 1; z++)
                {
                    FillSquirrelCubeSamples(
                        x,
                        y,
                        z,
                        min,
                        step,
                        primitives,
                        cubePoints,
                        cubeSamples);

                    for (int tetra = 0; tetra < 6; tetra++)
                    {
                        PolygonizeSquirrelTetra(
                            tetrahedra[tetra, 0],
                            tetrahedra[tetra, 1],
                            tetrahedra[tetra, 2],
                            tetrahedra[tetra, 3],
                            cubePoints,
                            cubeSamples,
                            primitives,
                            vertices,
                            boneWeights,
                            submeshTriangles);
                    }
                }
            }
        }

        Mesh mesh = new Mesh
        {
            name = "GeneratedContinuousSkinnedSquirrelMesh",
            hideFlags = HideFlags.DontSave,
            indexFormat = UnityEngine.Rendering.IndexFormat.UInt32
        };
        mesh.SetVertices(vertices);
        mesh.subMeshCount = submeshTriangles.Length;
        for (int i = 0; i < submeshTriangles.Length; i++)
            mesh.SetTriangles(submeshTriangles[i], i);

        mesh.boneWeights = boneWeights.ToArray();
        Matrix4x4 meshLocalToWorld = meshTransform.localToWorldMatrix;
        Matrix4x4[] bindPoses = new Matrix4x4[bones.Length];
        for (int i = 0; i < bones.Length; i++)
            bindPoses[i] = bones[i].worldToLocalMatrix * meshLocalToWorld;
        mesh.bindposes = bindPoses;

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        localBounds = mesh.bounds;
        localBounds.Expand(new Vector3(0.16f, 0.16f, 0.16f));
        return mesh;
    }

    private static SquirrelImplicitPrimitive[] CreateSquirrelImplicitPrimitives()
    {
        return new[]
        {
            new SquirrelImplicitPrimitive(new Vector3(0f, 0.34f, 0.02f), new Vector3(0.36f, 0.26f, 0.49f), Quaternion.Euler(-7f, 0f, 0f), SquirrelMaterialOrange, SquirrelBoneBody),
            new SquirrelImplicitPrimitive(new Vector3(0f, 0.28f, -0.2f), new Vector3(0.34f, 0.22f, 0.32f), Quaternion.Euler(4f, 0f, 0f), SquirrelMaterialOrange, SquirrelBoneBody),
            new SquirrelImplicitPrimitive(new Vector3(0f, 0.31f, 0.31f), new Vector3(0.22f, 0.18f, 0.13f), Quaternion.Euler(-10f, 0f, 0f), SquirrelMaterialCream, SquirrelBoneBody, 1.08f),
            new SquirrelImplicitPrimitive(new Vector3(0f, 0.45f, 0.29f), new Vector3(0.18f, 0.13f, 0.1f), Quaternion.Euler(-13f, 0f, 0f), SquirrelMaterialCream, SquirrelBoneBody, 1.06f),

            new SquirrelImplicitPrimitive(new Vector3(0f, 0.59f, 0.35f), new Vector3(0.24f, 0.2f, 0.23f), Quaternion.Euler(-3f, 0f, 0f), SquirrelMaterialOrange, SquirrelBoneHead),
            new SquirrelImplicitPrimitive(new Vector3(0f, 0.56f, 0.48f), new Vector3(0.15f, 0.095f, 0.12f), Quaternion.identity, SquirrelMaterialCream, SquirrelBoneHead, 1.08f),
            new SquirrelImplicitPrimitive(new Vector3(0f, 0.575f, 0.585f), new Vector3(0.052f, 0.035f, 0.035f), Quaternion.identity, SquirrelMaterialNose, SquirrelBoneHead, 1.15f),
            new SquirrelImplicitPrimitive(new Vector3(-0.082f, 0.635f, 0.49f), new Vector3(0.032f, 0.035f, 0.024f), Quaternion.identity, SquirrelMaterialNose, SquirrelBoneHead, 1.1f),
            new SquirrelImplicitPrimitive(new Vector3(0.082f, 0.635f, 0.49f), new Vector3(0.032f, 0.035f, 0.024f), Quaternion.identity, SquirrelMaterialNose, SquirrelBoneHead, 1.1f),
            new SquirrelImplicitPrimitive(new Vector3(-0.12f, 0.72f, 0.29f), new Vector3(0.07f, 0.12f, 0.055f), Quaternion.Euler(-12f, 0f, 20f), SquirrelMaterialDark, SquirrelBoneHead, 1.04f),
            new SquirrelImplicitPrimitive(new Vector3(0.12f, 0.72f, 0.29f), new Vector3(0.07f, 0.12f, 0.055f), Quaternion.Euler(-12f, 0f, -20f), SquirrelMaterialDark, SquirrelBoneHead, 1.04f),

            new SquirrelImplicitPrimitive(new Vector3(0f, 0.43f, -0.37f), new Vector3(0.17f, 0.22f, 0.15f), Quaternion.Euler(-58f, 0f, 0f), SquirrelMaterialOrange, SquirrelBoneTailRoot),
            new SquirrelImplicitPrimitive(new Vector3(0f, 0.56f, -0.37f), new Vector3(0.19f, 0.28f, 0.16f), Quaternion.Euler(-36f, 0f, 0f), SquirrelMaterialOrange, SquirrelBoneTailBase),
            new SquirrelImplicitPrimitive(new Vector3(0f, 0.72f, -0.26f), new Vector3(0.24f, 0.31f, 0.19f), Quaternion.Euler(-8f, 0f, 0f), SquirrelMaterialOrange, SquirrelBoneTailMid),
            new SquirrelImplicitPrimitive(new Vector3(0f, 0.81f, -0.08f), new Vector3(0.2f, 0.22f, 0.16f), Quaternion.Euler(22f, 0f, 0f), SquirrelMaterialCream, SquirrelBoneTailTip),

            new SquirrelImplicitPrimitive(new Vector3(-0.15f, 0.2f, 0.31f), new Vector3(0.075f, 0.19f, 0.07f), Quaternion.Euler(34f, 0f, 8f), SquirrelMaterialDark, SquirrelBoneFrontLeftLeg),
            new SquirrelImplicitPrimitive(new Vector3(0.15f, 0.2f, 0.31f), new Vector3(0.075f, 0.19f, 0.07f), Quaternion.Euler(34f, 0f, -8f), SquirrelMaterialDark, SquirrelBoneFrontRightLeg),
            new SquirrelImplicitPrimitive(new Vector3(-0.16f, 0.07f, 0.4f), new Vector3(0.1f, 0.055f, 0.14f), Quaternion.Euler(0f, -8f, 0f), SquirrelMaterialDark, SquirrelBoneFrontLeftPaw),
            new SquirrelImplicitPrimitive(new Vector3(0.16f, 0.07f, 0.4f), new Vector3(0.1f, 0.055f, 0.14f), Quaternion.Euler(0f, 8f, 0f), SquirrelMaterialDark, SquirrelBoneFrontRightPaw),

            new SquirrelImplicitPrimitive(new Vector3(-0.19f, 0.2f, -0.24f), new Vector3(0.1f, 0.2f, 0.09f), Quaternion.Euler(-30f, 0f, 7f), SquirrelMaterialDark, SquirrelBoneRearLeftLeg),
            new SquirrelImplicitPrimitive(new Vector3(0.19f, 0.2f, -0.24f), new Vector3(0.1f, 0.2f, 0.09f), Quaternion.Euler(-30f, 0f, -7f), SquirrelMaterialDark, SquirrelBoneRearRightLeg),
            new SquirrelImplicitPrimitive(new Vector3(-0.2f, 0.07f, -0.38f), new Vector3(0.12f, 0.06f, 0.16f), Quaternion.Euler(0f, -5f, 0f), SquirrelMaterialDark, SquirrelBoneRearLeftPaw),
            new SquirrelImplicitPrimitive(new Vector3(0.2f, 0.07f, -0.38f), new Vector3(0.12f, 0.06f, 0.16f), Quaternion.Euler(0f, 5f, 0f), SquirrelMaterialDark, SquirrelBoneRearRightPaw)
        };
    }

    private static void FillSquirrelCubeSamples(
        int x,
        int y,
        int z,
        Vector3 min,
        Vector3 step,
        SquirrelImplicitPrimitive[] primitives,
        Vector3[] cubePoints,
        SquirrelFieldSample[] cubeSamples)
    {
        for (int corner = 0; corner < 8; corner++)
        {
            int ox = corner == 1 || corner == 2 || corner == 5 || corner == 6 ? 1 : 0;
            int oy = corner == 2 || corner == 3 || corner == 6 || corner == 7 ? 1 : 0;
            int oz = corner >= 4 ? 1 : 0;
            Vector3 point = min + new Vector3((x + ox) * step.x, (y + oy) * step.y, (z + oz) * step.z);
            cubePoints[corner] = point;
            cubeSamples[corner] = SampleSquirrelField(point, primitives);
        }
    }

    private static void PolygonizeSquirrelTetra(
        int a,
        int b,
        int c,
        int d,
        Vector3[] cubePoints,
        SquirrelFieldSample[] cubeSamples,
        SquirrelImplicitPrimitive[] primitives,
        List<Vector3> vertices,
        List<BoneWeight> boneWeights,
        List<int>[] submeshTriangles)
    {
        int[] ids = { a, b, c, d };
        int[] inside = new int[4];
        int[] outside = new int[4];
        int insideCount = 0;
        int outsideCount = 0;

        for (int i = 0; i < ids.Length; i++)
        {
            if (cubeSamples[ids[i]].value >= 0f)
                inside[insideCount++] = ids[i];
            else
                outside[outsideCount++] = ids[i];
        }

        if (insideCount == 0 || insideCount == 4)
            return;

        if (insideCount == 1)
        {
            Vector3 p0 = InterpolateSquirrelSurface(inside[0], outside[0], cubePoints, cubeSamples);
            Vector3 p1 = InterpolateSquirrelSurface(inside[0], outside[1], cubePoints, cubeSamples);
            Vector3 p2 = InterpolateSquirrelSurface(inside[0], outside[2], cubePoints, cubeSamples);
            AddSquirrelSurfaceTriangle(p0, p1, p2, primitives, vertices, boneWeights, submeshTriangles);
            return;
        }

        if (insideCount == 3)
        {
            Vector3 p0 = InterpolateSquirrelSurface(outside[0], inside[0], cubePoints, cubeSamples);
            Vector3 p1 = InterpolateSquirrelSurface(outside[0], inside[1], cubePoints, cubeSamples);
            Vector3 p2 = InterpolateSquirrelSurface(outside[0], inside[2], cubePoints, cubeSamples);
            AddSquirrelSurfaceTriangle(p0, p1, p2, primitives, vertices, boneWeights, submeshTriangles);
            return;
        }

        Vector3 q0 = InterpolateSquirrelSurface(inside[0], outside[0], cubePoints, cubeSamples);
        Vector3 q1 = InterpolateSquirrelSurface(inside[0], outside[1], cubePoints, cubeSamples);
        Vector3 q2 = InterpolateSquirrelSurface(inside[1], outside[0], cubePoints, cubeSamples);
        Vector3 q3 = InterpolateSquirrelSurface(inside[1], outside[1], cubePoints, cubeSamples);
        AddSquirrelSurfaceTriangle(q0, q1, q2, primitives, vertices, boneWeights, submeshTriangles);
        AddSquirrelSurfaceTriangle(q2, q1, q3, primitives, vertices, boneWeights, submeshTriangles);
    }

    private static Vector3 InterpolateSquirrelSurface(
        int first,
        int second,
        Vector3[] cubePoints,
        SquirrelFieldSample[] cubeSamples)
    {
        float firstValue = cubeSamples[first].value;
        float secondValue = cubeSamples[second].value;
        float t = firstValue / Mathf.Max(0.0001f, firstValue - secondValue);
        return Vector3.Lerp(cubePoints[first], cubePoints[second], Mathf.Clamp01(t));
    }

    private static void AddSquirrelSurfaceTriangle(
        Vector3 a,
        Vector3 b,
        Vector3 c,
        SquirrelImplicitPrimitive[] primitives,
        List<Vector3> vertices,
        List<BoneWeight> boneWeights,
        List<int>[] submeshTriangles)
    {
        Vector3 centroid = (a + b + c) / 3f;
        Vector3 normal = Vector3.Cross(b - a, c - a);
        Vector3 gradient = EstimateSquirrelFieldGradient(centroid, primitives);
        if (Vector3.Dot(normal, gradient) > 0f)
        {
            Vector3 swap = b;
            b = c;
            c = swap;
        }

        int materialIndex = Mathf.Clamp(SampleSquirrelField(centroid, primitives).materialIndex, 0, submeshTriangles.Length - 1);
        int vertexIndex = vertices.Count;
        vertices.Add(a);
        vertices.Add(b);
        vertices.Add(c);
        boneWeights.Add(CreateSquirrelBoneWeight(a, primitives));
        boneWeights.Add(CreateSquirrelBoneWeight(b, primitives));
        boneWeights.Add(CreateSquirrelBoneWeight(c, primitives));
        submeshTriangles[materialIndex].Add(vertexIndex);
        submeshTriangles[materialIndex].Add(vertexIndex + 1);
        submeshTriangles[materialIndex].Add(vertexIndex + 2);
    }

    private static SquirrelFieldSample SampleSquirrelField(Vector3 point, SquirrelImplicitPrimitive[] primitives)
    {
        float bestValue = float.NegativeInfinity;
        int bestMaterial = SquirrelMaterialOrange;
        int bestBone = SquirrelBoneBody;

        for (int i = 0; i < primitives.Length; i++)
        {
            SquirrelImplicitPrimitive primitive = primitives[i];
            Vector3 local = Quaternion.Inverse(primitive.rotation) * (point - primitive.center);
            Vector3 radius = primitive.radius;
            float normalized =
                local.x * local.x / Mathf.Max(0.0001f, radius.x * radius.x) +
                local.y * local.y / Mathf.Max(0.0001f, radius.y * radius.y) +
                local.z * local.z / Mathf.Max(0.0001f, radius.z * radius.z);
            float value = primitive.strength - normalized;
            if (value > bestValue)
            {
                bestValue = value;
                bestMaterial = primitive.materialIndex;
                bestBone = primitive.boneIndex;
            }
        }

        return new SquirrelFieldSample(bestValue, bestMaterial, bestBone);
    }

    private static Vector3 EstimateSquirrelFieldGradient(Vector3 point, SquirrelImplicitPrimitive[] primitives)
    {
        const float epsilon = 0.01f;
        float dx = SampleSquirrelField(point + Vector3.right * epsilon, primitives).value -
            SampleSquirrelField(point - Vector3.right * epsilon, primitives).value;
        float dy = SampleSquirrelField(point + Vector3.up * epsilon, primitives).value -
            SampleSquirrelField(point - Vector3.up * epsilon, primitives).value;
        float dz = SampleSquirrelField(point + Vector3.forward * epsilon, primitives).value -
            SampleSquirrelField(point - Vector3.forward * epsilon, primitives).value;
        Vector3 gradient = new Vector3(dx, dy, dz);
        return gradient.sqrMagnitude > 0.0001f ? gradient.normalized : Vector3.up;
    }

    private static BoneWeight CreateSquirrelBoneWeight(Vector3 point, SquirrelImplicitPrimitive[] primitives)
    {
        SquirrelFieldSample sample = SampleSquirrelField(point, primitives);
        int primaryBone = Mathf.Clamp(sample.boneIndex, 0, SquirrelBoneRearRightPaw);
        int secondaryBone = SquirrelBoneBody;
        float primaryWeight = 1f;

        if (primaryBone == SquirrelBoneHead)
        {
            primaryWeight = Mathf.SmoothStep(0.55f, 1f, Mathf.InverseLerp(0.28f, 0.5f, point.z));
        }
        else if (primaryBone >= SquirrelBoneTailRoot && primaryBone <= SquirrelBoneTailTip)
        {
            secondaryBone = primaryBone == SquirrelBoneTailRoot ? SquirrelBoneBody : primaryBone - 1;
            primaryWeight = Mathf.SmoothStep(0.65f, 1f, Mathf.InverseLerp(-0.43f, -0.08f, point.z) + Mathf.InverseLerp(0.38f, 0.82f, point.y) * 0.35f);
        }
        else if (primaryBone >= SquirrelBoneFrontLeftLeg)
        {
            bool isPaw = primaryBone >= SquirrelBoneFrontLeftPaw;
            secondaryBone = isPaw
                ? (primaryBone == SquirrelBoneFrontLeftPaw ? SquirrelBoneFrontLeftLeg :
                   primaryBone == SquirrelBoneFrontRightPaw ? SquirrelBoneFrontRightLeg :
                   primaryBone == SquirrelBoneRearLeftPaw ? SquirrelBoneRearLeftLeg :
                   SquirrelBoneRearRightLeg)
                : SquirrelBoneBody;
            primaryWeight = isPaw
                ? Mathf.SmoothStep(0.7f, 1f, Mathf.InverseLerp(0.16f, 0.04f, point.y))
                : Mathf.SmoothStep(0.62f, 1f, Mathf.InverseLerp(0.28f, 0.12f, point.y));
        }

        primaryWeight = Mathf.Clamp01(primaryWeight);
        BoneWeight weight = new BoneWeight
        {
            boneIndex0 = primaryBone,
            weight0 = primaryWeight,
            boneIndex1 = secondaryBone,
            weight1 = 1f - primaryWeight
        };
        return weight;
    }

    private static Transform CreateRigJoint(string name, Transform parent, Vector3 localPosition, Quaternion localRotation)
    {
        GameObject joint = new GameObject(name);
        joint.transform.SetParent(parent, false);
        joint.transform.localPosition = localPosition;
        joint.transform.localRotation = localRotation;
        joint.transform.localScale = Vector3.one;
        return joint.transform;
    }

    private static Material CreatePresentationMaterial(string name, Color color, float smoothness)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        Material material = new Material(shader);
        material.name = name;
        material.hideFlags = HideFlags.DontSave;
        material.enableInstancing = true;
        SetMaterialColor(material, color);
        SetMaterialFloatIfPresent(material, "_Metallic", 0f);
        SetMaterialFloatIfPresent(material, "_Smoothness", smoothness);
        SetMaterialFloatIfPresent(material, "_Glossiness", smoothness);
        SetMaterialFloatIfPresent(material, "_Cull", 0f);
        return material;
    }

    private static void EnsureAnimator(GameObject visual)
    {
        Animator animator = visual.GetComponentInChildren<Animator>(true);
        if (animator == null)
            animator = visual.AddComponent<Animator>();

        animator.applyRootMotion = false;
    }

    private static void ConfigureSkinnedRenderers(GameObject visual)
    {
        foreach (SkinnedMeshRenderer renderer in visual.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            renderer.updateWhenOffscreen = true;
    }

    private static void DisableImportedPhysics(GameObject visual)
    {
        foreach (Rigidbody body in visual.GetComponentsInChildren<Rigidbody>(true))
        {
            body.isKinematic = true;
            body.detectCollisions = false;
        }

        foreach (Collider collider in visual.GetComponentsInChildren<Collider>(true))
            collider.enabled = false;
    }

    private static bool TryRefreshExistingVisual(
        Transform root,
        string visualName,
        float targetHeight,
        bool applyFoxPalette)
    {
        Transform existing = root.Find(visualName);
        if (existing == null)
            return false;

        if (applyFoxPalette)
        {
            ApplyFoxPalette(existing.gameObject);
            AddPaletteEnforcerIfNeeded(existing.gameObject, true);
        }

        FitVisualToRoot(existing, root, targetHeight);
        HideLegacyRenderers(root, existing);
        return true;
    }

    private static void AddPaletteEnforcerIfNeeded(GameObject holder, bool applyFoxPalette)
    {
        if (!applyFoxPalette || holder == null)
            return;

        if (holder.GetComponent<FoxPaletteRuntimeEnforcer>() == null)
            holder.AddComponent<FoxPaletteRuntimeEnforcer>();
    }

    public static void ApplyFoxPalette(GameObject visual)
    {
        foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] == null)
                    continue;

                Color targetColor = GetFoxPaletteColor(materials[i].name, i);
                materials[i] = CreateFoxPaletteMaterial(materials[i], targetColor);
                ApplySubmeshColorOverride(renderer, i, targetColor);
            }

            renderer.materials = materials;
        }
    }

    private static Material CreateFoxPaletteMaterial(Material source, Color color)
    {
        if (source != null && source.name.Contains(FoxPaletteMaterialSuffix))
        {
            SetMaterialTextureIfPresent(source, "_BaseMap", null);
            SetMaterialTextureIfPresent(source, "_MainTex", null);
            SetMaterialTextureIfPresent(source, "_MetallicGlossMap", null);
            SetMaterialColor(source, color);
            SetMaterialFloatIfPresent(source, "_Metallic", 0f);
            SetMaterialFloatIfPresent(source, "_Smoothness", 0.45f);
            SetMaterialFloatIfPresent(source, "_Glossiness", 0.45f);
            return source;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        if (shader == null && source != null)
            shader = source.shader;

        Material material = shader != null
            ? new Material(shader)
            : Object.Instantiate(source);

        string sourceName = source != null ? source.name : "Material";
        material.name = sourceName + FoxPaletteMaterialSuffix;
        material.hideFlags = HideFlags.DontSave;
        material.enableInstancing = true;

        SetMaterialTextureIfPresent(material, "_BaseMap", null);
        SetMaterialTextureIfPresent(material, "_MainTex", null);
        SetMaterialTextureIfPresent(material, "_MetallicGlossMap", null);
        SetMaterialColor(material, color);
        SetMaterialFloatIfPresent(material, "_Metallic", 0f);
        SetMaterialFloatIfPresent(material, "_Smoothness", 0.45f);
        SetMaterialFloatIfPresent(material, "_Glossiness", 0.45f);
        SetMaterialFloatIfPresent(material, "_AlphaClip", 0f);

        return material;
    }

    private static Color GetFoxPaletteColor(string materialName, int materialIndex)
    {
        string lower = materialName != null ? materialName.ToLowerInvariant() : string.Empty;
        if (lower.Contains("black") || lower.Contains("eye") || lower.Contains("grey") || materialIndex >= 2)
            return FoxBlack;

        if (lower.Contains("light") || materialIndex == 1)
            return FoxWarmWhite;

        return FoxOrange;
    }

    private static void SetMaterialColor(Material material, Color color)
    {
        if (material == null)
            return;

        if (material.HasProperty(BaseColorId))
            material.SetColor(BaseColorId, color);

        if (material.HasProperty(ColorId))
            material.SetColor(ColorId, color);
    }

    private static void SetMaterialFloatIfPresent(Material material, string propertyName, float value)
    {
        if (material != null && material.HasProperty(propertyName))
            material.SetFloat(propertyName, value);
    }

    private static void SetMaterialTextureIfPresent(Material material, string propertyName, Texture texture)
    {
        if (material != null && material.HasProperty(propertyName))
            material.SetTexture(propertyName, texture);
    }

    private static void ApplySubmeshColorOverride(Renderer renderer, int materialIndex, Color color)
    {
        if (renderer == null)
            return;

        MaterialPropertyBlock block = new MaterialPropertyBlock();
        block.SetColor(BaseColorId, color);
        block.SetColor(ColorId, color);
        renderer.SetPropertyBlock(block, materialIndex);
    }

    private static void HideLegacyRenderers(Transform root, Transform replacementVisual)
    {
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer.transform.IsChildOf(replacementVisual))
                continue;

            if (IsLegacyAnimalRenderer(renderer.transform, root))
                renderer.enabled = false;
        }
    }

    private static bool IsLegacyAnimalRenderer(Transform rendererTransform, Transform agentRoot)
    {
        if (rendererTransform == null || agentRoot == null || rendererTransform == agentRoot)
            return false;

        Transform visualRoot = rendererTransform;
        while (visualRoot.parent != null && visualRoot.parent != agentRoot)
            visualRoot = visualRoot.parent;

        string name = $"{visualRoot.name} {rendererTransform.name}".ToLowerInvariant();
        return name.Contains("visual") ||
            name.Contains("fox") ||
            name.Contains("squirrel") ||
            name.Contains("low_poly") ||
            name.Contains("lowpoly");
    }

    private static void FitVisualToRoot(Transform visual, Transform root, float targetHeight)
    {
        Bounds bounds = CalculateBounds(visual);
        if (bounds.size.y > 0.001f)
        {
            float scale = targetHeight / bounds.size.y;
            visual.localScale *= scale;
        }

        bounds = CalculateBounds(visual);
        float bottomOffset = root.position.y - bounds.min.y;
        visual.position += Vector3.up * bottomOffset;
    }

    private static Bounds CalculateBounds(Transform root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (VisualGroundingUtility.TryGetStableVisualBounds(root, renderers, out Bounds stableBounds))
            return stableBounds;

        if (VisualGroundingUtility.TryGetRendererBounds(renderers, out Bounds bounds))
            return bounds;

        return new Bounds(root.position, Vector3.one);
    }

    private static AnimationClip PickClip(AnimationClip[] clips, params string[] keywords)
    {
        foreach (string keyword in keywords)
        {
            AnimationClip clip = clips.FirstOrDefault(candidate =>
            {
                string lower = candidate.name.ToLowerInvariant();
                return lower.Contains(keyword) && !lower.Contains("hitreact") && !lower.Contains("death");
            });

            if (clip != null)
                return clip;
        }

        return null;
    }

    private static AnimationClip[] LoadClips(string resourcePath, string editorAssetPath)
    {
        AnimationClip[] clips = Resources.LoadAll<AnimationClip>(resourcePath)
            .Where(IsUsableClip)
            .ToArray();
        if (clips.Length > 0)
            return clips;

#if UNITY_EDITOR
        if (string.IsNullOrEmpty(editorAssetPath))
            return clips;

        return AssetDatabase
            .LoadAllAssetsAtPath(editorAssetPath)
            .OfType<AnimationClip>()
            .Where(IsUsableClip)
            .ToArray();
#else
        return clips;
#endif
    }

    private static GameObject LoadPrefab(string resourcePath, string editorAssetPath)
    {
        GameObject prefab = Resources.Load<GameObject>(resourcePath);
        if (prefab != null)
            return prefab;

#if UNITY_EDITOR
        if (!string.IsNullOrEmpty(editorAssetPath))
            return AssetDatabase.LoadAssetAtPath<GameObject>(editorAssetPath);
#endif

        return null;
    }

    private static bool IsUsableClip(AnimationClip clip)
    {
        return clip != null && !clip.name.StartsWith("__preview__", System.StringComparison.OrdinalIgnoreCase);
    }

    private static GameObject LoadFirstResourcePrefab(string resourceFolder)
    {
        return Resources
            .LoadAll<GameObject>(resourceFolder)
            .OrderBy(candidate => candidate.name)
            .FirstOrDefault();
    }

    private static string FindFirstModelFileInResources(string resourceFolder, params string[] extensions)
    {
        if (string.IsNullOrEmpty(resourceFolder) || extensions == null || extensions.Length == 0)
            return null;

        string relativeFolder = resourceFolder.Replace('/', Path.DirectorySeparatorChar);
        string folderPath = Path.Combine(Application.dataPath, "Resources", relativeFolder);
        if (!Directory.Exists(folderPath))
            return null;

        return Directory
            .EnumerateFiles(folderPath, "*.*", SearchOption.AllDirectories)
            .Where(path => extensions.Any(extension =>
                string.Equals(Path.GetExtension(path), extension, System.StringComparison.OrdinalIgnoreCase)))
            .OrderBy(path => path)
            .FirstOrDefault();
    }

    private static string GetResourceAssetPath(GameObject prefab)
    {
#if UNITY_EDITOR
        if (prefab == null)
            return null;

        string path = AssetDatabase.GetAssetPath(prefab);
        return string.IsNullOrEmpty(path) ? null : path;
#else
        return null;
#endif
    }
}

public class FoxPaletteRuntimeEnforcer : MonoBehaviour
{
    private void OnEnable()
    {
        RiggedVisualRuntimeInstaller.ApplyFoxPalette(gameObject);
    }

    private void Start()
    {
        RiggedVisualRuntimeInstaller.ApplyFoxPalette(gameObject);
    }

    private void LateUpdate()
    {
        RiggedVisualRuntimeInstaller.ApplyFoxPalette(gameObject);
    }
}

public class ProceduralSquirrelAnimationDriver : MonoBehaviour
{
    public Rigidbody rootRigidbody;

    [Header("Speed Mapping")]
    public float walkSpeed = 0.55f;
    public float runSpeed = 2.9f;
    public float speedDeadZone = 0.035f;
    public float speedSmoothingSharpness = 10f;
    public float runBlendStartSpeed01 = 0.58f;
    public float maxSampledSpeed = 7f;

    [Header("Clip Timing")]
    public float idleBreathRate = 1.4f;
    public float walkStrideRate = 7.6f;
    public float gallopStrideRate = 15.2f;

    [Header("Pose")]
    public float idleBreathAmount = 0.009f;
    public float walkBobAmount = 0.012f;
    public float gallopBobAmount = 0.038f;
    public float walkLegSwingAngle = 20f;
    public float gallopLegSwingAngle = 54f;
    public float walkLegLift = 0.018f;
    public float gallopLegLift = 0.058f;
    public float walkPawStrideDistance = 0.035f;
    public float gallopPawStrideDistance = 0.095f;
    public float gallopBodyStretch = 0.055f;
    public float walkTailSwayAngle = 8f;
    public float gallopTailSwayAngle = 22f;

    [Header("Grounding")]
    public bool keepVisualAboveGround = true;
    public float visualGroundClearance = 0.025f;
    public float groundProbeHeight = 2f;
    public float groundProbeDistance = 5f;
    public LayerMask groundMask = -1;
    public float groundSharpness = 14f;

    private Transform body;
    private Transform head;
    private Transform tailRoot;
    private Transform tailBase;
    private Transform tailMid;
    private Transform tailTip;
    private Transform[] legs;
    private Transform[] paws;
    private Renderer[] renderers;

    private Vector3 previousRootPosition;
    private bool hasPreviousRootPosition;
    private float measuredHorizontalSpeed;
    private float stridePhase;

    private Vector3 bodyBasePosition;
    private Quaternion bodyBaseRotation;
    private Vector3 bodyBaseScale;
    private Vector3 headBasePosition;
    private Quaternion headBaseRotation;
    private Vector3 headBaseScale;
    private Quaternion tailRootBaseRotation;
    private Quaternion tailBaseRotation;
    private Quaternion tailMidRotation;
    private Quaternion tailTipRotation;
    private Quaternion[] legBaseRotations;
    private Vector3[] pawBasePositions;
    private Quaternion[] pawBaseRotations;
    private bool cachedRig;

    private void OnEnable()
    {
        CacheRig();
        ResetSamples();
    }

    private void Update()
    {
        CacheRig();
        SampleSpeed(Time.deltaTime);
        ApplyProceduralClipPose(Time.deltaTime);
    }

    public void PreviewStep(float previewSpeed, float deltaTime)
    {
        CacheRig();
        SamplePreviewSpeed(previewSpeed, deltaTime);
        ApplyProceduralClipPose(deltaTime);
    }

    private void LateUpdate()
    {
        if (!keepVisualAboveGround)
            return;

        if (renderers == null || renderers.Length == 0)
            renderers = GetComponentsInChildren<Renderer>(true);

        VisualGroundingUtility.KeepBottomAboveGround(
            transform,
            rootRigidbody != null ? rootRigidbody.transform : transform.parent,
            renderers,
            visualGroundClearance,
            groundProbeHeight,
            groundProbeDistance,
            groundMask,
            groundSharpness);
    }

    private void CacheRig()
    {
        if (cachedRig)
            return;

        if (rootRigidbody == null)
            rootRigidbody = GetComponentInParent<Rigidbody>();

        body = FindDeepChild(transform, "Body");
        head = FindDeepChild(transform, "Head");
        tailRoot = FindDeepChild(transform, "TailRoot");
        tailBase = FindDeepChild(transform, "TailBase");
        tailMid = FindDeepChild(transform, "TailMid");
        tailTip = FindDeepChild(transform, "TailTip");
        legs = new[]
        {
            FindDeepChild(transform, "FrontLeftLeg"),
            FindDeepChild(transform, "FrontRightLeg"),
            FindDeepChild(transform, "RearLeftLeg"),
            FindDeepChild(transform, "RearRightLeg")
        };
        paws = new[]
        {
            FindDeepChild(transform, "FrontLeftPaw"),
            FindDeepChild(transform, "FrontRightPaw"),
            FindDeepChild(transform, "RearLeftPaw"),
            FindDeepChild(transform, "RearRightPaw")
        };
        renderers = GetComponentsInChildren<Renderer>(true);

        if (body == null || head == null || tailRoot == null)
            return;

        bodyBasePosition = body.localPosition;
        bodyBaseRotation = body.localRotation;
        bodyBaseScale = body.localScale;
        headBasePosition = head.localPosition;
        headBaseRotation = head.localRotation;
        headBaseScale = head.localScale;
        tailRootBaseRotation = tailRoot.localRotation;
        tailBaseRotation = tailBase != null ? tailBase.localRotation : Quaternion.identity;
        tailMidRotation = tailMid != null ? tailMid.localRotation : Quaternion.identity;
        tailTipRotation = tailTip != null ? tailTip.localRotation : Quaternion.identity;

        legBaseRotations = new Quaternion[legs.Length];
        for (int i = 0; i < legs.Length; i++)
            legBaseRotations[i] = legs[i] != null ? legs[i].localRotation : Quaternion.identity;

        pawBasePositions = new Vector3[paws.Length];
        pawBaseRotations = new Quaternion[paws.Length];
        for (int i = 0; i < paws.Length; i++)
        {
            pawBasePositions[i] = paws[i] != null ? paws[i].localPosition : Vector3.zero;
            pawBaseRotations[i] = paws[i] != null ? paws[i].localRotation : Quaternion.identity;
        }

        cachedRig = true;
    }

    private void ResetSamples()
    {
        Transform root = rootRigidbody != null ? rootRigidbody.transform : transform.parent;
        previousRootPosition = root != null ? root.position : transform.position;
        hasPreviousRootPosition = false;
        measuredHorizontalSpeed = 0f;
        stridePhase = Random.Range(0f, Mathf.PI * 2f);
    }

    private void SampleSpeed(float deltaTime)
    {
        if (deltaTime <= 0.0001f)
            return;

        Transform root = rootRigidbody != null ? rootRigidbody.transform : transform.parent;
        float velocitySpeed = 0f;
        if (rootRigidbody != null)
        {
            Vector3 velocity = rootRigidbody.linearVelocity;
            velocity.y = 0f;
            velocitySpeed = velocity.magnitude;
        }

        float positionSpeed = 0f;
        if (root != null)
        {
            Vector3 current = root.position;
            if (hasPreviousRootPosition)
            {
                Vector3 delta = current - previousRootPosition;
                delta.y = 0f;
                positionSpeed = delta.magnitude / deltaTime;
            }

            previousRootPosition = current;
            hasPreviousRootPosition = true;
        }

        float rawSpeed = Mathf.Max(velocitySpeed, positionSpeed);
        if (maxSampledSpeed > 0f)
            rawSpeed = Mathf.Min(rawSpeed, maxSampledSpeed);
        if (rawSpeed < Mathf.Max(0f, speedDeadZone))
            rawSpeed = 0f;

        float factor = DampFactor(speedSmoothingSharpness, deltaTime);
        measuredHorizontalSpeed = Mathf.Lerp(measuredHorizontalSpeed, rawSpeed, factor);
        if (rawSpeed <= 0f && measuredHorizontalSpeed < speedDeadZone * 0.5f)
            measuredHorizontalSpeed = 0f;
    }

    private void SamplePreviewSpeed(float previewSpeed, float deltaTime)
    {
        if (deltaTime <= 0.0001f)
            return;

        float rawSpeed = Mathf.Max(0f, previewSpeed);
        if (maxSampledSpeed > 0f)
            rawSpeed = Mathf.Min(rawSpeed, maxSampledSpeed);
        if (rawSpeed < Mathf.Max(0f, speedDeadZone))
            rawSpeed = 0f;

        float factor = DampFactor(speedSmoothingSharpness, deltaTime);
        measuredHorizontalSpeed = Mathf.Lerp(measuredHorizontalSpeed, rawSpeed, factor);
        if (rawSpeed <= 0f && measuredHorizontalSpeed < speedDeadZone * 0.5f)
            measuredHorizontalSpeed = 0f;
    }

    private void ApplyProceduralClipPose(float deltaTime)
    {
        if (!cachedRig || deltaTime <= 0f)
            return;

        float runStartSpeed = Mathf.Lerp(walkSpeed, runSpeed, Mathf.Clamp01(runBlendStartSpeed01));
        float movement = SmoothStep01(Mathf.InverseLerp(speedDeadZone, walkSpeed, measuredHorizontalSpeed));
        float gallop = SmoothStep01(Mathf.InverseLerp(runStartSpeed, runSpeed, measuredHorizontalSpeed));
        float walk = movement * (1f - gallop);
        float idle = 1f - movement;

        float phaseRate = Mathf.Lerp(
            idleBreathRate,
            Mathf.Lerp(walkStrideRate, gallopStrideRate, gallop),
            movement);
        stridePhase += phaseRate * deltaTime;

        float gait = Mathf.Sin(stridePhase);
        float oppositeGait = Mathf.Sin(stridePhase + Mathf.PI);
        float doubleGait = Mathf.Sin(stridePhase * 2f);
        float boundFront = Mathf.Sin(stridePhase + Mathf.PI * 0.58f);
        float boundRear = Mathf.Sin(stridePhase - Mathf.PI * 0.16f);

        float bodyBob = Mathf.Sin(stridePhase * 0.65f) * idleBreathAmount * idle
            + doubleGait * walkBobAmount * walk
            + Mathf.Abs(doubleGait) * gallopBobAmount * gallop;
        float bodyPitch = 2.2f * walk + 9f * gallop + doubleGait * (1.2f * walk + 4f * gallop);
        float bodyRoll = gait * (1.2f * walk + 1.7f * gallop);
        float stretch = doubleGait * gallopBodyStretch * gallop;

        body.localPosition = bodyBasePosition + Vector3.up * bodyBob + Vector3.forward * (gait * 0.018f * gallop);
        body.localRotation = bodyBaseRotation * Quaternion.Euler(bodyPitch, 0f, bodyRoll);
        body.localScale = bodyBaseScale + new Vector3(-stretch * 0.45f, Mathf.Abs(stretch) * 0.18f, stretch);

        head.localPosition = headBasePosition + Vector3.up * (bodyBob * 0.55f);
        head.localRotation = headBaseRotation * Quaternion.Euler(-bodyPitch * 0.35f + gait * 1.4f * movement, gait * 2f * movement, 0f);
        head.localScale = headBaseScale;

        PoseTail(gait, doubleGait, idle, walk, gallop);

        float swing = walkLegSwingAngle * walk + gallopLegSwingAngle * gallop;
        float liftHeight = walkLegLift * walk + gallopLegLift * gallop;
        float pawStride = walkPawStrideDistance * walk + gallopPawStrideDistance * gallop;
        PoseLeg(0, Mathf.Lerp(gait, boundFront, gallop), swing, liftHeight, pawStride, gallop);
        PoseLeg(1, Mathf.Lerp(oppositeGait, Mathf.Sin(stridePhase + Mathf.PI * 0.62f), gallop), swing, liftHeight, pawStride, gallop);
        PoseLeg(2, Mathf.Lerp(oppositeGait, boundRear, gallop), swing, liftHeight, pawStride, gallop);
        PoseLeg(3, Mathf.Lerp(gait, Mathf.Sin(stridePhase - Mathf.PI * 0.12f), gallop), swing, liftHeight, pawStride, gallop);
    }

    private void PoseTail(float gait, float doubleGait, float idle, float walk, float gallop)
    {
        float tailSway = gait * (walkTailSwayAngle * walk + gallopTailSwayAngle * gallop)
            + Mathf.Sin(stridePhase * 0.55f) * 4f * idle;
        float tailCurl = doubleGait * (2.5f * walk + 5.5f * gallop);

        tailRoot.localRotation = tailRootBaseRotation * Quaternion.Euler(tailCurl, tailSway, -tailSway * 0.25f);

        if (tailBase != null)
            tailBase.localRotation = tailBaseRotation * Quaternion.Euler(tailCurl * 0.25f, -tailSway * 0.2f, 0f);
        if (tailMid != null)
            tailMid.localRotation = tailMidRotation * Quaternion.Euler(-tailCurl * 0.25f, tailSway * 0.35f, 0f);
        if (tailTip != null)
            tailTip.localRotation = tailTipRotation * Quaternion.Euler(-tailCurl * 0.45f, tailSway * 0.55f, 0f);
    }

    private void PoseLeg(int index, float phase, float swing, float liftHeight, float pawStride, float gallop)
    {
        if (legs == null || index < 0 || index >= legs.Length || legs[index] == null)
            return;

        float extension = phase * swing;
        float gallopTuck = Mathf.Max(0f, -phase) * 24f * gallop;
        legs[index].localRotation = legBaseRotations[index] * Quaternion.Euler(extension - gallopTuck, 0f, 0f);

        if (paws == null || index >= paws.Length || paws[index] == null)
            return;

        float lift01 = Mathf.Clamp01(phase * 0.5f + 0.5f);
        float contactLift = Mathf.Sin(lift01 * Mathf.PI) * liftHeight;
        float strideOffset = -phase * pawStride;
        paws[index].localPosition = pawBasePositions[index]
            + Vector3.up * contactLift
            + Vector3.forward * strideOffset;
        paws[index].localRotation = pawBaseRotations[index] * Quaternion.Euler(extension * 0.35f, 0f, 0f);
    }

    private static Transform FindDeepChild(Transform root, string childName)
    {
        if (root == null)
            return null;

        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == childName)
                return child;
        }

        return null;
    }

    private static float DampFactor(float sharpness, float deltaTime)
    {
        if (sharpness <= 0f || deltaTime <= 0f)
            return 1f;

        return 1f - Mathf.Exp(-sharpness * deltaTime);
    }

    private static float SmoothStep01(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }
}

public class PlayableCreatureAnimationDriver : MonoBehaviour
{
    private enum LocomotionMode
    {
        Idle,
        Walk,
        Run
    }

    public Rigidbody rootRigidbody;
    public AnimationClip idleClip;
    public AnimationClip walkClip;
    public AnimationClip runClip;

    [Header("Speed Mapping")]
    public float walkSpeed = 0.45f;
    public float runSpeed = 3.2f;
    public float speedDeadZone = 0.04f;
    public float speedSmoothingSharpness = 10f;
    public float speedHysteresis = 0.06f;
    public float teleportResetDistance = 1.25f;
    public float maxSampledSpeed = 8f;

    [Header("Blend")]
    public float blendSharpness = 8f;
    public float idleFadeOutSpeed01 = 0.2f;
    public float runBlendStartSpeed01 = 0.55f;

    [Header("Playback")]
    public float idlePlaybackSpeed = 0.85f;
    public float walkPlaybackSpeed = 1f;
    public float runPlaybackSpeed = 1.05f;
    public float dynamicPlaybackInfluence = 0.35f;
    public float minPlaybackScale = 0.75f;
    public float maxPlaybackScale = 1.35f;
    public float playbackSmoothingSharpness = 8f;

    [Header("Grounding")]
    public bool keepVisualAboveGround = true;
    public float visualGroundClearance = 0.08f;
    public float groundProbeHeight = 2f;
    public float groundProbeDistance = 5f;
    public LayerMask groundMask = -1;
    public float groundSharpness = 14f;
    public float maxGroundDownStep = 0.35f;
    public float maxGroundUpStep = 1.25f;
    public bool useStableGroundAnchor = true;

    [Header("Visual Pose")]
    public bool alignToGroundNormal = true;
    public float maxVisualSlopeAngle = 24f;
    public float slopeAlignmentSharpness = 10f;
    public float turnLeanAngle = 4f;
    public float turnLeanSharpness = 8f;
    public float maxTurnRateForLean = 240f;

    private Animator animator;
    private Renderer[] renderers;
    private Transform rootTransform;
    private PlayableGraph graph;
    private AnimationMixerPlayable mixer;
    private AnimationClipPlayable idlePlayable;
    private AnimationClipPlayable walkPlayable;
    private AnimationClipPlayable runPlayable;
    private float speed01;
    private float idleWeight = 1f;
    private float walkWeight;
    private float runWeight;
    private Vector3 previousRootPosition;
    private Vector3 previousRootForward;
    private bool hasPreviousRootPosition;
    private bool hasPreviousRootForward;
    private LocomotionMode locomotionMode;
    private float measuredHorizontalSpeed;
    private float currentIdlePlaybackSpeed = 1f;
    private float currentWalkPlaybackSpeed = 1f;
    private float currentRunPlaybackSpeed = 1f;
    private Vector3 smoothedGroundNormal = Vector3.up;
    private float smoothedTurnLean;
    private bool hasStableGroundAnchor;
    private Vector3 stableGroundAnchorLocalPoint;

    private void OnEnable()
    {
        CacheSceneReferences();
        ResetVisualSamples();
        BuildGraph();
    }

    private void OnDisable()
    {
        DestroyGraph();
    }

    private void OnDestroy()
    {
        DestroyGraph();
    }

    public void RebuildAnimationGraph()
    {
        if (!isActiveAndEnabled)
            return;

        CacheSceneReferences();
        ResetVisualSamples();
        BuildGraph();
    }

    private void CacheSceneReferences()
    {
        animator = GetComponentInChildren<Animator>(true);
        if (animator == null)
            animator = gameObject.AddComponent<Animator>();

        if (animator != null)
            animator.applyRootMotion = false;

        if (rootRigidbody == null)
            rootRigidbody = GetComponentInParent<Rigidbody>();

        Transform resolvedRoot = ResolveRootTransform();
        if (rootTransform != resolvedRoot)
        {
            rootTransform = resolvedRoot;
            hasPreviousRootPosition = false;
            hasPreviousRootForward = false;
        }

        renderers = GetComponentsInChildren<Renderer>(true);
        RefreshStableGroundAnchor();
    }

    private void FixedUpdate()
    {
        CacheRootReferenceIfNeeded();
        SampleRootSpeed(Time.fixedDeltaTime);
    }

    private void Update()
    {
        CacheRootReferenceIfNeeded();

        if (rootRigidbody == null)
            SampleRootSpeed(Time.deltaTime);

        if (!graph.IsValid())
            return;

        UpdateLocomotionMode();
        UpdateBlendWeights(Time.deltaTime);
        UpdatePlayableSpeeds(Time.deltaTime);

        LoopPlayable(idlePlayable, idleClip);
        LoopPlayable(walkPlayable, walkClip);
        LoopPlayable(runPlayable, runClip);
    }

    private void CacheRootReferenceIfNeeded()
    {
        Rigidbody resolvedBody = rootRigidbody != null ? rootRigidbody : GetComponentInParent<Rigidbody>();
        Transform resolvedRoot = resolvedBody != null
            ? resolvedBody.transform
            : transform.parent != null
                ? transform.parent
                : transform;

        if (resolvedBody == rootRigidbody && resolvedRoot == rootTransform)
            return;

        rootRigidbody = resolvedBody;
        rootTransform = resolvedRoot;
        ResetVisualSamples();
    }

    private Transform ResolveRootTransform()
    {
        if (rootRigidbody != null)
            return rootRigidbody.transform;

        return transform.parent != null ? transform.parent : transform;
    }

    private void UpdateBlendWeights(float deltaTime)
    {
        if (!mixer.IsValid())
            return;

        GetLocomotionBands(
            out float idleEnterSpeed,
            out _,
            out float idleFadeEndSpeed,
            out float runExitSpeed,
            out _,
            out float runFullSpeed);

        float targetSpeed01 = Mathf.InverseLerp(idleEnterSpeed, runFullSpeed, measuredHorizontalSpeed);
        speed01 = Mathf.Lerp(speed01, targetSpeed01, DampFactor(blendSharpness, deltaTime));

        float locomotionAmount = locomotionMode == LocomotionMode.Idle
            ? 0f
            : SmoothStep01(Mathf.InverseLerp(idleEnterSpeed, idleFadeEndSpeed, measuredHorizontalSpeed));
        float runAmount = locomotionMode == LocomotionMode.Run
            ? SmoothStep01(Mathf.InverseLerp(runExitSpeed, runFullSpeed, measuredHorizontalSpeed))
            : 0f;

        float targetIdleWeight = 1f - locomotionAmount;
        float targetRunWeight = locomotionAmount * runAmount;
        float targetWalkWeight = locomotionAmount - targetRunWeight;
        NormalizeWeights(ref targetIdleWeight, ref targetWalkWeight, ref targetRunWeight);

        float blendFactor = DampFactor(blendSharpness, deltaTime);
        idleWeight = Mathf.Lerp(idleWeight, targetIdleWeight, blendFactor);
        walkWeight = Mathf.Lerp(walkWeight, targetWalkWeight, blendFactor);
        runWeight = Mathf.Lerp(runWeight, targetRunWeight, blendFactor);
        NormalizeWeights(ref idleWeight, ref walkWeight, ref runWeight);

        mixer.SetInputWeight(0, idleWeight);
        mixer.SetInputWeight(1, walkWeight);
        mixer.SetInputWeight(2, runWeight);
    }

    private void UpdatePlayableSpeeds(float deltaTime)
    {
        if (!idlePlayable.IsValid() || !walkPlayable.IsValid() || !runPlayable.IsValid())
            return;

        GetLocomotionBands(
            out _,
            out _,
            out _,
            out _,
            out _,
            out float runFullSpeed);

        float nominalWalkSpeed = Mathf.Max(0.01f, walkSpeed);
        float minScale = Mathf.Max(0.05f, Mathf.Min(minPlaybackScale, maxPlaybackScale));
        float maxScale = Mathf.Max(minScale, maxPlaybackScale);
        float influence = Mathf.Clamp01(dynamicPlaybackInfluence);
        float walkScale = Mathf.Lerp(
            1f,
            Mathf.Clamp(measuredHorizontalSpeed / nominalWalkSpeed, minScale, maxScale),
            influence);
        float runScale = Mathf.Lerp(
            1f,
            Mathf.Clamp(measuredHorizontalSpeed / Mathf.Max(0.01f, runFullSpeed), minScale, maxScale),
            influence);

        float factor = DampFactor(playbackSmoothingSharpness, deltaTime);
        currentIdlePlaybackSpeed = Mathf.Lerp(
            currentIdlePlaybackSpeed,
            Mathf.Max(0.01f, idlePlaybackSpeed),
            factor);
        currentWalkPlaybackSpeed = Mathf.Lerp(
            currentWalkPlaybackSpeed,
            Mathf.Max(0.01f, walkPlaybackSpeed) * walkScale,
            factor);
        currentRunPlaybackSpeed = Mathf.Lerp(
            currentRunPlaybackSpeed,
            Mathf.Max(0.01f, runPlaybackSpeed) * runScale,
            factor);

        idlePlayable.SetSpeed(currentIdlePlaybackSpeed);
        walkPlayable.SetSpeed(currentWalkPlaybackSpeed);
        runPlayable.SetSpeed(currentRunPlaybackSpeed);
    }

    private void SampleRootSpeed(float deltaTime)
    {
        if (deltaTime <= 0.0001f)
            return;

        Transform root = rootTransform != null ? rootTransform : ResolveRootTransform();
        if (root == null)
            return;

        float velocitySpeed = GetRigidbodyHorizontalSpeed();
        float positionSpeed = 0f;
        Vector3 current = root.position;
        if (hasPreviousRootPosition)
        {
            Vector3 delta = current - previousRootPosition;
            delta.y = 0f;
            float horizontalDistance = delta.magnitude;
            if (horizontalDistance > Mathf.Max(0.05f, teleportResetDistance))
            {
                ResetVisualSamples();
                previousRootPosition = current;
                hasPreviousRootPosition = true;
                return;
            }

            positionSpeed = horizontalDistance / deltaTime;
        }

        previousRootPosition = current;
        hasPreviousRootPosition = true;

        float rawSpeed = SanitizeSpeed(Mathf.Max(velocitySpeed, positionSpeed));
        if (maxSampledSpeed > 0f)
            rawSpeed = Mathf.Min(rawSpeed, maxSampledSpeed);

        if (rawSpeed < Mathf.Max(0f, speedDeadZone))
            rawSpeed = 0f;

        float speedFactor = DampFactor(speedSmoothingSharpness, deltaTime);
        measuredHorizontalSpeed = Mathf.Lerp(measuredHorizontalSpeed, rawSpeed, speedFactor);
        if (rawSpeed <= 0f && measuredHorizontalSpeed < Mathf.Max(0.005f, speedDeadZone * 0.5f))
            measuredHorizontalSpeed = 0f;

        SampleTurnLean(root, deltaTime);
    }

    private float GetRigidbodyHorizontalSpeed()
    {
        if (rootRigidbody == null)
            return 0f;

        Vector3 velocity = rootRigidbody.linearVelocity;
        velocity.y = 0f;
        return SanitizeSpeed(velocity.magnitude);
    }

    private void SampleTurnLean(Transform root, float deltaTime)
    {
        if (root == null || deltaTime <= 0.0001f)
            return;

        Vector3 forward = root.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
            return;

        forward.Normalize();
        float targetLean = 0f;
        if (hasPreviousRootForward)
        {
            float signedAngle = Vector3.SignedAngle(previousRootForward, forward, Vector3.up);
            float turnRate = signedAngle / deltaTime;
            targetLean = -Mathf.Clamp(turnRate / Mathf.Max(1f, maxTurnRateForLean), -1f, 1f)
                * turnLeanAngle
                * Mathf.Clamp01(speed01);
        }

        previousRootForward = forward;
        hasPreviousRootForward = true;

        float leanFactor = 1f - Mathf.Exp(-turnLeanSharpness * deltaTime);
        smoothedTurnLean = Mathf.Lerp(smoothedTurnLean, targetLean, leanFactor);
    }

    private void ResetVisualSamples()
    {
        Transform root = rootTransform != null ? rootTransform : ResolveRootTransform();
        rootTransform = root;
        previousRootPosition = root != null ? root.position : transform.position;
        previousRootForward = root != null ? Vector3.ProjectOnPlane(root.forward, Vector3.up).normalized : Vector3.forward;
        hasPreviousRootPosition = false;
        hasPreviousRootForward = false;
        measuredHorizontalSpeed = 0f;
        speed01 = 0f;
        idleWeight = 1f;
        walkWeight = 0f;
        runWeight = 0f;
        currentIdlePlaybackSpeed = Mathf.Max(0.01f, idlePlaybackSpeed);
        currentWalkPlaybackSpeed = Mathf.Max(0.01f, walkPlaybackSpeed);
        currentRunPlaybackSpeed = Mathf.Max(0.01f, runPlaybackSpeed);
        locomotionMode = LocomotionMode.Idle;
        smoothedGroundNormal = Vector3.up;
        smoothedTurnLean = 0f;
    }

    private void LateUpdate()
    {
        CacheRootReferenceIfNeeded();

        if (renderers == null || renderers.Length == 0)
        {
            renderers = GetComponentsInChildren<Renderer>(true);
            RefreshStableGroundAnchor();
        }

        Transform root = rootTransform != null ? rootTransform : ResolveRootTransform();
        bool grounded = false;
        VisualGroundingUtility.GroundSample ground = default;

        if (keepVisualAboveGround)
        {
            if (useStableGroundAnchor)
            {
                grounded = TrySampleStableGround(root, out ground);
            }

            if (!grounded)
            {
                grounded = VisualGroundingUtility.KeepBottomAboveGround(
                    transform,
                    root,
                    renderers,
                    visualGroundClearance,
                    groundProbeHeight,
                    groundProbeDistance,
                    groundMask,
                    groundSharpness,
                    maxGroundDownStep,
                    maxGroundUpStep,
                    out ground);
            }
        }

        ApplyVisualRotation(root, grounded ? ground.normal : Vector3.up);

        if (grounded && useStableGroundAnchor && hasStableGroundAnchor)
            ApplyStableGroundAnchor(ground.y + visualGroundClearance);
    }

    private bool TrySampleStableGround(Transform root, out VisualGroundingUtility.GroundSample ground)
    {
        if (!hasStableGroundAnchor)
            RefreshStableGroundAnchor();

        if (!hasStableGroundAnchor)
        {
            ground = default;
            return false;
        }

        return VisualGroundingUtility.TrySampleGroundForVisual(
            transform,
            root,
            renderers,
            groundProbeHeight,
            groundProbeDistance,
            groundMask,
            groundSharpness,
            out ground);
    }

    private void RefreshStableGroundAnchor()
    {
        hasStableGroundAnchor = false;

        if (!useStableGroundAnchor)
            return;

        if (VisualGroundingUtility.TryGetStableVisualLocalBounds(transform, renderers, out Bounds localBounds))
        {
            stableGroundAnchorLocalPoint = new Vector3(localBounds.center.x, localBounds.min.y, localBounds.center.z);
            hasStableGroundAnchor = true;
        }
    }

    private void ApplyStableGroundAnchor(float desiredAnchorY)
    {
        float currentAnchorY = transform.TransformPoint(stableGroundAnchorLocalPoint).y;
        float delta = desiredAnchorY - currentAnchorY;
        if (Mathf.Abs(delta) <= 0.003f)
            return;

        float maxStep = delta > 0f ? maxGroundUpStep : maxGroundDownStep;
        if (maxStep > 0f)
            delta = delta > 0f ? Mathf.Min(delta, maxStep) : Mathf.Max(delta, -maxStep);

        transform.position += Vector3.up * delta;
    }

    private void BuildGraph()
    {
        DestroyGraph();

        if (idleClip == null && walkClip == null && runClip == null)
            return;

        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        if (animator == null)
            animator = gameObject.AddComponent<Animator>();

        if (animator == null)
            return;

        animator.applyRootMotion = false;

        idleClip ??= walkClip != null ? walkClip : runClip;
        walkClip ??= idleClip;
        runClip ??= walkClip;

        if (idleClip == null || walkClip == null || runClip == null)
            return;

        graph = PlayableGraph.Create($"{name}_PresentationAnimation");
        graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

        mixer = AnimationMixerPlayable.Create(graph, 3);
        idlePlayable = AnimationClipPlayable.Create(graph, idleClip);
        walkPlayable = AnimationClipPlayable.Create(graph, walkClip);
        runPlayable = AnimationClipPlayable.Create(graph, runClip);

        idlePlayable.SetApplyFootIK(false);
        walkPlayable.SetApplyFootIK(false);
        runPlayable.SetApplyFootIK(false);

        graph.Connect(idlePlayable, 0, mixer, 0);
        graph.Connect(walkPlayable, 0, mixer, 1);
        graph.Connect(runPlayable, 0, mixer, 2);

        AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "Animation", animator);
        output.SetSourcePlayable(mixer);

        mixer.SetInputWeight(0, idleWeight);
        mixer.SetInputWeight(1, walkWeight);
        mixer.SetInputWeight(2, runWeight);
        idlePlayable.SetSpeed(currentIdlePlaybackSpeed);
        walkPlayable.SetSpeed(currentWalkPlaybackSpeed);
        runPlayable.SetSpeed(currentRunPlaybackSpeed);

        graph.Play();
    }

    private void UpdateLocomotionMode()
    {
        GetLocomotionBands(
            out float idleEnterSpeed,
            out float idleExitSpeed,
            out _,
            out float runExitSpeed,
            out float runEnterSpeed,
            out _);

        switch (locomotionMode)
        {
            case LocomotionMode.Idle:
                if (measuredHorizontalSpeed >= idleExitSpeed)
                    locomotionMode = LocomotionMode.Walk;
                break;
            case LocomotionMode.Walk:
                if (measuredHorizontalSpeed <= idleEnterSpeed)
                    locomotionMode = LocomotionMode.Idle;
                else if (measuredHorizontalSpeed >= runEnterSpeed)
                    locomotionMode = LocomotionMode.Run;
                break;
            case LocomotionMode.Run:
                if (measuredHorizontalSpeed <= runExitSpeed)
                    locomotionMode = measuredHorizontalSpeed <= idleEnterSpeed
                        ? LocomotionMode.Idle
                        : LocomotionMode.Walk;
                break;
        }
    }

    private void GetLocomotionBands(
        out float idleEnterSpeed,
        out float idleExitSpeed,
        out float idleFadeEndSpeed,
        out float runExitSpeed,
        out float runEnterSpeed,
        out float runFullSpeed)
    {
        float deadZone = Mathf.Max(0f, speedDeadZone);
        float nominalWalkSpeed = Mathf.Max(deadZone + 0.01f, walkSpeed);
        runFullSpeed = Mathf.Max(nominalWalkSpeed + 0.01f, runSpeed);
        float hysteresis = Mathf.Max(0.005f, speedHysteresis);

        idleExitSpeed = Mathf.Max(deadZone, nominalWalkSpeed * 0.55f);
        idleEnterSpeed = Mathf.Max(deadZone, idleExitSpeed - Mathf.Max(hysteresis, nominalWalkSpeed * 0.2f));

        float configuredIdleFadeEndSpeed = Mathf.Lerp(
            nominalWalkSpeed,
            runFullSpeed,
            Mathf.Clamp01(idleFadeOutSpeed01));
        float naturalIdleFadeEndSpeed = Mathf.Max(idleExitSpeed + 0.01f, nominalWalkSpeed * 1.6f);
        idleFadeEndSpeed = Mathf.Max(
            idleExitSpeed + 0.01f,
            Mathf.Min(configuredIdleFadeEndSpeed, naturalIdleFadeEndSpeed));

        float runBlendStartSpeed = Mathf.Lerp(
            nominalWalkSpeed,
            runFullSpeed,
            Mathf.Clamp01(runBlendStartSpeed01));
        float runBand = Mathf.Max(hysteresis, (runFullSpeed - nominalWalkSpeed) * 0.05f);
        runExitSpeed = Mathf.Clamp(runBlendStartSpeed - runBand, nominalWalkSpeed, runFullSpeed);
        runEnterSpeed = Mathf.Clamp(runBlendStartSpeed + runBand, runExitSpeed, runFullSpeed);
    }

    private void ApplyVisualRotation(Transform root, Vector3 groundNormal)
    {
        Quaternion rootRotation = root != null ? root.rotation : transform.rotation;
        Quaternion targetWorldRotation = rootRotation;

        if (alignToGroundNormal)
        {
            float slopeFactor = 1f - Mathf.Exp(-slopeAlignmentSharpness * Time.deltaTime);
            smoothedGroundNormal = Vector3.Slerp(smoothedGroundNormal, groundNormal.normalized, slopeFactor).normalized;
            targetWorldRotation = VisualGroundingUtility.GetSlopeRotation(
                rootRotation,
                smoothedGroundNormal,
                maxVisualSlopeAngle);
        }

        Quaternion targetLocalRotation = transform.parent != null
            ? Quaternion.Inverse(transform.parent.rotation) * targetWorldRotation
            : targetWorldRotation;
        targetLocalRotation *= Quaternion.Euler(0f, 0f, smoothedTurnLean);

        float factor = 1f - Mathf.Exp(-slopeAlignmentSharpness * Time.deltaTime);
        transform.localRotation = Quaternion.Slerp(transform.localRotation, targetLocalRotation, factor);
    }

    private static void LoopPlayable(AnimationClipPlayable playable, AnimationClip clip)
    {
        if (!playable.IsValid() || clip == null || clip.length <= 0.0001f)
            return;

        double time = playable.GetTime();
        if (time >= clip.length)
            playable.SetTime(time % clip.length);
    }

    private static float DampFactor(float sharpness, float deltaTime)
    {
        if (sharpness <= 0f || deltaTime <= 0f)
            return 1f;

        return 1f - Mathf.Exp(-sharpness * deltaTime);
    }

    private static float SmoothStep01(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }

    private static float SanitizeSpeed(float speed)
    {
        if (float.IsNaN(speed) || float.IsInfinity(speed))
            return 0f;

        return Mathf.Max(0f, speed);
    }

    private static void NormalizeWeights(ref float idle, ref float walk, ref float run)
    {
        idle = Mathf.Clamp01(idle);
        walk = Mathf.Clamp01(walk);
        run = Mathf.Clamp01(run);

        float total = idle + walk + run;
        if (total <= 0.0001f)
        {
            idle = 1f;
            walk = 0f;
            run = 0f;
            return;
        }

        idle /= total;
        walk /= total;
        run /= total;
    }

    private void DestroyGraph()
    {
        if (graph.IsValid())
            graph.Destroy();
    }
}
