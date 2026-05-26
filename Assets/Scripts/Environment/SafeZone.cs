using System;
using UnityEngine;

// Attach to a trigger collider. Squirrel tag detection is handled by SquirrelAgent.
[ExecuteAlways]
[RequireComponent(typeof(Collider))]
public class SafeZone : MonoBehaviour
{
    [Header("Editor Gizmo")]
    public Color gizmoColor = new Color(0.2f, 0.8f, 0.45f, 0.25f);

    [Header("Visible Marker")]
    public bool showMarker = true;
    public Color markerColor = new Color(0.15f, 1f, 0.25f, 1f);

    [Header("Visual Styling")]
    public bool applyNaturalColors = true;
    public Color bushColor = new Color(0.22f, 0.48f, 0.18f, 1f);
    public Color logColor = new Color(0.34f, 0.2f, 0.1f, 1f);
    public Color rockColor = new Color(0.42f, 0.43f, 0.39f, 1f);
    public Color leafColor = new Color(0.18f, 0.42f, 0.14f, 1f);
    public Color barkColor = new Color(0.38f, 0.24f, 0.13f, 1f);

    [Header("Footprint")]
    public bool fitFootprintToVisual = true;
    public Vector2 footprintPadding = new Vector2(0.7f, 0.7f);
    public float footprintHeightPadding = 0.4f;

    [Header("Terrain Placement")]
    public bool snapToTerrain = true;
    public float terrainOffset = 0.08f;

    private const string MarkerName = "SafeZone_VisibleMarker";
    private const float GroundMarkerOffset = 0.03f;
    private static bool isEnsuringDefaultSafeZones;

    void Awake()
    {
        ConfigureZone();
    }

    void OnEnable()
    {
        ConfigureZone();
    }

    void OnValidate()
    {
        ConfigureZone();
    }

    void LateUpdate()
    {
        AlignVisuals();
    }

    private void ConfigureZone()
    {
        gameObject.tag = "Safezone";
        EnsureDefaultSafeZones();

        Collider zoneCollider = GetFootprintCollider();

        SnapToTerrain(zoneCollider);
        Transform visual = AlignVisuals();
        StyleVisual(visual);
        UpdateFootprint(zoneCollider, visual);
        UpdateMarker(zoneCollider);
    }

    private Collider GetFootprintCollider()
    {
        BoxCollider box = GetComponent<BoxCollider>();
        if (box == null)
            box = gameObject.AddComponent<BoxCollider>();

        box.isTrigger = true;

        SphereCollider sphere = GetComponent<SphereCollider>();
        if (sphere != null)
            sphere.enabled = false;

        return box;
    }

    private Transform AlignVisuals()
    {
        string visualName = GetMatchingVisualName();
        if (string.IsNullOrEmpty(visualName)) return null;

        Transform visual = transform.Find(visualName);
        if (visual != null) return visual;

        visual = transform.parent != null ? transform.parent.Find(visualName) : null;
        if (visual == null)
        {
            GameObject visualObject = GameObject.Find(visualName);
            visual = visualObject != null ? visualObject.transform : null;
        }

        if (visual != null)
            visual.position = transform.position;

        return visual;
    }

    private string GetMatchingVisualName()
    {
        string normalizedName = gameObject.name.Replace(" ", "_");

        if (TryGetVisualName(normalizedName, "SafeZone_Bush_", "BushVisual_", out string bushVisualName))
        {
            return bushVisualName;
        }

        if (TryGetVisualName(normalizedName, "SafeZone_Log_", "LogVisual_", out string logVisualName))
        {
            return logVisualName;
        }

        if (TryGetVisualName(normalizedName, "SafeZone_Rock_", "RockVisual_", out string rockVisualName))
        {
            return rockVisualName;
        }

        return null;
    }

    private static bool TryGetVisualName(string zoneName, string zonePrefix, string visualPrefix, out string visualName)
    {
        if (zoneName.StartsWith(zonePrefix, StringComparison.OrdinalIgnoreCase))
        {
            string number = zoneName.Substring(zonePrefix.Length);
            visualName = $"{visualPrefix}{number}";
            return true;
        }

        visualName = null;
        return false;
    }

