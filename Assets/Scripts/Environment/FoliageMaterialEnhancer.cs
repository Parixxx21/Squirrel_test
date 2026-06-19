using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

// Presentation-only material fallback for imported foliage that appears gray
// because its original material/shader did not survive the Unity version change.
#if UNITY_EDITOR
[InitializeOnLoad]
#endif
public static class FoliageMaterialEnhancer
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int PrimaryColorId = Shader.PropertyToID("_Primary_Color");
    private static readonly int SecondaryColorId = Shader.PropertyToID("_Secondary_Color");
    private static readonly int TertiaryColorId = Shader.PropertyToID("_Tertiary_Color");
    private static readonly int ColorMultiplyId = Shader.PropertyToID("_ColorMultiply");
    private static readonly int ColorOverrideId = Shader.PropertyToID("_ColorOverride");
    private static readonly int HighlightColorId = Shader.PropertyToID("_HColor");
    private static readonly int ShadowColorId = Shader.PropertyToID("_SColor");
    private static readonly int SpecColorId = Shader.PropertyToID("_SpecColor");
    private static readonly int TintId = Shader.PropertyToID("_Tint");

    private static readonly Color LeafGreen = new Color(0.16f, 0.50f, 0.12f, 1f);
    private static readonly Color BushGreen = new Color(0.20f, 0.52f, 0.16f, 1f);
    private static readonly Color BarkBrown = new Color(0.34f, 0.20f, 0.10f, 1f);
    private static readonly Color BranchBrown = new Color(0.40f, 0.24f, 0.12f, 1f);
    private static readonly Color LeafAccent = new Color(0.30f, 0.64f, 0.18f, 1f);
    private static readonly Color BarkAccent = new Color(0.58f, 0.36f, 0.16f, 1f);

    private static Material leafMaterial;
    private static Material bushMaterial;
    private static Material barkMaterial;
    private static Material branchMaterial;

