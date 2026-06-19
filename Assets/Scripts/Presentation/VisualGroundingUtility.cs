using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

public static class VisualGroundingUtility
{
    private const string ObstacleTag = "Obstacle";
    private const string AcornTag = "Acorn";
    private const string SafeZoneTag = "Safezone";
    private const float MinimumProbeHeight = 0.1f;
    private const float MinimumProbeDistance = 0.1f;
    private const float GroundingDeadZone = 0.003f;
    private const float DownwardSettlingSlack = 0.012f;
    private const float HeightSelectionEpsilon = 0.004f;
    private const float TerrainPreferenceHeightTolerance = 0.18f;
    private const float MinimumGroundNormalY = 0.15f;
    private const float MinimumFootprintProbeReach = 0.035f;
    private const float MaximumFootprintProbeReach = 0.45f;
    private const float FootprintProbeScale = 0.45f;
    private const float MinimumBakedBoundsVerticalCorrection = 0.08f;
    private const float MaximumBakedBoundsVerticalCorrection = 0.85f;
    private const float BakedBoundsVerticalCorrectionScale = 0.35f;
    private const float MinimumBakedToRendererHeightRatio = 0.35f;
    private const float MaximumBakedToRendererHeightRatio = 1.45f;
    private const int RaycastHitCapacity = 32;
    private const int StaleStateFrameGap = 4;

    private static readonly RaycastHit[] RaycastHits = new RaycastHit[RaycastHitCapacity];
    private static readonly ConditionalWeakTable<Transform, GroundingState> GroundingStates =
        new ConditionalWeakTable<Transform, GroundingState>();
    private static readonly ConditionalWeakTable<SkinnedMeshRenderer, SkinnedBoundsCache> SkinnedBoundsCaches =
        new ConditionalWeakTable<SkinnedMeshRenderer, SkinnedBoundsCache>();

    public struct GroundSample
    {
        public bool found;
        public float y;
        public Vector3 normal;
    }

    private sealed class GroundingState
    {
        public bool hasSample;
        public float groundY;
        public Vector3 normal = Vector3.up;
        public int frame;
    }

    private sealed class SkinnedBoundsCache
    {
        public readonly Mesh mesh;
        public readonly List<Vector3> vertices = new List<Vector3>(4096);
        public bool hasBounds;
        public Bounds bounds;
        public int frame = -1;

        public SkinnedBoundsCache()
        {
            mesh = new Mesh
            {
                name = "PresentationGroundingBakedMesh",
                hideFlags = HideFlags.DontSave
            };
        }
    }

    public static bool TryGetRendererBounds(Renderer[] renderers, out Bounds bounds)
    {
        bounds = default;
        bool hasBounds = false;

        if (renderers == null)
            return false;

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                continue;

            if (!TryGetVisibleRendererBounds(renderer, out Bounds rendererBounds))
                continue;

            if (!hasBounds)
            {
                bounds = rendererBounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(rendererBounds);
            }
        }