    private void StyleVisual(Transform visual)
    {
        if (!applyNaturalColors || visual == null) return;

        Color tint = visual.name.Contains("Log")
            ? logColor
            : visual.name.Contains("Rock")
                ? rockColor
                : visual.name.Contains("Bush")
                    ? bushColor
                    : leafColor;
        Renderer[] renderers = visual.GetComponentsInChildren<Renderer>();

        foreach (Renderer visualRenderer in renderers)
        {
            MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
            visualRenderer.GetPropertyBlock(propertyBlock);

            Color rendererTint = visualRenderer.name.ToLowerInvariant().Contains("trunk")
                ? barkColor
                : tint;

            propertyBlock.SetColor("_BaseColor", rendererTint);
            propertyBlock.SetColor("_Color", rendererTint);
            visualRenderer.SetPropertyBlock(propertyBlock);
        }
    }

    private void UpdateFootprint(Collider zoneCollider, Transform visual)
    {
        BoxCollider box = zoneCollider as BoxCollider;
        if (!fitFootprintToVisual || box == null) return;

        Bounds? visualBounds = GetVisualBounds(visual);
        if (!visualBounds.HasValue)
        {
            box.center = Vector3.zero;
            box.size = new Vector3(4f, 1.4f, 4f);
            return;
        }

        Bounds bounds = visualBounds.Value;
        Vector3 localCenter = transform.InverseTransformPoint(bounds.center);
        float fittedHeight = Mathf.Max(1f, bounds.size.y + footprintHeightPadding);
        localCenter.y = fittedHeight * 0.5f;

        box.center = localCenter;
        box.size = new Vector3(
            Mathf.Max(1f, bounds.size.x + footprintPadding.x),
            fittedHeight,
            Mathf.Max(1f, bounds.size.z + footprintPadding.y));
    }

    private Bounds? GetVisualBounds(Transform visual)
    {
        if (visual == null) return null;

        Renderer[] renderers = visual.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return null;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        return bounds;
    }

    private void SnapToTerrain(Collider zoneCollider)
    {
        if (!snapToTerrain) return;

        Terrain activeTerrain = Terrain.activeTerrain;
        if (activeTerrain == null) return;

        Vector3 position = transform.position;
        Vector3 terrainPosition = activeTerrain.transform.position;
        TerrainData terrainData = activeTerrain.terrainData;

        bool isInsideTerrain =
            position.x >= terrainPosition.x &&
            position.z >= terrainPosition.z &&
            position.x <= terrainPosition.x + terrainData.size.x &&
            position.z <= terrainPosition.z + terrainData.size.z;

        if (!isInsideTerrain) return;

        float groundY = terrainPosition.y + activeTerrain.SampleHeight(position);
        position.y = groundY + terrainOffset;
        transform.position = position;
    }

    private void UpdateMarker(Collider zoneCollider)
    {
        Transform marker = transform.Find(MarkerName);

        if (!showMarker)
        {
            if (marker != null)
                marker.gameObject.SetActive(false);
            return;
        }

        if (marker == null)
        {
            GameObject markerObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            markerObject.name = MarkerName;
            markerObject.transform.SetParent(transform, false);

            Collider markerCollider = markerObject.GetComponent<Collider>();
            if (markerCollider != null)
            {
                if (Application.isPlaying)
                    Destroy(markerCollider);
                else
                    DestroyImmediate(markerCollider);
            }

            marker = markerObject.transform;
        }

        marker.gameObject.SetActive(true);
        Vector3 markerPosition = zoneCollider is SphereCollider sphere ? sphere.center : Vector3.zero;
        if (zoneCollider is BoxCollider box)
            markerPosition = new Vector3(box.center.x, GroundMarkerOffset - terrainOffset, box.center.z);
        else
            markerPosition.y = GroundMarkerOffset - terrainOffset;

        marker.localPosition = markerPosition;
        marker.localRotation = Quaternion.identity;

        if (zoneCollider is BoxCollider boxCollider)
        {
            marker.localScale = new Vector3(boxCollider.size.x, 0.12f, boxCollider.size.z);
        }
        else
        {
            float radius = zoneCollider is SphereCollider sphereCollider ? sphereCollider.radius : 1f;
            marker.localScale = new Vector3(radius * 2f, 0.12f, radius * 2f);
        }

        Renderer renderer = marker.GetComponent<Renderer>();
        if (renderer == null) return;

        Material material = renderer.sharedMaterial;
        if (material == null || material.name != "SafeZone Marker Material")
        {
            Shader markerShader = Shader.Find("Universal Render Pipeline/Lit");
            if (markerShader == null)
                markerShader = Shader.Find("Standard");

            material = new Material(markerShader)
            {
                name = "SafeZone Marker Material"
            };
            renderer.sharedMaterial = material;
        }

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", markerColor);
        else
            material.color = markerColor;
    }

