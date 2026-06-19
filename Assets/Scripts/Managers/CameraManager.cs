using UnityEngine;

public class CameraSwitcher : MonoBehaviour
{
    public Camera mainCam;
    public Camera predator1Cam;
    public Camera predator2Cam;
    public Camera squirrelCam;

    [Header("Follow Camera")]
    public bool useSmoothFollowCameras = true;
    public Vector3 followOffset = new Vector3(0f, 3f, -5f);
    public float lookAtHeight = 1f;
    public float positionSharpness = 8f;
    public float rotationSharpness = 10f;

    void Start()
    {
        if (useSmoothFollowCameras)
        {
            SetupFollowCamera(predator1Cam);
            SetupFollowCamera(predator2Cam);
            SetupFollowCamera(squirrelCam);
        }

        SetActiveCamera(mainCam);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            SetActiveCamera(mainCam);
        }

        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            SetActiveCamera(predator1Cam);
        }

        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            SetActiveCamera(predator2Cam);
        }

        if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            SetActiveCamera(squirrelCam);
        }
    }

    void SetActiveCamera(Camera activeCam)
    {
        if (mainCam) mainCam.enabled = false;
        if (predator1Cam) predator1Cam.enabled = false;
        if (predator2Cam) predator2Cam.enabled = false;
        if (squirrelCam) squirrelCam.enabled = false;

        if (activeCam) activeCam.enabled = true;
    }

    void SetupFollowCamera(Camera camera)
    {
        if (camera == null || camera.transform.parent == null)
            return;

        Transform target = camera.transform.parent;

        // The old setup made the camera a child of the animal, so it inherited
        // slope alignment and sudden rotations. Keep the same target, but make
        // the camera independent and smooth its world-space motion instead.
        camera.transform.SetParent(null, true);

        SmoothFollowCamera follow = camera.GetComponent<SmoothFollowCamera>();
        if (follow == null)
            follow = camera.gameObject.AddComponent<SmoothFollowCamera>();

        follow.target = target;
        follow.offset = followOffset;
        follow.lookAtHeight = lookAtHeight;
        follow.positionSharpness = positionSharpness;
        follow.rotationSharpness = rotationSharpness;
        follow.SnapToTarget();
    }
}

public class SmoothFollowCamera : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0f, 3f, -5f);
    public float lookAtHeight = 1f;
    public float positionSharpness = 8f;
    public float rotationSharpness = 10f;

    void LateUpdate()
    {
        if (target == null)
            return;

        Vector3 targetPosition = GetDesiredPosition();
        transform.position = Vector3.Lerp(transform.position, targetPosition, SmoothFactor(positionSharpness));

        Quaternion targetRotation = GetDesiredRotation();
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, SmoothFactor(rotationSharpness));
    }

    public void SnapToTarget()
    {
        if (target == null)
            return;

        transform.position = GetDesiredPosition();
        transform.rotation = GetDesiredRotation();
    }

    Vector3 GetDesiredPosition()
    {
        Vector3 forward = Vector3.ProjectOnPlane(target.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;

        forward.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

        return target.position
            + right * offset.x
            + Vector3.up * offset.y
            + forward * offset.z;
    }

    Quaternion GetDesiredRotation()
    {
        Vector3 lookPoint = target.position + Vector3.up * lookAtHeight;
        Vector3 direction = lookPoint - transform.position;
        if (direction.sqrMagnitude < 0.0001f)
            return transform.rotation;

        return Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    float SmoothFactor(float sharpness)
    {
        return 1f - Mathf.Exp(-Mathf.Max(0.01f, sharpness) * Time.deltaTime);
    }
}
