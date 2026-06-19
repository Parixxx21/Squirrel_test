using UnityEngine;

public static class ObstaclePresentationEnhancer
{
    private const string VisualRootName = "PresentationObstacleVisual";
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnhanceAllOnSceneLoad()
    {
        EnhanceAll();
    }

    public static void EnhanceAll()
    {
        foreach (Obstacle obstacle in Object.FindObjectsByType<Obstacle>(FindObjectsInactive.Include))
            Enhance(obstacle);
    }

    private static void Enhance(Obstacle obstacle)
    {
        if (obstacle == null)
            return;

        SnapRockAndTrashToTerrain(obstacle);

        Transform existing = obstacle.transform.Find(VisualRootName);
        if (existing != null)
            DestroyObject(existing.gameObject);

        GameObject visualRoot = new GameObject(VisualRootName);
        visualRoot.transform.SetParent(obstacle.transform, false);
        visualRoot.transform.localPosition = Vector3.zero;
        visualRoot.transform.localRotation = Quaternion.identity;
        visualRoot.transform.localScale = Vector3.one;

        HideOriginalObstacleRenderers(obstacle.transform, visualRoot.transform);

        float scale = GetObstacleScale(obstacle);
        switch (obstacle.type)
        {
            case Obstacle.ObstacleType.DeepPit:
                BuildDeepPit(visualRoot.transform, scale);
                break;
            case Obstacle.ObstacleType.MudPuddle:
                BuildMudPuddle(visualRoot.transform, scale);
                break;
            case Obstacle.ObstacleType.Trash:
                BuildTrash(visualRoot.transform, scale);
                break;
            default:
                BuildRock(visualRoot.transform, scale);
                break;
        }
    }

    private static void BuildDeepPit(Transform parent, float scale)
    {
        Material black = MakeMaterial(new Color(0.005f, 0.004f, 0.003f));
        Material darkEdge = MakeMaterial(new Color(0.045f, 0.035f, 0.03f));
        Material rim = MakeMaterial(new Color(0.18f, 0.16f, 0.14f));

        AddPart(parent, "BlackPitOpening", PrimitiveType.Cylinder, new Vector3(0f, 0.012f, 0f), Quaternion.identity, new Vector3(scale * 2.5f, 0.02f, scale * 1.8f), black);
        AddPart(parent, "InnerShadow", PrimitiveType.Cylinder, new Vector3(0f, 0.004f, 0f), Quaternion.identity, new Vector3(scale * 1.55f, 0.016f, scale * 1.1f), darkEdge);

        int rimCount = 14;
        for (int i = 0; i < rimCount; i++)
        {
            float angle = Mathf.PI * 2f * i / rimCount;
            Vector3 local = new Vector3(Mathf.Cos(angle) * scale * 1.36f, 0.055f, Mathf.Sin(angle) * scale * 0.98f);
            Vector3 localScale = new Vector3(0.24f, 0.1f, 0.18f) * scale * RandomScale(i);
            AddPart(parent, "PitRimRock", PrimitiveType.Sphere, local, Quaternion.Euler(0f, -angle * Mathf.Rad2Deg, 0f), localScale, rim);
        }
    }

    private static void BuildMudPuddle(Transform parent, float scale)
    {
        Material darkMud = MakeMaterial(new Color(0.11f, 0.065f, 0.025f));
        Material wetMud = MakeMaterial(new Color(0.32f, 0.18f, 0.075f));
        Material highlight = MakeMaterial(new Color(0.47f, 0.31f, 0.16f));

        AddPart(parent, "MudOuter", PrimitiveType.Cylinder, new Vector3(0f, 0.018f, 0f), Quaternion.identity, new Vector3(scale * 2.65f, 0.018f, scale * 1.55f), darkMud);
        AddPart(parent, "MudWetCenter", PrimitiveType.Cylinder, new Vector3(0.12f * scale, 0.028f, -0.03f * scale), Quaternion.Euler(0f, 18f, 0f), new Vector3(scale * 1.55f, 0.012f, scale * 0.72f), wetMud);
        AddPart(parent, "MudHighlight", PrimitiveType.Cylinder, new Vector3(-0.38f * scale, 0.036f, 0.2f * scale), Quaternion.Euler(0f, -12f, 0f), new Vector3(scale * 0.48f, 0.009f, scale * 0.16f), highlight);
    }