    private void EnsureDefaultSafeZones()
    {
        if (isEnsuringDefaultSafeZones || !gameObject.name.StartsWith("SafeZone_Bush_", StringComparison.OrdinalIgnoreCase))
            return;

        Transform root = transform.parent;
        if (root == null || root.name != "SafeZones")
            return;

        isEnsuringDefaultSafeZones = true;
        RemoveExtraSafeZones(root);

        EnsureBushSafeZone(root, "SafeZone_Bush_1", "BushVisual_1", TerrainPoint(0.12f, 0.18f), new Vector3(0.85f, 0.55f, 0.7f));
        EnsureBushSafeZone(root, "SafeZone_Bush_2", "BushVisual_2", TerrainPoint(0.36f, 0.16f), new Vector3(0.95f, 0.6f, 0.9f));
        EnsureBushSafeZone(root, "SafeZone_Bush_3", "BushVisual_3", TerrainPoint(0.62f, 0.18f), new Vector3(0.75f, 0.5f, 0.95f));
        EnsureBushSafeZone(root, "SafeZone_Bush_4", "BushVisual_4", TerrainPoint(0.86f, 0.22f), new Vector3(0.8f, 0.55f, 0.85f));

        EnsureLogSafeZone(root, "SafeZone_Log_1", "LogVisual_1", TerrainPoint(0.12f, 0.82f), 0f, new Vector3(1.25f, 6.2f, 1.25f));
        EnsureLogSafeZone(root, "SafeZone_Log_2", "LogVisual_2", TerrainPoint(0.88f, 0.78f), 90f, new Vector3(1.35f, 6.8f, 1.35f));

        EnsureRockSafeZone(root, "SafeZone_Rock_1", "RockVisual_1", TerrainPoint(0.08f, 0.34f), 12f, new Vector3(1.5f, 0.9f, 1.25f));
        EnsureRockSafeZone(root, "SafeZone_Rock_2", "RockVisual_2", TerrainPoint(0.30f, 0.30f), 48f, new Vector3(1.35f, 0.8f, 1.55f));
        EnsureRockSafeZone(root, "SafeZone_Rock_3", "RockVisual_3", TerrainPoint(0.56f, 0.28f), 95f, new Vector3(1.65f, 0.95f, 1.3f));
        isEnsuringDefaultSafeZones = false;
    }

    private void RemoveExtraSafeZones(Transform root)
    {
        RemoveSafeZone(root, "SafeZone_Bush_5");
        RemoveSafeZone(root, "SafeZone_Bush_6");
        RemoveSafeZone(root, "SafeZone_Bush_7");
        RemoveSafeZone(root, "SafeZone_Bush_8");
        RemoveSafeZone(root, "SafeZone_Bush_9");
        RemoveSafeZone(root, "SafeZone_Log_3");
        RemoveSafeZone(root, "SafeZone_Log_4");
        RemoveSafeZone(root, "SafeZone_Log_5");
        RemoveSafeZone(root, "SafeZone_Rock_4");
        RemoveSafeZone(root, "SafeZone_Rock_5");
        RemoveSafeZone(root, "SafeZone_Rock_6");
    }

