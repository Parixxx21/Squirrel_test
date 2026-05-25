using UnityEngine;

// Attach to the low_poly_squirrel child object
public class SquirrelAnimator : MonoBehaviour
{
    [Header("Bob")]
    public float bobSpeed  = 8f;
    public float bobAmount = 0.04f;

    [Header("Tilt")]
    public float tiltAmount = 12f;
    public float tiltSpeed  = 6f;

    private Rigidbody rb;
    private Vector3 initialLocalPos;
    private float bobTime;
    private float currentTilt;

    void Start()
    {
        rb = GetComponentInParent<Rigidbody>();
        initialLocalPos = transform.localPosition;
    }

    void Update()
    {
        float speed = rb != null
            ? new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z).magnitude
            : 0f;

        bool isMoving = speed > 0.3f;

        // Bob up and down when moving
        if (isMoving)
        {
            bobTime += Time.deltaTime * bobSpeed;
            float bob = Mathf.Sin(bobTime) * bobAmount;
            transform.localPosition = initialLocalPos + Vector3.up * bob;
        }
        else
        {
            bobTime = 0f;
            transform.localPosition = Vector3.Lerp(
                transform.localPosition, initialLocalPos, Time.deltaTime * 5f);
        }

        // Tilt forward when moving, upright when still
        float targetTilt = isMoving ? tiltAmount : 0f;
        currentTilt = Mathf.Lerp(currentTilt, targetTilt, Time.deltaTime * tiltSpeed);
        transform.localRotation = Quaternion.Euler(currentTilt, transform.localRotation.eulerAngles.y, 0f);
    }
}
