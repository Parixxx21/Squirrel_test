using UnityEngine;

// Presentation-only replacement for the old sphere acorn mesh.
// It keeps the Acorn collider and collection logic unchanged.
public class AcornVisual : MonoBehaviour
{
    private const string VisualRootName = "AcornModelVisual";

    [Header("Model")]
    public string modelResourcePath = "Models/Acorn/poly_google_acorn";
    public float targetHeight = 0.28f;
    public float groundOffset = -0.05f;
    public float randomYawRange = 180f;

    [Header("Idle Motion")]
    public bool enableIdleMotion = true;
    public float bobAmount = 0.025f;
    public float bobSpeed = 2f;
    public float spinSpeed = 12f;

    private Transform visualRoot;
    private Vector3 baseLocalPosition;
    private float phaseOffset;

    private void Awake()
    {
        SetupVisual();
    }

    private void OnEnable()
    {
        if (visualRoot == null)
            SetupVisual();

        phaseOffset = Random.value * Mathf.PI * 2f;
    }

    private void Update()
    {
        if (!enableIdleMotion || visualRoot == null)
            return;

        float bob = Mathf.Sin(Time.time * bobSpeed + phaseOffset) * bobAmount;
        visualRoot.localPosition = baseLocalPosition + Vector3.up * bob;
        visualRoot.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.Self);
    }

    private void SetupVisual()
    {
        Transform existing = transform.Find(VisualRootName);
        if (existing != null)
        {
            HideRootSphereRenderer();
            visualRoot = existing;
            baseLocalPosition = visualRoot.localPosition;
            return;
        }

        GameObject source = Resources.Load<GameObject>(modelResourcePath);
        if (source == null)
            return;

        HideRootSphereRenderer();

        GameObject rootObject = new GameObject(VisualRootName);
        visualRoot = rootObject.transform;
        visualRoot.SetParent(transform, false);
        visualRoot.localPosition = Vector3.zero;
        visualRoot.localRotation = Quaternion.Euler(0f, Random.Range(-randomYawRange, randomYawRange), 0f);
        visualRoot.localScale = Vector3.one;

        GameObject model = Instantiate(source, visualRoot);
        model.name = "PolyGoogleAcorn";
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        model.transform.localScale = Vector3.one;

        RemoveImportedColliders(model);
        FitModelToAcorn(model.transform);
        baseLocalPosition = visualRoot.localPosition;
    }

    private void HideRootSphereRenderer()
    {
        MeshRenderer rootRenderer = GetComponent<MeshRenderer>();
        if (rootRenderer != null)
            rootRenderer.enabled = false;
    }

    private void RemoveImportedColliders(GameObject model)
    {
        foreach (Collider importedCollider in model.GetComponentsInChildren<Collider>(true))
        {
            if (Application.isPlaying)
                Destroy(importedCollider);
            else
                DestroyImmediate(importedCollider);
        }
    }

    private void FitModelToAcorn(Transform model)
    {
        Bounds bounds = CalculateBounds(model.gameObject);
        if (bounds.size.y <= 0.0001f)
            return;

        float scale = targetHeight / bounds.size.y;
        model.localScale *= scale;

        bounds = CalculateBounds(model.gameObject);
        Vector3 centerLocal = visualRoot.InverseTransformPoint(bounds.center);
        Vector3 bottomLocal = visualRoot.InverseTransformPoint(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z));

        model.localPosition += new Vector3(
            -centerLocal.x,
            groundOffset - bottomLocal.y,
            -centerLocal.z);
    }

    private Bounds CalculateBounds(GameObject target)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return new Bounds(target.transform.position, Vector3.zero);

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        return bounds;
    }
}
