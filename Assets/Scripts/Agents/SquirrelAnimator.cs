using UnityEngine;

// Presentation-only animation for the existing visible animal mesh.
// It never moves the physics root, collider, or ML-Agent state.
public class SquirrelAnimator : MonoBehaviour
{
    [Header("Motion")]
    public bool enableProceduralMotion = true;
    public float speedThreshold = 0.2f;
    public float maxVisualSpeed = 3.5f;
    public float strideRate = 6f;
    public float idleBreathSpeed = 1.8f;
    public float idleBreathAmount = 0.012f;
    public float runBlendStartSpeed01 = 0.54f;

    [Header("Body Pose")]
    public float bobAmount = 0.018f;
    public float swayAngle = 2f;
    public float forwardPitch = 2.5f;
    public float pitchPulse = 1.2f;
    public float turnLeanAngle = 3f;
    public float visualYawOffset = 0f;
    public float runBodyStretchAmount = 0.035f;

    [Header("Mesh Limb Motion")]
    public bool enableMeshLimbMotion = true;
    public float legSwingAmount = 0.024f;
    public float legLiftAmount = 0.009f;
    public float runLegSwingMultiplier = 1.65f;
    public float runLegLiftMultiplier = 2.2f;
    public float tailSwayAmount = 0.018f;
    public float runTailLiftAmount = 0.025f;
    public float lowerBodyRatio = 0.28f;
    public float sideLegRatio = 0.46f;

    [Header("Smoothing")]
    public float positionSharpness = 10f;
    public float rotationSharpness = 8f;

    [Header("Visual Grounding")]
    public bool keepVisualAboveGround = true;
    public float visualGroundClearance = 0.07f;
    public float groundProbeHeight = 2f;
    public float groundProbeDistance = 5f;
    public LayerMask groundMask = -1;

    [Header("State Tint")]
    public bool enableStateTint = true;
    public float stateTintStrength = 0.22f;
    public float hungerTintThreshold = 0.7f;
    public float fearTintThreshold = 0.35f;
    public float tiredEnergyThreshold = 0.25f;
    public Color hungryTint = new Color(1f, 0.55f, 0.25f, 1f);
    public Color fearTint = new Color(1f, 0.28f, 0.22f, 1f);
    public Color safeZoneTint = new Color(0.35f, 0.85f, 1f, 1f);

    private Rigidbody rb;
    private SquirrelAgent squirrel;
    private Vector3 baseLocalPosition;
    private Quaternion baseLocalRotation;
    private Vector3 baseLocalScale;
    private Renderer[] renderers;
    private MaterialPropertyBlock[] materialBlocks;
    private Color[] baseColors;
    private MeshFilter[] meshFilters;
    private Mesh[] originalMeshes;
    private Mesh[] animatedMeshes;
    private Vector3[][] baseVertices;
    private Vector3[][] workingVertices;
    private Bounds[] baseBounds;
    private float stridePhase;
    private float smoothedSpeed01;
    private float smoothedTurn;
    private bool isSafeVisualTarget;
    private bool meshesReady;
    private bool hasCachedBasePose;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private void Awake()
    {
        CacheComponents();
    }

    private void OnEnable()
    {
        CacheComponents();
    }

    private void OnDisable()
    {
        if (!hasCachedBasePose)
            return;

        RestoreBasePoseImmediate();
        RestoreMeshVertices();
    }

    private void OnDestroy()
    {
        RestoreOriginalMeshes();
    }

    private void CacheComponents()
    {
        if (!IsSafeVisualTarget())
        {
            isSafeVisualTarget = false;
            enabled = false;
            return;
        }

        isSafeVisualTarget = true;
        rb = GetComponentInParent<Rigidbody>();
        squirrel = GetComponentInParent<SquirrelAgent>();
        baseLocalPosition = transform.localPosition;
        baseLocalRotation = transform.localRotation;
        baseLocalScale = transform.localScale;
        hasCachedBasePose = true;

        CacheRenderers();
        CacheMeshes();
    }

    private void CacheRenderers()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        materialBlocks = new MaterialPropertyBlock[renderers.Length];
        baseColors = new Color[renderers.Length];