        return hasBounds;
    }

    public static bool TryGetStableVisualBounds(Transform visual, Renderer[] renderers, out Bounds bounds)
    {
        bounds = default;
        if (!TryGetStableVisualLocalBounds(visual, renderers, out Bounds localBounds))
            return false;

        bounds = TransformLocalBoundsToWorld(visual, localBounds);
        return true;
    }

    public static bool TryGetStableVisualLocalBounds(Transform visual, Renderer[] renderers, out Bounds bounds)
    {
        bounds = default;
        bool hasBounds = false;

        if (visual == null || renderers == null)
            return false;

        Matrix4x4 worldToVisual = visual.worldToLocalMatrix;
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                continue;

            if (!TryGetStableRendererLocalBounds(renderer, worldToVisual, out Bounds rendererBounds))
                continue;

            if (!hasBounds)
            {
                bounds = rendererBounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(rendererBounds);
            }
        }

        return hasBounds;
    }

    public static bool TrySampleGroundForVisual(
        Transform visual,
        Transform ignoredRoot,
        Renderer[] renderers,
        float probeHeight,
        float probeDistance,
        LayerMask groundMask,
        float sharpness,
        out GroundSample ground)
    {
        ground = CreateMissingSample();

        if (visual == null || !TryGetRendererBounds(renderers, out Bounds bounds))
            return false;

        Transform probeRoot = ignoredRoot != null ? ignoredRoot : visual;
        if (!TrySampleGroundFootprint(
                probeRoot.position,
                visual,
                ignoredRoot,
                bounds,
                probeHeight,
                probeDistance,
                groundMask,
                out GroundSample sampledGround))
        {
            return false;
        }

        ground = StabilizeSample(visual, sampledGround, sharpness);
        return true;
    }

    private static bool TryGetStableRendererLocalBounds(
        Renderer renderer,
        Matrix4x4 worldToVisual,
        out Bounds bounds)
    {
        bounds = default;

        if (renderer is SkinnedMeshRenderer skinnedRenderer && skinnedRenderer.sharedMesh != null)
        {
            Matrix4x4 meshToVisual = worldToVisual * skinnedRenderer.transform.localToWorldMatrix;
            if (TryGetMeshBounds(skinnedRenderer.sharedMesh, meshToVisual, out bounds))
                return true;

            if (TryGetBakedSkinnedLocalBounds(skinnedRenderer, worldToVisual, out bounds))
                return true;
        }

        MeshFilter meshFilter = renderer.GetComponent<MeshFilter>();
        if (meshFilter != null && meshFilter.sharedMesh != null)
        {
            Matrix4x4 meshToVisual = worldToVisual * meshFilter.transform.localToWorldMatrix;
            if (TryGetMeshBounds(meshFilter.sharedMesh, meshToVisual, out bounds))
                return true;
        }

        return false;
    }

    private static bool TryGetMeshBounds(Mesh mesh, Matrix4x4 meshToTarget, out Bounds bounds)
    {
        bounds = default;
        if (mesh == null)
            return false;

        List<Vector3> vertices = new List<Vector3>(mesh.vertexCount);
        try
        {
            mesh.GetVertices(vertices);
        }
        catch
        {
            return false;
        }

        if (vertices.Count == 0)
            return false;

        return TryBuildBounds(vertices, meshToTarget, out bounds);
    }

    private static bool TryGetBakedSkinnedLocalBounds(
        SkinnedMeshRenderer renderer,
        Matrix4x4 worldToVisual,
        out Bounds bounds)
    {
        bounds = default;
        SkinnedBoundsCache cache = SkinnedBoundsCaches.GetValue(renderer, _ => new SkinnedBoundsCache());

        try
        {
            renderer.BakeMesh(cache.mesh);
        }
        catch
        {
            return false;
        }

        cache.vertices.Clear();
        cache.mesh.GetVertices(cache.vertices);
        if (cache.vertices.Count == 0)
            return false;

        Matrix4x4 rendererLocalToVisual = worldToVisual * renderer.transform.localToWorldMatrix;
        bool hasRendererLocalBounds = TryBuildBounds(cache.vertices, rendererLocalToVisual, out Bounds rendererLocalBounds);
        bool hasWorldBounds = TryBuildBounds(cache.vertices, worldToVisual, out Bounds worldBounds);

        if (!hasRendererLocalBounds)
        {
            bounds = worldBounds;
            return hasWorldBounds;
        }

        if (!hasWorldBounds)
        {
            bounds = rendererLocalBounds;
            return true;
        }

        Bounds reference = TransformBounds(renderer.bounds, worldToVisual);
        bounds = GetBoundsCompatibilityScore(rendererLocalBounds, reference) <= GetBoundsCompatibilityScore(worldBounds, reference)
            ? rendererLocalBounds
            : worldBounds;
        return true;
    }

    private static bool TryBuildBounds(List<Vector3> vertices, Matrix4x4 transform, out Bounds bounds)
    {
        bounds = default;
        if (vertices == null || vertices.Count == 0)
            return false;

        Vector3 first = transform.MultiplyPoint3x4(vertices[0]);
        bounds = new Bounds(first, Vector3.zero);
        for (int i = 1; i < vertices.Count; i++)
            bounds.Encapsulate(transform.MultiplyPoint3x4(vertices[i]));

        return bounds.size.sqrMagnitude > 0.000001f;
    }

    private static Bounds TransformLocalBoundsToWorld(Transform transform, Bounds localBounds)
    {
        return TransformBounds(localBounds, transform.localToWorldMatrix);
    }

    private static Bounds TransformBounds(Bounds source, Matrix4x4 transform)
    {
        Vector3 min = source.min;
        Vector3 max = source.max;
        Vector3 first = transform.MultiplyPoint3x4(new Vector3(min.x, min.y, min.z));
        Bounds bounds = new Bounds(first, Vector3.zero);

        bounds.Encapsulate(transform.MultiplyPoint3x4(new Vector3(min.x, min.y, max.z)));
        bounds.Encapsulate(transform.MultiplyPoint3x4(new Vector3(min.x, max.y, min.z)));
        bounds.Encapsulate(transform.MultiplyPoint3x4(new Vector3(min.x, max.y, max.z)));
        bounds.Encapsulate(transform.MultiplyPoint3x4(new Vector3(max.x, min.y, min.z)));
        bounds.Encapsulate(transform.MultiplyPoint3x4(new Vector3(max.x, min.y, max.z)));
        bounds.Encapsulate(transform.MultiplyPoint3x4(new Vector3(max.x, max.y, min.z)));
        bounds.Encapsulate(transform.MultiplyPoint3x4(new Vector3(max.x, max.y, max.z)));

        return bounds;
    }

    private static float GetBoundsCompatibilityScore(Bounds candidate, Bounds reference)
    {
        float referenceHeight = Mathf.Max(0.001f, reference.size.y);
        float heightRatio = Mathf.Max(0.001f, candidate.size.y / referenceHeight);
        float heightScore = Mathf.Abs(Mathf.Log(heightRatio));
        float horizontalReference = Mathf.Max(
            0.001f,
            new Vector2(reference.extents.x, reference.extents.z).magnitude);
        float horizontalScore = new Vector2(
            candidate.center.x - reference.center.x,
            candidate.center.z - reference.center.z).magnitude / horizontalReference;
        float verticalScore = Mathf.Abs(candidate.center.y - reference.center.y) / referenceHeight;

        return horizontalScore * 2f + heightScore + verticalScore * 0.35f;
    }

    private static bool TryGetVisibleRendererBounds(Renderer renderer, out Bounds bounds)
    {
        bounds = renderer.bounds;
        if (bounds.size.sqrMagnitude <= 0.000001f)
            return false;

        if (renderer is SkinnedMeshRenderer skinnedRenderer &&
            skinnedRenderer.sharedMesh != null &&
            TryGetBakedSkinnedBounds(skinnedRenderer, out Bounds bakedBounds) &&
            IsBakedBoundsCompatible(bounds, bakedBounds))
        {
            bounds = WithBakedVerticalExtents(bounds, bakedBounds);
        }

        return true;
    }

    private static bool IsBakedBoundsCompatible(Bounds rendererBounds, Bounds bakedBounds)
    {
        if (bakedBounds.size.sqrMagnitude <= 0.000001f || rendererBounds.size.y <= 0.001f)
            return false;

        float heightRatio = bakedBounds.size.y / rendererBounds.size.y;
        if (heightRatio < MinimumBakedToRendererHeightRatio || heightRatio > MaximumBakedToRendererHeightRatio)
            return false;

        float maxVerticalCorrection = GetMaximumBakedVerticalCorrection(rendererBounds);
        return Mathf.Abs(bakedBounds.min.y - rendererBounds.min.y) <= maxVerticalCorrection &&
            Mathf.Abs(bakedBounds.max.y - rendererBounds.max.y) <= maxVerticalCorrection;
    }

    private static Bounds WithBakedVerticalExtents(Bounds rendererBounds, Bounds bakedBounds)
    {
        float maxVerticalCorrection = GetMaximumBakedVerticalCorrection(rendererBounds);
        float minY = Mathf.Clamp(
            bakedBounds.min.y,
            rendererBounds.min.y - maxVerticalCorrection,
            rendererBounds.min.y + maxVerticalCorrection);
        float maxY = Mathf.Clamp(
            bakedBounds.max.y,
            rendererBounds.max.y - maxVerticalCorrection,
            rendererBounds.max.y + maxVerticalCorrection);

        if (maxY <= minY + 0.001f)
            return rendererBounds;

        Bounds adjusted = rendererBounds;
        adjusted.SetMinMax(
            new Vector3(rendererBounds.min.x, minY, rendererBounds.min.z),
            new Vector3(rendererBounds.max.x, maxY, rendererBounds.max.z));
        return adjusted;
    }

    private static float GetMaximumBakedVerticalCorrection(Bounds rendererBounds)
    {
        return Mathf.Clamp(
            rendererBounds.size.y * BakedBoundsVerticalCorrectionScale,
            MinimumBakedBoundsVerticalCorrection,
            MaximumBakedBoundsVerticalCorrection);
    }

    private static bool TryGetBakedSkinnedBounds(SkinnedMeshRenderer renderer, out Bounds bounds)
    {
        bounds = default;
        SkinnedBoundsCache cache = SkinnedBoundsCaches.GetValue(renderer, _ => new SkinnedBoundsCache());
        int currentFrame = Time.frameCount;
        if (cache.frame == currentFrame && cache.hasBounds)
        {
            bounds = cache.bounds;
            return true;
        }

        try
        {
            renderer.BakeMesh(cache.mesh);
        }
        catch
        {
            return false;
        }

        cache.vertices.Clear();
        cache.mesh.GetVertices(cache.vertices);
        if (cache.vertices.Count == 0)
            return false;

        Matrix4x4 localToWorld = renderer.transform.localToWorldMatrix;
        Vector3 first = localToWorld.MultiplyPoint3x4(cache.vertices[0]);
        bounds = new Bounds(first, Vector3.zero);
        for (int i = 1; i < cache.vertices.Count; i++)
            bounds.Encapsulate(localToWorld.MultiplyPoint3x4(cache.vertices[i]));

        cache.bounds = bounds;
        cache.hasBounds = true;
        cache.frame = currentFrame;
        return true;
    }

    public static void KeepBottomAboveGround(
        Transform visual,
        Transform ignoredRoot,
        Renderer[] renderers,
        float clearance,
        float probeHeight,
        float probeDistance,
        LayerMask groundMask,
        float sharpness)
    {
        KeepBottomAboveGround(
            visual,
            ignoredRoot,
            renderers,
            clearance,
            probeHeight,
            probeDistance,
            groundMask,
            sharpness,
            maxDownStep: 0.35f,
            maxUpStep: 1.25f,
            out _);
    }

    public static bool KeepBottomAboveGround(
        Transform visual,
        Transform ignoredRoot,
        Renderer[] renderers,
        float clearance,
        float probeHeight,
        float probeDistance,
        LayerMask groundMask,
        float sharpness,
        float maxDownStep,
        float maxUpStep,
        out GroundSample ground)
    {
        ground = CreateMissingSample();

        if (!TrySampleGroundForVisual(
                visual,
                ignoredRoot,
                renderers,
                probeHeight,
                probeDistance,
                groundMask,
                sharpness,
                out ground))
        {
            return false;
        }

        if (!TryGetRendererBounds(renderers, out Bounds bounds))
            return false;

        float desiredBottom = ground.y + clearance;
        float delta = desiredBottom - bounds.min.y;
        if (delta > GroundingDeadZone)
        {
            float clampedDelta = maxUpStep > 0f ? Mathf.Min(delta, maxUpStep) : delta;
            visual.position += Vector3.up * clampedDelta;
            return true;
        }

        if (delta < -DownwardSettlingSlack)
        {
            float clampedDelta = maxDownStep > 0f ? Mathf.Max(delta, -maxDownStep) : delta;
            visual.position += Vector3.up * (clampedDelta * SmoothFactor(sharpness));
        }

        return true;
    }

    public static bool TryGetGroundY(
        Vector3 worldPosition,
        Transform ignoredRoot,
        float probeHeight,
        float probeDistance,
        LayerMask groundMask,
        out float groundY)
    {
        if (TrySampleGround(worldPosition, ignoredRoot, probeHeight, probeDistance, groundMask, out GroundSample sample))
        {
            groundY = sample.y;
            return true;
        }

        groundY = float.NegativeInfinity;
        return false;
    }

    /// <summary>
    /// Samples the highest valid Terrain or raycast ground point at one X/Z location.
    /// </summary>
    public static bool TrySampleGround(
        Vector3 worldPosition,
        Transform ignoredRoot,
        float probeHeight,
        float probeDistance,
        LayerMask groundMask,
        out GroundSample sample)
    {
        return TrySampleGround(worldPosition, ignoredRoot, null, probeHeight, probeDistance, groundMask, out sample);
    }

    /// <summary>
    /// Samples a compact renderer footprint and averages normals for stable visual slope tilt.
    /// </summary>
    public static bool TrySampleGroundFootprint(
        Vector3 origin,
        Transform visual,
        Transform ignoredRoot,
        Bounds bounds,
        float probeHeight,
        float probeDistance,
        LayerMask groundMask,
        out GroundSample sample)
    {
        GroundSampleAccumulator accumulator = default;

        TryAddGroundSample(origin, visual, ignoredRoot, probeHeight, probeDistance, groundMask, ref accumulator);

        Vector3 boundsCenter = new Vector3(bounds.center.x, origin.y, bounds.center.z);
        if (HorizontalDistanceSquared(origin, boundsCenter) > 0.01f)
            TryAddGroundSample(boundsCenter, visual, ignoredRoot, probeHeight, probeDistance, groundMask, ref accumulator);

        Vector3 forward = FlattenedDirection(visual != null ? visual.forward : Vector3.forward, Vector3.forward);
        Vector3 right = FlattenedDirection(visual != null ? visual.right : Vector3.right, Vector3.right);
        float forwardReach = EstimateFootprintReach(bounds, forward);
        float rightReach = EstimateFootprintReach(bounds, right);

        if (forwardReach > 0f)
        {
            TryAddGroundSample(origin + forward * forwardReach, visual, ignoredRoot, probeHeight, probeDistance, groundMask, ref accumulator);
            TryAddGroundSample(origin - forward * forwardReach, visual, ignoredRoot, probeHeight, probeDistance, groundMask, ref accumulator);
        }

        if (rightReach > 0f)
        {
            TryAddGroundSample(origin + right * rightReach, visual, ignoredRoot, probeHeight, probeDistance, groundMask, ref accumulator);
            TryAddGroundSample(origin - right * rightReach, visual, ignoredRoot, probeHeight, probeDistance, groundMask, ref accumulator);
        }

        return accumulator.TryGetSample(out sample);
    }

    public static Quaternion GetSlopeRotation(
        Quaternion yawRotation,
        Vector3 groundNormal,
        float maxSlopeAngle)
    {
        if (groundNormal.sqrMagnitude < 0.0001f)
            return yawRotation;

        Vector3 normal = SafeNormal(groundNormal);
        float slopeAngle = Vector3.Angle(Vector3.up, normal);
        if (slopeAngle > maxSlopeAngle && slopeAngle > 0.001f)
            normal = Vector3.Slerp(Vector3.up, normal, maxSlopeAngle / slopeAngle).normalized;

        Vector3 forward = Vector3.ProjectOnPlane(yawRotation * Vector3.forward, normal);
        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.ProjectOnPlane(yawRotation * Vector3.right, normal);
        if (forward.sqrMagnitude < 0.0001f)
            return Quaternion.FromToRotation(Vector3.up, normal) * yawRotation;

        return Quaternion.LookRotation(forward.normalized, normal);
    }

    private static void TryAddGroundSample(
        Vector3 worldPosition,
        Transform visual,
        Transform ignoredRoot,
        float probeHeight,
        float probeDistance,
        LayerMask groundMask,
        ref GroundSampleAccumulator accumulator)
    {
        if (TrySampleGround(worldPosition, ignoredRoot, visual, probeHeight, probeDistance, groundMask, out GroundSample sample))
            accumulator.Add(sample);
    }

    private static bool TrySampleGround(
        Vector3 worldPosition,
        Transform ignoredRoot,
        Transform secondaryIgnoredRoot,
        float probeHeight,
        float probeDistance,
        LayerMask groundMask,
        out GroundSample sample)
    {
        bool hasTerrain = TrySampleTerrain(worldPosition, out GroundSample terrainSample);
        bool hasRaycast = TrySampleRaycast(
            worldPosition,
            ignoredRoot,
            secondaryIgnoredRoot,
            probeHeight,
            probeDistance,
            groundMask,
            out GroundSample raycastSample);

        if (hasTerrain && hasRaycast)
        {
            sample = SelectTerrainPreferredSample(terrainSample, raycastSample);
            return true;
        }

        if (hasRaycast)
        {
            sample = raycastSample;
            return true;
        }

        if (hasTerrain)
        {
            sample = terrainSample;
            return true;
        }

        sample = CreateMissingSample();
        return false;
    }

    private static bool TrySampleTerrain(Vector3 worldPosition, out GroundSample sample)
    {
        GroundSampleAccumulator accumulator = default;
        Terrain[] terrains = Terrain.activeTerrains;

        foreach (Terrain terrain in terrains)
        {
            if (terrain == null || terrain.terrainData == null)
                continue;

            Vector3 terrainPosition = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            if (size.x <= 0f || size.z <= 0f)
                continue;

            bool insideX = worldPosition.x >= terrainPosition.x && worldPosition.x <= terrainPosition.x + size.x;
            bool insideZ = worldPosition.z >= terrainPosition.z && worldPosition.z <= terrainPosition.z + size.z;
            if (!insideX || !insideZ)
                continue;

            float sampledY = terrainPosition.y + terrain.SampleHeight(worldPosition);
            float normalizedX = Mathf.Clamp01((worldPosition.x - terrainPosition.x) / size.x);
            float normalizedZ = Mathf.Clamp01((worldPosition.z - terrainPosition.z) / size.z);
            Vector3 localNormal = terrain.terrainData.GetInterpolatedNormal(normalizedX, normalizedZ);
            Vector3 worldNormal = terrain.transform.TransformDirection(localNormal);
            accumulator.Add(CreateSample(sampledY, worldNormal));
        }

        return accumulator.TryGetSample(out sample);
    }

    private static bool TrySampleRaycast(
        Vector3 worldPosition,
        Transform ignoredRoot,
        Transform secondaryIgnoredRoot,
        float probeHeight,
        float probeDistance,
        LayerMask groundMask,
        out GroundSample sample)
    {
        if (groundMask.value == 0)
        {
            sample = CreateMissingSample();
            return false;
        }

        float safeProbeHeight = Mathf.Max(MinimumProbeHeight, probeHeight);
        float safeProbeDistance = Mathf.Max(MinimumProbeDistance, probeDistance);
        Vector3 rayOrigin = worldPosition + Vector3.up * safeProbeHeight;
        float rayDistance = safeProbeHeight + safeProbeDistance;

        int hitCount = Physics.RaycastNonAlloc(
            rayOrigin,
            Vector3.down,
            RaycastHits,
            rayDistance,
            groundMask,
            QueryTriggerInteraction.Ignore);

        if (hitCount >= RaycastHits.Length)
        {
            RaycastHit[] allHits = Physics.RaycastAll(
                rayOrigin,
                Vector3.down,
                rayDistance,
                groundMask,
                QueryTriggerInteraction.Ignore);

            return TrySelectRaycastSample(allHits, allHits.Length, ignoredRoot, secondaryIgnoredRoot, out sample);
        }

        return TrySelectRaycastSample(RaycastHits, hitCount, ignoredRoot, secondaryIgnoredRoot, out sample);
    }

    private static bool TrySelectRaycastSample(
        RaycastHit[] hits,
        int hitCount,
        Transform ignoredRoot,
        Transform secondaryIgnoredRoot,
        out GroundSample sample)
    {
        float bestDistance = float.PositiveInfinity;
        RaycastHit bestHit = default;
        bool found = false;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = hits[i];
            if (hit.collider == null || hit.normal.y < MinimumGroundNormalY)
                continue;

            if (IsIgnoredCollider(hit.collider, ignoredRoot) || IsIgnoredCollider(hit.collider, secondaryIgnoredRoot))
                continue;

            if (!IsWalkableGroundCollider(hit.collider))
                continue;

            if (!found || hit.distance < bestDistance)
            {
                bestHit = hit;
                bestDistance = hit.distance;
                found = true;
            }
        }

        if (!found)
        {
            sample = CreateMissingSample();
            return false;
        }

        sample = CreateSample(bestHit.point.y, bestHit.normal);
        return true;
    }

    private static GroundSample SelectTerrainPreferredSample(GroundSample terrainSample, GroundSample raycastSample)
    {
        float heightDifference = raycastSample.y - terrainSample.y;
        if (Mathf.Abs(heightDifference) <= HeightSelectionEpsilon)
        {
            return new GroundSample
            {
                found = true,
                y = Mathf.Max(terrainSample.y, raycastSample.y),
                normal = SafeNormal(terrainSample.normal + raycastSample.normal)
            };
        }

        if (heightDifference > TerrainPreferenceHeightTolerance)
            return raycastSample;

        return terrainSample;
    }

    private static GroundSample StabilizeSample(Transform visual, GroundSample sample, float sharpness)
    {
        GroundingState state = GroundingStates.GetValue(visual, _ => new GroundingState());
        int frame = Time.frameCount;
        bool stale = !state.hasSample || frame - state.frame > StaleStateFrameGap;

        if (stale)
        {
            state.groundY = sample.y;
            state.normal = SafeNormal(sample.normal);
            state.hasSample = true;
        }
        else
        {
            if (sample.y >= state.groundY || sharpness <= 0f)
                state.groundY = sample.y;
            else
                state.groundY = Mathf.Lerp(state.groundY, sample.y, SmoothFactor(sharpness));

            float normalFactor = SmoothFactor(Mathf.Max(sharpness, 12f));
            state.normal = SafeNormal(Vector3.Slerp(state.normal, sample.normal, normalFactor));
        }

        state.frame = frame;

        sample.y = state.groundY;
        sample.normal = state.normal;
        return sample;
    }

    private static GroundSample CreateSample(float y, Vector3 normal)
    {
        return new GroundSample
        {
            found = true,
            y = y,
            normal = SafeNormal(normal)
        };
    }

    private static GroundSample CreateMissingSample()
    {
        return new GroundSample
        {
            found = false,
            y = float.NegativeInfinity,
            normal = Vector3.up
        };
    }

    private static bool IsIgnoredCollider(Collider collider, Transform ignoredRoot)
    {
        if (collider == null || ignoredRoot == null)
            return false;

        if (IsSameOrChild(collider.transform, ignoredRoot))
            return true;

        Rigidbody attachedRigidbody = collider.attachedRigidbody;
        return attachedRigidbody != null && IsSameOrChild(attachedRigidbody.transform, ignoredRoot);
    }

    private static bool IsWalkableGroundCollider(Collider collider)
    {
        if (collider == null)
            return false;

        if (collider is TerrainCollider)
            return true;

        Transform current = collider.transform;
        while (current != null)
        {
            if (current.GetComponent<SquirrelAgent>() != null ||
                current.GetComponent<PredatorAgent>() != null ||
                current.GetComponent<Acorn>() != null ||
                current.GetComponent<Obstacle>() != null ||
                current.GetComponent<SafeZone>() != null)
            {
                return false;
            }

            string tag = current.tag;
            if (tag == ObstacleTag || tag == AcornTag || tag == SafeZoneTag)
                return false;

            current = current.parent;
        }

        return true;
    }

    private static bool IsSameOrChild(Transform candidate, Transform root)
    {
        return candidate != null && root != null && (candidate == root || candidate.IsChildOf(root));
    }

    private static Vector3 FlattenedDirection(Vector3 direction, Vector3 fallback)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
            direction = fallback;

        direction.y = 0f;
        return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
    }

    private static float EstimateFootprintReach(Bounds bounds, Vector3 direction)
    {
        float horizontalExtent =
            Mathf.Abs(direction.x) * bounds.extents.x +
            Mathf.Abs(direction.z) * bounds.extents.z;

        if (horizontalExtent < MinimumFootprintProbeReach)
            return 0f;

        return Mathf.Clamp(horizontalExtent * FootprintProbeScale, MinimumFootprintProbeReach, MaximumFootprintProbeReach);
    }

    private static float HorizontalDistanceSquared(Vector3 a, Vector3 b)
    {
        float x = a.x - b.x;
        float z = a.z - b.z;
        return x * x + z * z;
    }

    private static Vector3 SafeNormal(Vector3 normal)
    {
        if (normal.sqrMagnitude < 0.0001f)
            return Vector3.up;

        normal.Normalize();
        return normal.y >= MinimumGroundNormalY ? normal : Vector3.up;
    }

    private static float SmoothFactor(float sharpness)
    {
        if (sharpness <= 0f || Time.deltaTime <= 0f)
            return 1f;

        return 1f - Mathf.Exp(-sharpness * Time.deltaTime);
    }

    private struct GroundSampleAccumulator
    {
        private bool hasSample;
        private float maxY;
        private Vector3 normalSum;

        public void Add(GroundSample sample)
        {
            if (!sample.found)
                return;

            if (!hasSample || sample.y > maxY)
                maxY = sample.y;

            normalSum += SafeNormal(sample.normal);
            hasSample = true;
        }

        public bool TryGetSample(out GroundSample sample)
        {
            if (!hasSample)
            {
                sample = CreateMissingSample();
                return false;
            }

            sample = CreateSample(maxY, normalSum);
            return true;
        }
    }
}
