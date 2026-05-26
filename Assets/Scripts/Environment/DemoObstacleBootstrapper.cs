using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class DemoObstacleBootstrapper
{
    private const string RootName = "ObstacleField";
    private const int Seed = 275;

    static DemoObstacleBootstrapper()
    {
        EditorApplication.delayCall += EnsureObstacleField;
    }

    [MenuItem("CS275/Obstacles/Rebuild Random Obstacle Field")]
    public static void RebuildObstacleField()
    {
        GameObject existing = GameObject.Find(RootName);
        if (existing != null)
            Object.DestroyImmediate(existing);

        CreateObstacleField();
    }

    private static void EnsureObstacleField()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (SceneManager.GetActiveScene().name != "ForagingScene") return;
        if (GameObject.Find(RootName) != null) return;

        CreateObstacleField();
    }

    private static void CreateObstacleField()
    {
        Terrain terrain = Object.FindAnyObjectByType<Terrain>();
        if (terrain == null) return;

        GameObject parent = new GameObject(RootName);
        Random.InitState(Seed);

        for (int i = 0; i < 9; i++)
            CreateRock(parent.transform, terrain, RandomTerrainPosition(terrain), Random.Range(0.8f, 1.4f), Random.Range(0f, 360f));

        for (int i = 0; i < 5; i++)
            CreateDeepPit(parent.transform, terrain, RandomTerrainPosition(terrain), Random.Range(0.85f, 1.25f), Random.Range(0f, 360f));

        for (int i = 0; i < 6; i++)
            CreateTrash(parent.transform, terrain, RandomTerrainPosition(terrain), Random.Range(0.8f, 1.15f), Random.Range(0f, 360f));

        for (int i = 0; i < 7; i++)
            CreateMudPuddle(parent.transform, terrain, RandomTerrainPosition(terrain), Random.Range(0.85f, 1.3f), Random.Range(0f, 360f));

        Selection.activeGameObject = parent;
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    private static Vector3 RandomTerrainPosition(Terrain terrain)
    {
        Vector3 size = terrain.terrainData.size;
        Vector3 origin = terrain.transform.position;
        float margin = 4f;

        float x = origin.x + Random.Range(margin, size.x - margin);
        float z = origin.z + Random.Range(margin, size.z - margin);
        return new Vector3(x, 0f, z);
    }

    private static void CreateRock(Transform parent, Terrain terrain, Vector3 position, float scale, float yaw)
    {
        GameObject rock = CreateObstacleRoot(parent, "Rock", terrain, position, 0.15f, yaw);

        SphereCollider collider = rock.AddComponent<SphereCollider>();
        collider.radius = 0.85f * scale;
        collider.center = new Vector3(0f, 0.45f * scale, 0f);

        CreatePrimitiveVisual(rock.transform, PrimitiveType.Sphere, Vector3.zero, new Vector3(1.35f, 0.8f, 1.05f) * scale, Gray(0.34f));
        CreatePrimitiveVisual(rock.transform, PrimitiveType.Sphere, new Vector3(0.45f, 0.08f, -0.25f) * scale, new Vector3(0.75f, 0.45f, 0.55f) * scale, Gray(0.27f));
        CreatePrimitiveVisual(rock.transform, PrimitiveType.Sphere, new Vector3(-0.35f, 0.04f, 0.35f) * scale, new Vector3(0.55f, 0.35f, 0.42f) * scale, Gray(0.42f));

        ConfigureObstacle(rock, Obstacle.ObstacleType.Rock);
    }

    private static void CreateDeepPit(Transform parent, Terrain terrain, Vector3 position, float scale, float yaw)
    {
        GameObject pit = CreateObstacleRoot(parent, "DeepPit", terrain, position, 0.012f, yaw);

        CapsuleCollider collider = pit.AddComponent<CapsuleCollider>();
        collider.radius = 1.45f * scale;
        collider.height = 0.2f;

        CreateTerrainPatchVisual(pit.transform, terrain, 1.45f * scale, 1.1f * scale, Color.black, 0.025f);

        ConfigureObstacle(pit, Obstacle.ObstacleType.DeepPit);
    }

    private static void CreateTrash(Transform parent, Terrain terrain, Vector3 position, float scale, float yaw)
    {
        GameObject trash = CreateObstacleRoot(parent, "Trash", terrain, position, 0.15f, yaw);

        BoxCollider collider = trash.AddComponent<BoxCollider>();
        collider.size = new Vector3(1.8f, 0.9f, 1.2f) * scale;
        collider.center = new Vector3(0f, 0.45f * scale, 0f);

        CreatePrimitiveVisual(trash.transform, PrimitiveType.Cube, new Vector3(0f, 0.22f, 0f) * scale, new Vector3(1.0f, 0.45f, 0.75f) * scale, new Color(0.15f, 0.18f, 0.22f));
        CreatePrimitiveVisual(trash.transform, PrimitiveType.Cube, new Vector3(0.45f, 0.52f, -0.28f) * scale, new Vector3(0.55f, 0.22f, 0.38f) * scale, new Color(0.05f, 0.25f, 0.55f));
        CreatePrimitiveVisual(trash.transform, PrimitiveType.Cylinder, new Vector3(-0.45f, 0.35f, 0.2f) * scale, new Vector3(0.35f, 0.45f, 0.35f) * scale, new Color(0.55f, 0.52f, 0.45f));
        CreatePrimitiveVisual(trash.transform, PrimitiveType.Cube, new Vector3(0f, 0.65f, 0.35f) * scale, new Vector3(1.25f, 0.08f, 0.28f) * scale, new Color(0.75f, 0.68f, 0.4f));

        ConfigureObstacle(trash, Obstacle.ObstacleType.Trash);
    }

    private static void CreateMudPuddle(Transform parent, Terrain terrain, Vector3 position, float scale, float yaw)
    {
        GameObject mud = CreateObstacleRoot(parent, "MudPuddle", terrain, position, 0.018f, yaw);

        CapsuleCollider collider = mud.AddComponent<CapsuleCollider>();
        collider.radius = 1.55f * scale;
        collider.height = 0.2f;

        CreateTerrainPatchVisual(mud.transform, terrain, 1.5f * scale, 0.78f * scale, new Color(0.22f, 0.1f, 0.035f), 0.028f);
        CreateTerrainPatchVisual(mud.transform, terrain, 1.1f * scale, 0.52f * scale, new Color(0.38f, 0.2f, 0.08f), 0.035f);
        CreateTerrainPatchVisual(mud.transform, terrain, 0.35f * scale, 0.16f * scale, new Color(0.48f, 0.28f, 0.12f), 0.042f);

        ConfigureObstacle(mud, Obstacle.ObstacleType.MudPuddle);
    }

    private static GameObject CreateObstacleRoot(Transform parent, string typeName, Terrain terrain, Vector3 position, float yOffset, float yaw)
    {
        GameObject root = new GameObject(typeName + "_Obstacle");
        root.transform.SetParent(parent);
        root.transform.position = TerrainPosition(terrain, position, yOffset);
        root.transform.rotation = TerrainAlignedRotation(terrain, position, yaw);
        return root;
    }

    private static void ConfigureObstacle(GameObject root, Obstacle.ObstacleType type)
    {
        Obstacle obstacle = root.AddComponent<Obstacle>();
        obstacle.type = type;
        obstacle.ApplyConfiguration();
    }

    private static void CreatePrimitiveVisual(Transform parent, PrimitiveType primitiveType, Vector3 localPosition, Vector3 localScale, Color color)
    {
        GameObject visual = GameObject.CreatePrimitive(primitiveType);
        visual.name = "Visual";
        visual.transform.SetParent(parent);
        visual.transform.localPosition = localPosition;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = localScale;
        visual.GetComponent<Renderer>().sharedMaterial = MakeMaterial(color);

        foreach (Collider childCollider in visual.GetComponentsInChildren<Collider>())
            childCollider.enabled = false;
    }

    private static void CreateTerrainPatchVisual(Transform parent, Terrain terrain, float radiusX, float radiusZ, Color color, float surfaceOffset)
    {
        const int segments = 32;
        const int rings = 5;

        GameObject visual = new GameObject("Visual_TerrainPatch");
        visual.transform.SetParent(parent);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one;

        Vector3[] vertices = new Vector3[1 + rings * segments];
        int[] triangles = new int[segments * 3 + (rings - 1) * segments * 6];

        vertices[0] = TerrainLocalPoint(parent, terrain, Vector3.zero, surfaceOffset);

        for (int ring = 1; ring <= rings; ring++)
        {
            float t = ring / (float)rings;
            for (int i = 0; i < segments; i++)
            {
                float angle = Mathf.PI * 2f * i / segments;
                Vector3 localFlat = new Vector3(Mathf.Cos(angle) * radiusX * t, 0f, Mathf.Sin(angle) * radiusZ * t);
                vertices[VertexIndex(ring, i, segments)] = TerrainLocalPoint(parent, terrain, localFlat, surfaceOffset);
            }
        }

        int tri = 0;
        for (int i = 0; i < segments; i++)
        {
            triangles[tri++] = 0;
            triangles[tri++] = VertexIndex(1, (i + 1) % segments, segments);
            triangles[tri++] = VertexIndex(1, i, segments);
        }

        for (int ring = 1; ring < rings; ring++)
        {
            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                int innerA = VertexIndex(ring, i, segments);
                int innerB = VertexIndex(ring, next, segments);
                int outerA = VertexIndex(ring + 1, i, segments);
                int outerB = VertexIndex(ring + 1, next, segments);

                triangles[tri++] = innerA;
                triangles[tri++] = innerB;
                triangles[tri++] = outerB;

                triangles[tri++] = innerA;
                triangles[tri++] = outerB;
                triangles[tri++] = outerA;
            }
        }

        Mesh mesh = new Mesh
        {
            name = "DeepPitTerrainPatch",
            vertices = vertices,
            triangles = triangles
        };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        MeshFilter meshFilter = visual.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = mesh;

        MeshRenderer meshRenderer = visual.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = MakeMaterial(color);
    }

    private static int VertexIndex(int ring, int segment, int segments)
    {
        return 1 + (ring - 1) * segments + segment;
    }

    private static Vector3 TerrainLocalPoint(Transform parent, Terrain terrain, Vector3 localFlat, float surfaceOffset)
    {
        Vector3 worldFlat = parent.TransformPoint(localFlat);
        float y = terrain.SampleHeight(worldFlat) + terrain.transform.position.y + surfaceOffset;
        Vector3 worldOnTerrain = new Vector3(worldFlat.x, y, worldFlat.z);
        return parent.InverseTransformPoint(worldOnTerrain);
    }

    private static Vector3 TerrainPosition(Terrain terrain, Vector3 position, float yOffset)
    {
        float y = terrain.SampleHeight(position) + terrain.transform.position.y + yOffset;
        return new Vector3(position.x, y, position.z);
    }

    private static Quaternion TerrainAlignedRotation(Terrain terrain, Vector3 position, float yaw)
    {
        Vector3 terrainPosition = terrain.transform.position;
        Vector3 terrainSize = terrain.terrainData.size;
        float normalizedX = Mathf.InverseLerp(terrainPosition.x, terrainPosition.x + terrainSize.x, position.x);
        float normalizedZ = Mathf.InverseLerp(terrainPosition.z, terrainPosition.z + terrainSize.z, position.z);
        Vector3 normal = terrain.terrainData.GetInterpolatedNormal(normalizedX, normalizedZ);

        return Quaternion.FromToRotation(Vector3.up, normal) * Quaternion.Euler(0f, yaw, 0f);
    }

    private static Color Gray(float value)
    {
        return new Color(value, value, value);
    }

    private static Material MakeMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("HDRP/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        Material material = new Material(shader);
        material.color = color;
        return material;
    }
}
#endif