    private void RemoveSafeZone(Transform root, string safeZoneName)
    {
        Transform safeZone = root.Find(safeZoneName);
        if (safeZone == null) return;

        if (Application.isPlaying)
            Destroy(safeZone.gameObject);
        else
            DestroyImmediate(safeZone.gameObject);
    }

    private void EnsureBushSafeZone(Transform root, string safeZoneName, string visualName, Vector3 position, Vector3 visualScale)
    {
        Transform safeZone = root.Find(safeZoneName);
        if (safeZone == null)
        {
            GameObject safeZoneObject = new GameObject(safeZoneName);
            safeZoneObject.transform.SetParent(root, false);
            safeZone = safeZoneObject.transform;
        }

        safeZone.gameObject.tag = "Safezone";
        safeZone.position = SampleTerrainPosition(position);
        safeZone.rotation = Quaternion.identity;
        safeZone.localScale = Vector3.one;

        SphereCollider trigger = safeZone.GetComponent<SphereCollider>();
        if (trigger == null && safeZone.GetComponent<BoxCollider>() == null)
            trigger = safeZone.gameObject.AddComponent<SphereCollider>();

        if (trigger != null)
        {
            trigger.isTrigger = true;
            trigger.radius = 2.5f;
        }

        Transform visual = safeZone.Find(visualName);
        if (visual == null)
            visual = CreateBushVisual(visualName, safeZone);

        visual.localPosition = Vector3.zero;
        visual.localRotation = Quaternion.identity;
        visual.localScale = visualScale;

        if (safeZone.GetComponent<SafeZone>() == null)
            safeZone.gameObject.AddComponent<SafeZone>();
    }

    private void EnsureLogSafeZone(Transform root, string safeZoneName, string visualName, Vector3 position, float yaw, Vector3 visualScale)
    {
        Transform safeZone = root.Find(safeZoneName);
        if (safeZone == null)
        {
            GameObject safeZoneObject = new GameObject(safeZoneName);
            safeZoneObject.transform.SetParent(root, false);
            safeZone = safeZoneObject.transform;
        }

        safeZone.gameObject.tag = "Safezone";
        safeZone.position = SampleTerrainPosition(position);
        safeZone.rotation = Quaternion.identity;
        safeZone.localScale = Vector3.one;

        SphereCollider trigger = safeZone.GetComponent<SphereCollider>();
        if (trigger == null && safeZone.GetComponent<BoxCollider>() == null)
            trigger = safeZone.gameObject.AddComponent<SphereCollider>();

        if (trigger != null)
        {
            trigger.isTrigger = true;
            trigger.radius = 3.5f;
        }

        Transform visual = safeZone.Find(visualName);
        if (visual == null)
            visual = CreateLogVisual(visualName, safeZone);

        ConfigureLogVisual(visual, visualName);
        visual.localPosition = Vector3.zero;
        visual.localRotation = Quaternion.Euler(0f, yaw, 90f);
        visual.localScale = visualScale;

        if (safeZone.GetComponent<SafeZone>() == null)
            safeZone.gameObject.AddComponent<SafeZone>();
    }

    private void EnsureRockSafeZone(Transform root, string safeZoneName, string visualName, Vector3 position, float yaw, Vector3 visualScale)
    {
        Transform safeZone = root.Find(safeZoneName);
        if (safeZone == null)
        {
            GameObject safeZoneObject = new GameObject(safeZoneName);
            safeZoneObject.transform.SetParent(root, false);
            safeZone = safeZoneObject.transform;
        }

        safeZone.gameObject.tag = "Safezone";
        safeZone.position = SampleTerrainPosition(position);
        safeZone.rotation = Quaternion.identity;
        safeZone.localScale = Vector3.one;

        SphereCollider trigger = safeZone.GetComponent<SphereCollider>();
        if (trigger == null && safeZone.GetComponent<BoxCollider>() == null)
            trigger = safeZone.gameObject.AddComponent<SphereCollider>();

        if (trigger != null)
        {
            trigger.isTrigger = true;
            trigger.radius = 3f;
        }

        Transform visual = safeZone.Find(visualName);
        if (visual == null)
            visual = CreateRockVisual(visualName, safeZone);

        ConfigureRockVisual(visual, visualName);
        visual.localPosition = Vector3.zero;
        visual.localRotation = Quaternion.Euler(0f, yaw, 0f);
        visual.localScale = visualScale;

        if (safeZone.GetComponent<SafeZone>() == null)
            safeZone.gameObject.AddComponent<SafeZone>();
    }