        for (int i = 0; i < renderers.Length; i++)
        {
            materialBlocks[i] = new MaterialPropertyBlock();
            baseColors[i] = GetRendererBaseColor(renderers[i]);
        }
    }

    private void CacheMeshes()
    {
        if (meshesReady)
            return;

        meshFilters = GetComponentsInChildren<MeshFilter>(true);
        originalMeshes = new Mesh[meshFilters.Length];
        animatedMeshes = new Mesh[meshFilters.Length];
        baseVertices = new Vector3[meshFilters.Length][];
        workingVertices = new Vector3[meshFilters.Length][];
        baseBounds = new Bounds[meshFilters.Length];
        int readableMeshCount = 0;

        for (int i = 0; i < meshFilters.Length; i++)
        {
            if (meshFilters[i] == null)
                continue;

            Mesh source = meshFilters[i].sharedMesh;
            if (source == null || !source.isReadable)
                continue;

            originalMeshes[i] = source;
            animatedMeshes[i] = Instantiate(source);
            animatedMeshes[i].name = source.name + "_PresentationAnimated";
            animatedMeshes[i].hideFlags = HideFlags.DontSave;
            meshFilters[i].sharedMesh = animatedMeshes[i];

            baseVertices[i] = animatedMeshes[i].vertices;
            workingVertices[i] = new Vector3[baseVertices[i].Length];
            baseBounds[i] = animatedMeshes[i].bounds;
            readableMeshCount++;
        }

        if (readableMeshCount == 0)
            enableMeshLimbMotion = false;

        meshesReady = true;
    }

    private void Update()
    {
        if (!isSafeVisualTarget)
            return;

        float speed = rb != null
            ? new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z).magnitude
            : 0f;

        float targetSpeed01 = Mathf.InverseLerp(speedThreshold, maxVisualSpeed, speed);
        smoothedSpeed01 = Mathf.Lerp(smoothedSpeed01, targetSpeed01, SmoothFactor(positionSharpness));

        float runAmount = GetRunAmount();
        float phaseSpeed = Mathf.Lerp(idleBreathSpeed, strideRate, smoothedSpeed01);
        phaseSpeed *= Mathf.Lerp(1f, 1.2f, runAmount);
        stridePhase += Time.deltaTime * phaseSpeed;

        if (enableProceduralMotion)
        {
            ApplyBodyMotion();
            ApplyMeshLimbMotion();
        }
        else
        {
            RestoreBasePose();
            RestoreMeshVertices();
        }

        ApplyStateTint();
    }

    private void LateUpdate()
    {
        if (!isSafeVisualTarget || !keepVisualAboveGround)
            return;

        VisualGroundingUtility.KeepBottomAboveGround(
            transform,
            rb != null ? rb.transform : transform.parent,
            renderers,
            visualGroundClearance,
            groundProbeHeight,
            groundProbeDistance,
            groundMask,
            positionSharpness);
    }

    private bool IsSafeVisualTarget()
    {
        if (HasRiggedAnimationDriver(transform) || GetComponentInChildren<Animator>(true) != null)
            return false;

        if (GetComponent<Rigidbody>() != null)
            return false;

        if (GetComponent<SquirrelAgent>() != null || GetComponent<PredatorAgent>() != null)
            return false;

        if (GetComponentInChildren<Camera>(true) != null)
            return false;

        if (HasEnabledCollider(transform))
            return false;

        return GetComponentInParent<Rigidbody>() != null;
    }

    private static bool HasRiggedAnimationDriver(Transform candidate)
    {
        return candidate != null &&
            (candidate.GetComponentInParent<CreatureAnimationDriver>() != null ||
             candidate.GetComponentInParent<ProceduralSquirrelAnimationDriver>() != null ||
             candidate.GetComponentInParent<PlayableCreatureAnimationDriver>() != null);
    }

    private static bool HasEnabledCollider(Transform candidate)
    {
        if (candidate == null)
            return false;

        foreach (Collider collider in candidate.GetComponentsInChildren<Collider>(true))
        {
            if (collider != null && collider.enabled)
                return true;
        }

        return false;
    }

    private void ApplyBodyMotion()
    {
        float runAmount = GetRunAmount();
        float doubleStride = Mathf.Sin(stridePhase * 2f);
        float runBob = doubleStride * bobAmount * Mathf.Lerp(smoothedSpeed01, 1.25f, runAmount);
        float idleBob = Mathf.Sin(stridePhase) * idleBreathAmount * (1f - smoothedSpeed01);
        Vector3 targetPosition = baseLocalPosition + Vector3.up * (runBob + idleBob);

        float turnVelocity = rb != null ? rb.angularVelocity.y * Mathf.Rad2Deg : 0f;
        float targetTurn = Mathf.Clamp(-turnVelocity / 180f, -1f, 1f);
        smoothedTurn = Mathf.Lerp(smoothedTurn, targetTurn, SmoothFactor(rotationSharpness));

        float sway = Mathf.Sin(stridePhase) * swayAngle * smoothedSpeed01;
        float pitch = forwardPitch * smoothedSpeed01
            + doubleStride * pitchPulse * Mathf.Lerp(smoothedSpeed01, 1.35f, runAmount);
        float lean = smoothedTurn * turnLeanAngle * smoothedSpeed01;
        float stretch = doubleStride * runBodyStretchAmount * runAmount;

        Quaternion targetRotation = baseLocalRotation
            * Quaternion.Euler(pitch, visualYawOffset + sway, lean);

        transform.localPosition = Vector3.Lerp(transform.localPosition, targetPosition, SmoothFactor(positionSharpness));
        transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRotation, SmoothFactor(rotationSharpness));
        Vector3 targetScale = new Vector3(
            baseLocalScale.x * (1f - stretch * 0.35f),
            baseLocalScale.y * (1f + Mathf.Abs(stretch) * 0.18f),
            baseLocalScale.z * (1f + stretch));
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, SmoothFactor(positionSharpness));
    }

    private void ApplyMeshLimbMotion()
    {
        if (!enableMeshLimbMotion || !meshesReady || animatedMeshes == null)
        {
            if (meshesReady)
                RestoreMeshVertices();
            return;
        }

        if (baseVertices == null || workingVertices == null || baseBounds == null)
            return;

        float gait = Mathf.Sin(stridePhase);
        float oppositeGait = Mathf.Sin(stridePhase + Mathf.PI);
        float liftPhase = Mathf.Abs(Mathf.Sin(stridePhase));
        float runAmount = GetRunAmount();
        float tailSway = Mathf.Sin(stridePhase * 0.7f) * tailSwayAmount * Mathf.Lerp(smoothedSpeed01, 1.45f, runAmount);
        float tailLift = Mathf.Abs(Mathf.Sin(stridePhase * 2f)) * runTailLiftAmount * runAmount;
        float legSwing = legSwingAmount * Mathf.Lerp(1f, runLegSwingMultiplier, runAmount);
        float legLift = legLiftAmount * Mathf.Lerp(1f, runLegLiftMultiplier, runAmount);

        for (int meshIndex = 0; meshIndex < animatedMeshes.Length; meshIndex++)
        {
            if (baseVertices == null || workingVertices == null ||
                meshIndex >= baseVertices.Length || meshIndex >= workingVertices.Length)
                continue;

            Mesh mesh = animatedMeshes[meshIndex];
            Vector3[] source = baseVertices[meshIndex];
            Vector3[] target = workingVertices[meshIndex];
            if (mesh == null || source == null || target == null)
                continue;

            Bounds bounds = baseBounds[meshIndex];
            Vector3 size = bounds.size;
            if (size.x < 0.001f || size.y < 0.001f || size.z < 0.001f)
                continue;

            for (int i = 0; i < source.Length; i++)
            {
                Vector3 v = source[i];
                float nx = Mathf.InverseLerp(bounds.min.x, bounds.max.x, v.x) * 2f - 1f;
                float ny = Mathf.InverseLerp(bounds.min.y, bounds.max.y, v.y);
                float nz = Mathf.InverseLerp(bounds.min.z, bounds.max.z, v.z) * 2f - 1f;

                float lowerWeight = Mathf.Clamp01((lowerBodyRatio - ny) / Mathf.Max(0.001f, lowerBodyRatio));
                float sideWeight = Mathf.Clamp01((Mathf.Abs(nx) - sideLegRatio) / Mathf.Max(0.001f, 1f - sideLegRatio));
                float frontBackWeight = Mathf.Clamp01((Mathf.Abs(nz) - 0.25f) / 0.75f);

                if (lowerWeight > 0f && sideWeight > 0f && frontBackWeight > 0f)
                {
                    float diagonalPhase = nx * nz >= 0f ? gait : oppositeGait;
                    float legWeight = lowerWeight * sideWeight * frontBackWeight * smoothedSpeed01;
                    float boundPhase = nz > 0f
                        ? Mathf.Sin(stridePhase + Mathf.PI * 0.6f + nx * 0.18f)
                        : Mathf.Sin(stridePhase - Mathf.PI * 0.12f - nx * 0.18f);
                    float blendedPhase = Mathf.Lerp(diagonalPhase, boundPhase, runAmount);
                    v.x += blendedPhase * legSwing * legWeight * 0.35f;
                    v.z += blendedPhase * legSwing * legWeight;
                    v.y += liftPhase * legLift * legWeight;
                }

                float tailWeight = Mathf.Clamp01((-nz - 0.25f) / 0.75f) * Mathf.Clamp01((ny - 0.35f) / 0.65f);
                if (tailWeight > 0f)
                {
                    v.x += tailSway * tailWeight;
                    v.y += tailLift * tailWeight;
                }

                target[i] = v;
            }

            mesh.vertices = target;
            mesh.RecalculateBounds();
        }
    }

    private void RestoreBasePose()
    {
        transform.localPosition = Vector3.Lerp(transform.localPosition, baseLocalPosition, SmoothFactor(positionSharpness));
        transform.localRotation = Quaternion.Slerp(transform.localRotation, baseLocalRotation, SmoothFactor(rotationSharpness));
        transform.localScale = baseLocalScale;
    }

    private void RestoreBasePoseImmediate()
    {
        transform.localPosition = baseLocalPosition;
        transform.localRotation = baseLocalRotation;
        transform.localScale = baseLocalScale;
    }

    private void RestoreMeshVertices()
    {
        if (!meshesReady || animatedMeshes == null || baseVertices == null)
            return;

        for (int i = 0; i < animatedMeshes.Length; i++)
        {
            if (i >= baseVertices.Length)
                continue;

            if (animatedMeshes[i] == null || baseVertices[i] == null)
                continue;

            animatedMeshes[i].vertices = baseVertices[i];
            animatedMeshes[i].RecalculateBounds();
        }
    }

    private void RestoreOriginalMeshes()
    {
        if (meshFilters == null || originalMeshes == null)
            return;

        for (int i = 0; i < meshFilters.Length; i++)
        {
            if (meshFilters[i] != null && originalMeshes[i] != null)
                meshFilters[i].sharedMesh = originalMeshes[i];
        }
    }

    private void ApplyStateTint()
    {
        if (!enableStateTint || squirrel == null || renderers == null)
            return;

        Color stateColor = Color.white;
        float strength = 0f;

        if (squirrel.IsInSafeZone)
        {
            stateColor = safeZoneTint;
            strength = stateTintStrength;
        }
        else if (squirrel.fear >= fearTintThreshold)
        {
            stateColor = fearTint;
            strength = stateTintStrength;
        }
        else if (squirrel.hunger >= hungerTintThreshold || squirrel.energy <= tiredEnergyThreshold)
        {
            stateColor = hungryTint;
            strength = stateTintStrength;
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            Color color = Color.Lerp(baseColors[i], stateColor, strength);
            renderers[i].GetPropertyBlock(materialBlocks[i]);
            materialBlocks[i].SetColor(BaseColorId, color);
            materialBlocks[i].SetColor(ColorId, color);
            renderers[i].SetPropertyBlock(materialBlocks[i]);
        }
    }

    private Color GetRendererBaseColor(Renderer renderer)
    {
        if (renderer == null || renderer.sharedMaterial == null)
            return Color.white;

        Material material = renderer.sharedMaterial;
        if (material.HasProperty(BaseColorId))
            return material.GetColor(BaseColorId);
        if (material.HasProperty(ColorId))
            return material.GetColor(ColorId);
        return Color.white;
    }

    private float SmoothFactor(float sharpness)
    {
        return 1f - Mathf.Exp(-Mathf.Max(0.01f, sharpness) * Time.deltaTime);
    }

    private float GetRunAmount()
    {
        return SmoothStep01(Mathf.InverseLerp(runBlendStartSpeed01, 1f, smoothedSpeed01));
    }

    private static float SmoothStep01(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }
}