#if UNITY_EDITOR
    static FoliageMaterialEnhancer()
    {
        EditorApplication.delayCall += EnhanceAll;
        EditorSceneManager.sceneOpened += (_, _) => EditorApplication.delayCall += EnhanceAll;
        EditorApplication.hierarchyChanged += EnhanceAll;
    }
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnhanceAllOnSceneLoad()
    {
        EnhanceAll();
        EnsureRuntimeRefresher();
    }

    public static void EnhanceAll()
    {
        ConfigureTerrainTreeRendering();

        foreach (Renderer renderer in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include))
            Enhance(renderer);
    }

    public static void Enhance(GameObject root)
    {
        if (root == null) return;

        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            Enhance(renderer);
    }

    private static void Enhance(Renderer renderer)
    {
        if (renderer == null || !IsFoliageCandidate(renderer))
            return;

        Material[] materials = renderer.sharedMaterials;
        bool changed = false;

        for (int i = 0; i < materials.Length; i++)
        {
            if (!TryGetFoliageTint(renderer, materials[i], out Color main, out Color accent))
                continue;

            Material replacement = GetReplacementMaterial(main);
            if (replacement != null && materials[i] != replacement)
            {
                materials[i] = replacement;
                changed = true;
            }

            MaterialPropertyBlock block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block, i);
            ApplyColors(block, main, accent);
            renderer.SetPropertyBlock(block, i);

#if UNITY_EDITOR
            if (materials[i] != null)
            {
                ApplyMaterialColors(materials[i], main, accent);
                EditorUtility.SetDirty(materials[i]);
            }
#endif
        }

        if (changed)
            renderer.sharedMaterials = materials;
    }

    private static bool IsFoliageCandidate(Renderer renderer)
    {
        string name = GetDescriptor(renderer, null);
        return ContainsAny(
            name,
            "trunk", "bark", "stump", "root", "log", "branch", "wood",
            "bush", "shrub", "tree", "forest", "pine", "fir",
            "spruce", "conifer", "cedar", "needle", "leaf", "leaves",
            "foliage", "grass", "flower", "plant", "holotna", "supercyan");
    }

    private static bool TryGetFoliageTint(Renderer renderer, Material material, out Color main, out Color accent)
    {
        string materialName = material != null ? material.name.ToLowerInvariant() : "";
        string rendererName = renderer.name.ToLowerInvariant();
        string hierarchyName = GetDescriptor(renderer, null);
        string fullName = GetDescriptor(renderer, material);

        if (ContainsAny(materialName, "trunk", "bark", "stump", "root") ||
            ContainsAny(rendererName, "trunk", "bark", "stump", "root") ||
            ContainsAny(hierarchyName, "trunk", "bark", "stump", "root"))
        {
            main = BarkBrown;
            accent = BarkAccent;
            return true;
        }

        if (ContainsAny(materialName, "log", "branch", "wood") ||
            ContainsAny(rendererName, "log", "branch", "wood") ||
            ContainsAny(hierarchyName, "log", "branch", "wood"))
        {
            main = BranchBrown;
            accent = BarkAccent;
            return true;
        }

        if (ContainsAny(fullName, "bush", "shrub"))
        {
            main = BushGreen;
            accent = LeafAccent;
            return true;
        }

        if (ContainsAny(
                fullName,
                "tree", "forest", "pine", "fir", "spruce", "conifer", "cedar", "needle",
                "leaf", "leaves", "foliage", "holotna", "supercyan"))
        {
            main = LeafGreen;
            accent = LeafAccent;
            return true;
        }

        main = Color.white;
        accent = Color.white;
        return false;
    }

    private static void ApplyColors(MaterialPropertyBlock block, Color main, Color accent)
    {
        Color shadow = new Color(main.r * 0.45f, main.g * 0.45f, main.b * 0.45f, 1f);

        block.SetColor(BaseColorId, main);
        block.SetColor(ColorId, main);
        block.SetColor(PrimaryColorId, main);
        block.SetColor(SecondaryColorId, accent);
        block.SetColor(TertiaryColorId, Color.black);
        block.SetColor(ColorMultiplyId, main);
        block.SetColor(ColorOverrideId, main);
        block.SetColor(HighlightColorId, accent);
        block.SetColor(ShadowColorId, shadow);
        block.SetColor(SpecColorId, Color.black);
        block.SetColor(TintId, main);
    }

    private static Material GetReplacementMaterial(Color main)
    {
        if (main == BarkBrown)
            return barkMaterial ??= CreateReplacementMaterial("Generated Bark Foliage Material", BarkBrown, BarkAccent);

        if (main == BranchBrown)
            return branchMaterial ??= CreateReplacementMaterial("Generated Branch Foliage Material", BranchBrown, BarkAccent);

        if (main == BushGreen)
            return bushMaterial ??= CreateReplacementMaterial("Generated Bush Foliage Material", BushGreen, LeafAccent);

        return leafMaterial ??= CreateReplacementMaterial("Generated Leaf Foliage Material", LeafGreen, LeafAccent);
    }

    private static Material CreateReplacementMaterial(string name, Color main, Color accent)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit")
            ?? Shader.Find("HDRP/Lit")
            ?? Shader.Find("Standard")
            ?? Shader.Find("Sprites/Default");

        Material material = new Material(shader)
        {
            name = name,
            hideFlags = HideFlags.DontSaveInBuild,
            enableInstancing = true
        };

        ApplyMaterialColors(material, main, accent);
        return material;
    }

    private static void ApplyMaterialColors(Material material, Color main, Color accent)
    {
        Color shadow = new Color(main.r * 0.45f, main.g * 0.45f, main.b * 0.45f, 1f);

        SetColorIfPresent(material, BaseColorId, main);
        SetColorIfPresent(material, ColorId, main);
        SetColorIfPresent(material, PrimaryColorId, main);
        SetColorIfPresent(material, SecondaryColorId, accent);
        SetColorIfPresent(material, TertiaryColorId, Color.black);
        SetColorIfPresent(material, ColorMultiplyId, main);
        SetColorIfPresent(material, ColorOverrideId, main);
        SetColorIfPresent(material, HighlightColorId, accent);
        SetColorIfPresent(material, ShadowColorId, shadow);
        SetColorIfPresent(material, SpecColorId, Color.black);
        SetColorIfPresent(material, TintId, main);
    }

    private static void ConfigureTerrainTreeRendering()
    {
        foreach (Terrain terrain in Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include))
        {
            if (terrain == null)
                continue;

            bool changed = false;
            float treeDistance = Mathf.Max(terrain.treeDistance, 1000f);
            int fullLodCount = Mathf.Max(terrain.treeMaximumFullLODCount, 10000);

            if (!Mathf.Approximately(terrain.treeBillboardDistance, treeDistance))
            {
                terrain.treeBillboardDistance = treeDistance;
                changed = true;
            }

            if (terrain.treeMaximumFullLODCount != fullLodCount)
            {
                terrain.treeMaximumFullLODCount = fullLodCount;
                changed = true;
            }

#if UNITY_EDITOR
            if (changed && !Application.isPlaying)
                EditorUtility.SetDirty(terrain);
#endif
        }
    }

    private static void SetColorIfPresent(Material material, int propertyId, Color color)
    {
        if (material.HasProperty(propertyId))
            material.SetColor(propertyId, color);
    }

    private static string GetDescriptor(Renderer renderer, Material material)
    {
        string name = renderer.name;

        if (material != null)
            name += "/" + material.name;

        Transform current = renderer.transform;

        while (current != null)
        {
            name += "/" + current.name;
            current = current.parent;
        }

        return name.ToLowerInvariant();
    }

    private static bool ContainsAny(string text, params string[] tokens)
    {
        foreach (string token in tokens)
        {
            if (text.Contains(token))
                return true;
        }

        return false;
    }

    private static void EnsureRuntimeRefresher()
    {
        if (Object.FindFirstObjectByType<FoliageMaterialRuntimeRefresher>() != null)
            return;

        GameObject refresher = new GameObject("PresentationFoliageMaterialRefresher")
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        Object.DontDestroyOnLoad(refresher);
        refresher.AddComponent<FoliageMaterialRuntimeRefresher>();
    }
}

public sealed class FoliageMaterialRuntimeRefresher : MonoBehaviour
{
    private float elapsed;
    private float nextRefresh;

    private void Update()
    {
        elapsed += Time.unscaledDeltaTime;
        if (elapsed > 8f)
        {
            Destroy(gameObject);
            return;
        }

        if (Time.unscaledTime < nextRefresh)
            return;

        nextRefresh = Time.unscaledTime + 0.5f;
        FoliageMaterialEnhancer.EnhanceAll();
    }
}
