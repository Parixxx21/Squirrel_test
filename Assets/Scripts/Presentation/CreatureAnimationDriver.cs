using UnityEngine;

// Drives an imported rigged visual model from the existing ML-Agent root velocity.
// This script is visual-only: it does not move the root, collider, Rigidbody, or rewards.
public class CreatureAnimationDriver : MonoBehaviour
{
    [Header("Animator")]
    public Animator animator;
    public string speedParameter = "Speed";
    public string movingParameter = "Moving";
    public string runningParameter = "Running";
    public bool disableRootMotion = true;
    public bool disableImportedPhysics = true;

    [Header("Speed Mapping")]
    public float walkSpeed = 0.4f;
    public float runSpeed = 3.5f;
    public float speedSmoothing = 8f;
    public float idlePlaybackSpeed = 0.85f;
    public float movingPlaybackSpeed = 1.0f;

    private Rigidbody rootRigidbody;
    private float smoothedSpeed01;
    private int speedHash;
    private int movingHash;
    private int runningHash;

    private void Reset()
    {
        animator = GetComponentInChildren<Animator>(true);
    }

    private void Awake()
    {
        Cache();
        ConfigureVisualOnlyModel();
    }

    private void OnValidate()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);
    }

    private void Cache()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        rootRigidbody = GetComponentInParent<Rigidbody>();
        speedHash = Animator.StringToHash(speedParameter);
        movingHash = Animator.StringToHash(movingParameter);
        runningHash = Animator.StringToHash(runningParameter);
    }

    private void ConfigureVisualOnlyModel()
    {
        if (animator != null)
            animator.applyRootMotion = !disableRootMotion ? animator.applyRootMotion : false;

        if (!disableImportedPhysics)
            return;

        foreach (Rigidbody childBody in GetComponentsInChildren<Rigidbody>(true))
        {
            if (childBody != rootRigidbody)
            {
                childBody.isKinematic = true;
                childBody.detectCollisions = false;
            }
        }

        foreach (Collider childCollider in GetComponentsInChildren<Collider>(true))
        {
            if (rootRigidbody == null || !childCollider.transform.IsChildOf(rootRigidbody.transform) || childCollider.transform == rootRigidbody.transform)
                continue;

            childCollider.enabled = false;
        }
    }

    private void Update()
    {
        if (animator == null)
            return;

        if (rootRigidbody == null)
            rootRigidbody = GetComponentInParent<Rigidbody>();

        Vector3 velocity = rootRigidbody != null
            ? new Vector3(rootRigidbody.linearVelocity.x, 0f, rootRigidbody.linearVelocity.z)
            : Vector3.zero;

        float speed = velocity.magnitude;
        float targetSpeed01 = Mathf.InverseLerp(walkSpeed, runSpeed, speed);
        smoothedSpeed01 = Mathf.Lerp(smoothedSpeed01, targetSpeed01, 1f - Mathf.Exp(-speedSmoothing * Time.deltaTime));

        bool moving = speed >= walkSpeed;
        bool running = speed >= Mathf.Lerp(walkSpeed, runSpeed, 0.65f);

        SetFloatIfPresent(speedHash, smoothedSpeed01);
        SetBoolIfPresent(movingHash, moving);
        SetBoolIfPresent(runningHash, running);

        animator.speed = Mathf.Lerp(idlePlaybackSpeed, movingPlaybackSpeed, smoothedSpeed01);
    }

    private void SetFloatIfPresent(int parameterHash, float value)
    {
        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.nameHash == parameterHash && parameter.type == AnimatorControllerParameterType.Float)
            {
                animator.SetFloat(parameterHash, value);
                return;
            }
        }
    }

    private void SetBoolIfPresent(int parameterHash, bool value)
    {
        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.nameHash == parameterHash && parameter.type == AnimatorControllerParameterType.Bool)
            {
                animator.SetBool(parameterHash, value);
                return;
            }
        }
    }
}
