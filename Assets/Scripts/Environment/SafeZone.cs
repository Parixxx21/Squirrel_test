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
        return gameObject.name switch
        {
            "SafeZone_Bush_West" => "BushVisual_West",
            "SafeZone_Bush_East" => "BushVisual_East",
            "SafeZone_TreeHollow_North" => "TreeHollowVisual_North",
            "SafeZone_TreeHollow_South" => "TreeHollowVisual_South",
            _ => null
        };
    }

    private void StyleVisual(Transform visual)
    {
        if (!applyNaturalColors || visual == null) return;

        Color tint = visual.name.Contains("Bush") ? bushColor : leafColor;
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