    private Vector3 SampleTerrainPosition(Vector3 position)
    {
        Terrain activeTerrain = Terrain.activeTerrain;
        if (activeTerrain == null) return position;

        position.y = activeTerrain.transform.position.y + activeTerrain.SampleHeight(position) + terrainOffset;
        return position;
    }

    private Vector3 TerrainPoint(float normalizedX, float normalizedZ)
    {
        Terrain activeTerrain = Terrain.activeTerrain;
        if (activeTerrain == null)
            return new Vector3(normalizedX * 100f, 0f, normalizedZ * 100f);

        TerrainData terrainData = activeTerrain.terrainData;
        Vector3 origin = activeTerrain.transform.position;
        return new Vector3(
            origin.x + terrainData.size.x * normalizedX,
            0f,
            origin.z + terrainData.size.z * normalizedZ);
    }

    private Transform CreateLogVisual(string visualName, Transform parent)
    {
        GameObject visualObject = new GameObject(visualName);
        visualObject.transform.SetParent(parent, false);
        ConfigureLogVisual(visualObject.transform, visualName);
        return visualObject.transform;
    }

    private void ConfigureLogVisual(Transform visual, string visualName)
    {
        Collider rootCollider = visual.GetComponent<Collider>();
        if (rootCollider != null)
        {
            if (Application.isPlaying)
                Destroy(rootCollider);
            else
                DestroyImmediate(rootCollider);
        }

        MeshRenderer rootRenderer = visual.GetComponent<MeshRenderer>();
        if (rootRenderer != null)
        {
            if (Application.isPlaying)
                Destroy(rootRenderer);
            else
                DestroyImmediate(rootRenderer);
        }

        MeshFilter rootFilter = visual.GetComponent<MeshFilter>();
        if (rootFilter != null)
        {
            if (Application.isPlaying)
                Destroy(rootFilter);
            else
                DestroyImmediate(rootFilter);
        }

        EnsureLogCylinder(visual, $"{visualName}_Body", Vector3.zero, Quaternion.identity, new Vector3(1f, 1f, 1f), CreateLogMaterial());
        EnsureLogCylinder(visual, $"{visualName}_CutA", new Vector3(0f, -0.52f, 0f), Quaternion.identity, new Vector3(1.04f, 0.06f, 1.04f), CreateLogEndMaterial());
        EnsureLogCylinder(visual, $"{visualName}_CutB", new Vector3(0f, 0.52f, 0f), Quaternion.identity, new Vector3(1.04f, 0.06f, 1.04f), CreateLogEndMaterial());
    }

    private void EnsureLogCylinder(Transform parent, string pieceName, Vector3 localPosition, Quaternion localRotation, Vector3 localScale, Material material)
    {
        Transform piece = parent.Find(pieceName);
        GameObject pieceObject;

        if (piece == null)
        {
            pieceObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pieceObject.name = pieceName;
            pieceObject.transform.SetParent(parent, false);
        }
        else
        {
            pieceObject = piece.gameObject;
        }

        pieceObject.transform.localPosition = localPosition;
        pieceObject.transform.localRotation = localRotation;
        pieceObject.transform.localScale = localScale;

        Collider visualCollider = pieceObject.GetComponent<Collider>();
        if (visualCollider != null)
        {
            if (Application.isPlaying)
                Destroy(visualCollider);
            else
                DestroyImmediate(visualCollider);
        }

        Renderer renderer = pieceObject.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial = material;
    }

