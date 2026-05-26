#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class SafeZoneSceneBootstrapper
{
    private const string ForagingSceneName = "ForagingScene";
    private const string SafeZonesRootName = "SafeZones";

    static SafeZoneSceneBootstrapper()
    {
        EditorApplication.delayCall += EnsureForagingSceneLogs;
        EditorSceneManager.sceneOpened += (_, _) => EditorApplication.delayCall += EnsureForagingSceneLogs;
    }

    [MenuItem("Squirrel/Ensure Log Safe Zones")]
    public static void EnsureForagingSceneLogs()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.name != ForagingSceneName) return;

        bool changed = false;
        Transform safeZonesRoot = FindOrCreateSafeZonesRoot(scene, ref changed);

        changed |= EnsureLogSafeZone(safeZonesRoot, "SafeZone_Log_1", "LogVisual_1", TerrainPoint(0.12f, 0.82f), 0f, new Vector3(1.25f, 6.2f, 1.25f));
        changed |= EnsureLogSafeZone(safeZonesRoot, "SafeZone_Log_2", "LogVisual_2", TerrainPoint(0.88f, 0.78f), 90f, new Vector3(1.35f, 6.8f, 1.35f));

        if (changed)
            EditorSceneManager.MarkSceneDirty(scene);
    }

    private static Transform FindOrCreateSafeZonesRoot(Scene scene, ref bool changed)
    {
        foreach (GameObject rootObject in scene.GetRootGameObjects())
        {
            if (rootObject.name == SafeZonesRootName)
                return rootObject.transform;
        }

        GameObject safeZonesRoot = new GameObject(SafeZonesRootName);
        SceneManager.MoveGameObjectToScene(safeZonesRoot, scene);
        changed = true;
        return safeZonesRoot.transform;
    }

    private static bool EnsureLogSafeZone(Transform parent, string safeZoneName, string visualName, Vector3 flatPosition, float yaw, Vector3 visualScale)
    {
        bool changed = false;
        Transform safeZone = parent.Find(safeZoneName);

        if (safeZone == null)
        {
            GameObject safeZoneObject = new GameObject(safeZoneName);
            safeZoneObject.transform.SetParent(parent, false);
            safeZone = safeZoneObject.transform;
            changed = true;
        }

        changed |= SetTag(safeZone.gameObject, "Safezone");
        changed |= SetTransform(safeZone, SampleTerrainPosition(flatPosition), Quaternion.identity, Vector3.one);

        SphereCollider sphere = safeZone.GetComponent<SphereCollider>();
        BoxCollider box = safeZone.GetComponent<BoxCollider>();
        if (sphere == null && box == null)
        {
            sphere = safeZone.gameObject.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.radius = 3.5f;
            changed = true;
        }

        if (safeZone.GetComponent<SafeZone>() == null)
        {
            safeZone.gameObject.AddComponent<SafeZone>();
            changed = true;
        }

        Transform visual = safeZone.Find(visualName);
        if (visual == null)
        {
            visual = CreateLogVisual(visualName, safeZone);
            changed = true;
        }

        changed |= SetTransform(visual, Vector3.zero, Quaternion.Euler(0f, yaw, 90f), visualScale);
        return changed;
    }

    private static bool SetTag(GameObject gameObject, string tag)
    {
        if (gameObject.CompareTag(tag)) return false;

        gameObject.tag = tag;
        return true;
    }

    private static bool SetTransform(Transform transform, Vector3 localOrWorldPosition, Quaternion rotation, Vector3 scale)
    {
        bool changed = false;

        if (transform.parent == null)
        {
            if (transform.position != localOrWorldPosition)
            {
                transform.position = localOrWorldPosition;
                changed = true;
            }
        }
        else if (transform.localPosition != localOrWorldPosition)
        {
            transform.localPosition = localOrWorldPosition;
            changed = true;
        }

        if (transform.localRotation != rotation)
        {
            transform.localRotation = rotation;
            changed = true;
        }

        if (transform.localScale != scale)
        {
            transform.localScale = scale;
            changed = true;
        }

        return changed;
    }

    private static Vector3 SampleTerrainPosition(Vector3 flatPosition)
    {
        Terrain terrain = Object.FindFirstObjectByType<Terrain>();
        if (terrain == null) return flatPosition;

        flatPosition.y = terrain.transform.position.y + terrain.SampleHeight(flatPosition) + 0.08f;
        return flatPosition;
    }

    private static Vector3 TerrainPoint(float normalizedX, float normalizedZ)
    {
        Terrain terrain = Object.FindFirstObjectByType<Terrain>();
        if (terrain == null)
            return new Vector3(normalizedX * 100f, 0f, normalizedZ * 100f);

        TerrainData terrainData = terrain.terrainData;
        Vector3 origin = terrain.transform.position;
        return new Vector3(
            origin.x + terrainData.size.x * normalizedX,
            0f,
            origin.z + terrainData.size.z * normalizedZ);
    }

    private static Transform CreateLogVisual(string visualName, Transform parent)
    {
        GameObject visualObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        visualObject.name = visualName;
        visualObject.transform.SetParent(parent, false);

        Collider collider = visualObject.GetComponent<Collider>();
        if (collider != null)
            Object.DestroyImmediate(collider);

        Renderer renderer = visualObject.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial = CreateLogMaterial();

        return visualObject.transform;
    }

    private static Material CreateLogMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        Material material = new Material(shader)
        {
            name = "Generated Fallen Log Material"
        };

        Color color = new Color(0.34f, 0.2f, 0.1f, 1f);
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        else
            material.color = color;

        return material;
    }
}
#endif