    private static void BuildTrash(Transform parent, float scale)
    {
        Material bag = MakeMaterial(new Color(0.055f, 0.06f, 0.065f));
        Material blue = MakeMaterial(new Color(0.06f, 0.2f, 0.58f));
        Material can = MakeMaterial(new Color(0.52f, 0.52f, 0.46f));
        Material paper = MakeMaterial(new Color(0.72f, 0.64f, 0.42f));

        AddPart(parent, "TrashBag", PrimitiveType.Sphere, new Vector3(-0.12f * scale, 0.22f * scale, 0f), Quaternion.identity, new Vector3(0.8f, 0.42f, 0.62f) * scale, bag);
        AddPart(parent, "BlueBox", PrimitiveType.Cube, new Vector3(0.42f * scale, 0.2f * scale, -0.24f * scale), Quaternion.Euler(0f, 18f, 8f), new Vector3(0.48f, 0.28f, 0.36f) * scale, blue);
        AddPart(parent, "Can", PrimitiveType.Cylinder, new Vector3(-0.54f * scale, 0.18f * scale, 0.24f * scale), Quaternion.Euler(72f, 0f, 24f), new Vector3(0.22f, 0.34f, 0.22f) * scale, can);
        AddPart(parent, "Plank", PrimitiveType.Cube, new Vector3(0.05f * scale, 0.42f * scale, 0.32f * scale), Quaternion.Euler(6f, -18f, -8f), new Vector3(1.15f, 0.08f, 0.26f) * scale, paper);
    }

    private static void BuildRock(Transform parent, float scale)
    {
        Material rockA = MakeMaterial(new Color(0.31f, 0.31f, 0.3f));
        Material rockB = MakeMaterial(new Color(0.2f, 0.2f, 0.19f));
        Material rockC = MakeMaterial(new Color(0.42f, 0.41f, 0.38f));

        AddPart(parent, "RockMain", PrimitiveType.Sphere, new Vector3(0f, 0.35f * scale, 0f), Quaternion.Euler(0f, 18f, 0f), new Vector3(1.25f, 0.68f, 0.9f) * scale, rockA);
        AddPart(parent, "RockSideA", PrimitiveType.Sphere, new Vector3(0.43f * scale, 0.27f * scale, -0.24f * scale), Quaternion.Euler(0f, -24f, 0f), new Vector3(0.62f, 0.42f, 0.52f) * scale, rockB);
        AddPart(parent, "RockSideB", PrimitiveType.Sphere, new Vector3(-0.38f * scale, 0.18f * scale, 0.3f * scale), Quaternion.Euler(0f, 36f, 0f), new Vector3(0.48f, 0.32f, 0.42f) * scale, rockC);
    }

    private static Transform AddPart(Transform parent, string partName, PrimitiveType primitiveType, Vector3 localPosition, Quaternion localRotation, Vector3 localScale, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(primitiveType);
        part.name = partName;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localRotation = localRotation;
        part.transform.localScale = localScale;

        foreach (Collider collider in part.GetComponentsInChildren<Collider>())
            DestroyObject(collider);

        Renderer renderer = part.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial = material;

        return part.transform;
    }

    private static void HideOriginalObstacleRenderers(Transform obstacleRoot, Transform presentationRoot)
    {
        foreach (Renderer renderer in obstacleRoot.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null || renderer.transform.IsChildOf(presentationRoot))
                continue;

            renderer.enabled = false;
        }
    }

    private static float GetObstacleScale(Obstacle obstacle)
    {
        Collider collider = obstacle.GetComponent<Collider>();
        if (collider is SphereCollider sphere)
            return Mathf.Max(0.55f, sphere.radius);
        if (collider is CapsuleCollider capsule)
            return Mathf.Max(0.55f, capsule.radius);
        if (collider is BoxCollider box)
            return Mathf.Max(0.55f, Mathf.Max(box.size.x, box.size.z) * 0.5f);

        return Mathf.Max(0.55f, Mathf.Max(obstacle.transform.localScale.x, obstacle.transform.localScale.z));
    }

    private static void SnapRockAndTrashToTerrain(Obstacle obstacle)
    {
        if (obstacle.type != Obstacle.ObstacleType.Rock && obstacle.type != Obstacle.ObstacleType.Trash)
            return;

        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null)
            terrain = Object.FindAnyObjectByType<Terrain>();
        if (terrain == null || terrain.terrainData == null)
            return;

        Vector3 terrainPos = terrain.transform.position;
        Vector3 terrainSize = terrain.terrainData.size;
        Vector3 pos = obstacle.transform.position;

        bool insideTerrain =
            pos.x >= terrainPos.x && pos.x <= terrainPos.x + terrainSize.x &&
            pos.z >= terrainPos.z && pos.z <= terrainPos.z + terrainSize.z;
        if (!insideTerrain)
            return;

        pos.y = terrainPos.y + terrain.SampleHeight(pos);
        obstacle.transform.position = pos;
    }

    private static float RandomScale(int index)
    {
        return 0.8f + Mathf.Abs(Mathf.Sin(index * 17.23f)) * 0.45f;
    }

    private static Material MakeMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("HDRP/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        Material material = new Material(shader);
        material.hideFlags = HideFlags.DontSave;
        if (material.HasProperty(BaseColorId))
            material.SetColor(BaseColorId, color);
        if (material.HasProperty(ColorId))
            material.SetColor(ColorId, color);
        material.color = color;
        return material;
    }

    private static void DestroyObject(Object target)
    {
        if (target == null)
            return;

        if (Application.isPlaying)
            Object.Destroy(target);
        else
            Object.DestroyImmediate(target);
    }
}