    private Transform CreateBushVisual(string visualName, Transform parent)
    {
        GameObject visualObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        visualObject.name = visualName;
        visualObject.transform.SetParent(parent, false);

        Collider visualCollider = visualObject.GetComponent<Collider>();
        if (visualCollider != null)
        {
            if (Application.isPlaying)
                Destroy(visualCollider);
            else
                DestroyImmediate(visualCollider);
        }

        Renderer renderer = visualObject.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial = CreateBushMaterial();

        return visualObject.transform;
    }

    private Transform CreateRockVisual(string visualName, Transform parent)
    {
        GameObject visualObject = new GameObject(visualName);
        visualObject.transform.SetParent(parent, false);
        ConfigureRockVisual(visualObject.transform, visualName);
        return visualObject.transform;
    }

    private void ConfigureRockVisual(Transform visual, string visualName)
    {
        Collider rootCollider = visual.GetComponent<Collider>();
        if (rootCollider != null)
        {
            if (Application.isPlaying)
                Destroy(rootCollider);
            else
                DestroyImmediate(rootCollider);
        }

        MeshRenderer rootRenderer = visual.GetComponent<MeshRenderer>();
        if (rootRenderer != null)
        {
            if (Application.isPlaying)
                Destroy(rootRenderer);
            else
                DestroyImmediate(rootRenderer);
        }

        MeshFilter rootFilter = visual.GetComponent<MeshFilter>();
        if (rootFilter != null)
        {
            if (Application.isPlaying)
                Destroy(rootFilter);
            else
                DestroyImmediate(rootFilter);
        }

        EnsureRockPiece(visual, $"{visualName}_Core", new Vector3(0f, 0.28f, 0f), Quaternion.Euler(-4f, 28f, 7f), new Vector3(1.3f, 0.85f, 1.1f), 11);
        EnsureRockPiece(visual, $"{visualName}_SideA", new Vector3(0.42f, 0.18f, -0.22f), Quaternion.Euler(5f, -20f, -6f), new Vector3(0.85f, 0.55f, 0.75f), 23);
        EnsureRockPiece(visual, $"{visualName}_SideB", new Vector3(-0.36f, 0.14f, 0.28f), Quaternion.Euler(-7f, 44f, 4f), new Vector3(0.7f, 0.48f, 0.82f), 37);
    }

    private void EnsureRockPiece(Transform parent, string pieceName, Vector3 localPosition, Quaternion localRotation, Vector3 localScale, int seed)
    {
        Transform piece = parent.Find(pieceName);
        GameObject pieceObject;

        if (piece == null)
        {
            pieceObject = new GameObject(pieceName);
            pieceObject.transform.SetParent(parent, false);
        }
        else
        {
            pieceObject = piece.gameObject;
        }

        pieceObject.transform.localPosition = localPosition;
        pieceObject.transform.localRotation = localRotation;
        pieceObject.transform.localScale = localScale;

        MeshFilter meshFilter = pieceObject.GetComponent<MeshFilter>();
        if (meshFilter == null)
            meshFilter = pieceObject.AddComponent<MeshFilter>();

        MeshRenderer meshRenderer = pieceObject.GetComponent<MeshRenderer>();
        if (meshRenderer == null)
            meshRenderer = pieceObject.AddComponent<MeshRenderer>();

        Mesh mesh = meshFilter.sharedMesh;
        if (mesh == null || mesh.name != $"IrregularRock_{seed}")
            meshFilter.sharedMesh = CreateIrregularRockMesh(seed);

        meshRenderer.sharedMaterial = CreateRockMaterial();
    }