public static class CreatureVisualAutoAttach
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AttachAllCreatureVisuals()
    {
        foreach (SquirrelAgent squirrel in Object.FindObjectsByType<SquirrelAgent>(FindObjectsInactive.Exclude))
            AttachAnimator(squirrel.transform, true, "squirrel", "low_poly", "lowpoly");

        foreach (PredatorAgent predator in Object.FindObjectsByType<PredatorAgent>(FindObjectsInactive.Exclude))
            AttachAnimator(predator.transform, false, "fox", "low_poly", "lowpoly");
    }

    private static void AttachAnimator(Transform root, bool isSquirrel, params string[] nameHints)
    {
        Transform visual = FindBestVisualTransform(root, nameHints);
        if (visual == null)
            return;

        SquirrelAnimator animator = visual.GetComponent<SquirrelAnimator>();
        if (animator == null)
            animator = visual.gameObject.AddComponent<SquirrelAnimator>();

        if (isSquirrel)
            ApplySquirrelPreset(animator);
        else
            ApplyPredatorPreset(animator);
    }

    private static Transform FindBestVisualTransform(Transform root, params string[] nameHints)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return null;

        Transform fallback = null;

        foreach (Renderer renderer in renderers)
        {
            if (!renderer.enabled)
                continue;

            Transform candidate = GetVisualRoot(renderer.transform, root);
            if (!IsSafeVisualCandidate(candidate, root))
                continue;

            if (fallback == null)
                fallback = candidate;

            string lowerName = $"{candidate.name} {renderer.name}".ToLowerInvariant();
            foreach (string hint in nameHints)
            {
                if (lowerName.Contains(hint))
                    return candidate;
            }
        }

        return fallback;
    }

    private static Transform GetVisualRoot(Transform rendererTransform, Transform agentRoot)
    {
        Transform candidate = rendererTransform;

        while (candidate.parent != null && candidate.parent != agentRoot)
        {
            if (candidate.parent.GetComponent<Rigidbody>() != null ||
                candidate.parent.GetComponent<SquirrelAgent>() != null ||
                candidate.parent.GetComponent<PredatorAgent>() != null)
                break;

            candidate = candidate.parent;
        }

        return candidate;
    }

    private static bool IsSafeVisualCandidate(Transform candidate, Transform root)
    {
        if (candidate == null || candidate == root || !candidate.IsChildOf(root))
            return false;

        if (candidate.GetComponentInParent<CreatureAnimationDriver>() != null ||
            candidate.GetComponentInParent<ProceduralSquirrelAnimationDriver>() != null ||
            candidate.GetComponentInParent<PlayableCreatureAnimationDriver>() != null ||
            candidate.GetComponentInChildren<Animator>(true) != null)
            return false;

        if (candidate.GetComponent<Rigidbody>() != null ||
            candidate.GetComponent<SquirrelAgent>() != null ||
            candidate.GetComponent<PredatorAgent>() != null)
            return false;

        if (candidate.GetComponentInChildren<Camera>(true) != null)
            return false;

        if (HasEnabledCollider(candidate))
            return false;

        return true;
    }

    private static bool HasEnabledCollider(Transform candidate)
    {
        if (candidate == null)
            return false;

        foreach (Collider collider in candidate.GetComponentsInChildren<Collider>(true))
        {
            if (collider != null && collider.enabled)
                return true;
        }

        return false;
    }

    private static void ApplySquirrelPreset(SquirrelAnimator animator)
    {
        animator.enableProceduralMotion = true;
        animator.enableMeshLimbMotion = true;
        animator.enableStateTint = true;
        animator.maxVisualSpeed = 3.5f;
        animator.strideRate = 8.8f;
        animator.runBlendStartSpeed01 = 0.5f;
        animator.bobAmount = 0.017f;
        animator.swayAngle = 1.8f;
        animator.forwardPitch = 2.1f;
        animator.pitchPulse = 1.2f;
        animator.turnLeanAngle = 2.4f;
        animator.runBodyStretchAmount = 0.028f;
        animator.legSwingAmount = 0.038f;
        animator.legLiftAmount = 0.013f;
        animator.runLegSwingMultiplier = 1.75f;
        animator.runLegLiftMultiplier = 2.35f;
        animator.tailSwayAmount = 0.032f;
        animator.runTailLiftAmount = 0.03f;
        animator.lowerBodyRatio = 0.28f;
        animator.sideLegRatio = 0.42f;
        animator.visualYawOffset = 0f;
        animator.keepVisualAboveGround = true;
        animator.visualGroundClearance = 0.055f;
        animator.stateTintStrength = 0.18f;
    }

    private static void ApplyPredatorPreset(SquirrelAnimator animator)
    {
        animator.enableProceduralMotion = true;
        // Predator meshes are not rigged here; fake vertex limb motion looks
        // unnatural on the fox model, so keep only subtle body motion.
        animator.enableMeshLimbMotion = false;
        animator.enableStateTint = false;
        animator.maxVisualSpeed = 4.2f;
        animator.strideRate = 6f;
        animator.bobAmount = 0.004f;
        animator.swayAngle = 0.5f;
        animator.forwardPitch = 0.6f;
        animator.pitchPulse = 0.3f;
        animator.turnLeanAngle = 0.8f;
        animator.legSwingAmount = 0.02f;
        animator.legLiftAmount = 0.007f;
        animator.tailSwayAmount = 0.014f;
        animator.lowerBodyRatio = 0.28f;
        animator.sideLegRatio = 0.46f;
        animator.visualYawOffset = 0f;
        animator.keepVisualAboveGround = true;
        animator.visualGroundClearance = 0.08f;
    }
}