    private Mesh CreateIrregularRockMesh(int seed)
    {
        const int sides = 10;
        Vector3[] vertices = new Vector3[sides * 3 + 2];
        int bottomCenter = sides * 3;
        int topCenter = bottomCenter + 1;

        System.Random random = new System.Random(seed);

        for (int i = 0; i < sides; i++)
        {
            float angle = i * Mathf.PI * 2f / sides;
            float bottomRadius = RandomRange(random, 0.72f, 1.05f);
            float middleRadius = RandomRange(random, 0.85f, 1.18f);
            float topRadius = RandomRange(random, 0.42f, 0.72f);
            float topOffsetX = RandomRange(random, -0.12f, 0.12f);
            float topOffsetZ = RandomRange(random, -0.12f, 0.12f);

            Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            vertices[i] = new Vector3(direction.x * bottomRadius, 0f, direction.z * bottomRadius);
            vertices[sides + i] = new Vector3(direction.x * middleRadius, RandomRange(random, 0.38f, 0.62f), direction.z * middleRadius);
            vertices[sides * 2 + i] = new Vector3(direction.x * topRadius + topOffsetX, RandomRange(random, 0.82f, 1.08f), direction.z * topRadius + topOffsetZ);
        }

        vertices[bottomCenter] = new Vector3(0f, -0.04f, 0f);
        vertices[topCenter] = new Vector3(0.06f, 1.05f, -0.04f);

        int[] triangles = new int[sides * 18];
        int index = 0;

        for (int i = 0; i < sides; i++)
        {
            int next = (i + 1) % sides;
            index = AddQuad(triangles, index, i, next, sides + next, sides + i);
            index = AddQuad(triangles, index, sides + i, sides + next, sides * 2 + next, sides * 2 + i);

            triangles[index++] = bottomCenter;
            triangles[index++] = next;
            triangles[index++] = i;

            triangles[index++] = topCenter;
            triangles[index++] = sides * 2 + i;
            triangles[index++] = sides * 2 + next;
        }

        Mesh mesh = new Mesh
        {
            name = $"IrregularRock_{seed}",
            vertices = vertices,
            triangles = triangles
        };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static int AddQuad(int[] triangles, int index, int a, int b, int c, int d)
    {
        triangles[index++] = a;
        triangles[index++] = b;
        triangles[index++] = c;
        triangles[index++] = a;
        triangles[index++] = c;
        triangles[index++] = d;
        return index;
    }

    private static float RandomRange(System.Random random, float min, float max)
    {
        return Mathf.Lerp(min, max, (float)random.NextDouble());
    }

    private Material CreateLogMaterial()
    {
        Material material = new Material(FindUsableShader())
        {
            name = "Generated Fallen Log Material"
        };

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", logColor);
        else
            material.color = logColor;

        return material;
    }

    private Material CreateLogEndMaterial()
    {
        Material material = new Material(FindUsableShader())
        {
            name = "Generated Fallen Log Cut Material"
        };

        Color cutColor = new Color(0.58f, 0.38f, 0.18f, 1f);
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", cutColor);
        else
            material.color = cutColor;

        return material;
    }

    private Material CreateBushMaterial()
    {
        Material material = new Material(FindUsableShader())
        {
            name = "Generated Bush Material"
        };

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", bushColor);
        else
            material.color = bushColor;

        return material;
    }

    private Material CreateRockMaterial()
    {
        Material material = new Material(FindUsableShader())
        {
            name = "Generated Rock Material"
        };

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", rockColor);
        else
            material.color = rockColor;

        return material;
    }

    private static Shader FindUsableShader()
    {
        return Shader.Find("Universal Render Pipeline/Lit")
            ?? Shader.Find("Universal Render Pipeline/Simple Lit")
            ?? Shader.Find("HDRP/Lit")
            ?? Shader.Find("Standard")
            ?? Shader.Find("Sprites/Default")
            ?? Shader.Find("UI/Default");
    }

    void OnDrawGizmos()
    {
        Collider zoneCollider = GetComponent<BoxCollider>();
        if (zoneCollider == null)
            zoneCollider = GetComponent<Collider>();
        if (zoneCollider == null) return;

        Gizmos.color = gizmoColor;

        if (zoneCollider is BoxCollider box)
        {
            Vector3 center = transform.TransformPoint(box.center);
            Vector3 size = Vector3.Scale(box.size, transform.lossyScale);
            Gizmos.DrawCube(center, size);
        }
        else if (zoneCollider is SphereCollider sphere)
        {
            Vector3 center = transform.TransformPoint(sphere.center);
            float radius = sphere.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z);
            Gizmos.DrawSphere(center, radius);
        }
        else
        {
            Gizmos.DrawCube(zoneCollider.bounds.center, zoneCollider.bounds.size);
        }
    }
}
