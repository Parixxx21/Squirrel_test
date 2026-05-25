using UnityEngine;

// Temporary test script - attach to Squirrel to verify movement works
public class SquirrelTest : MonoBehaviour
{
    public float moveSpeed = 3f;
    public float turnSpeed = 150f;
    private Rigidbody rb;

    void Start() => rb = GetComponent<Rigidbody>();

    void Update()
    {
        float forward = 0f;
        float turn = 0f;

        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))    forward =  1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))  forward = -1f;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))  turn   = -1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) turn   =  1f;

        transform.Rotate(0f, turn * turnSpeed * Time.deltaTime, 0f);
        Vector3 move = transform.forward * forward * moveSpeed;
        rb.linearVelocity = new Vector3(move.x, rb.linearVelocity.y, move.z);

        Debug.Log($"forward={forward} turn={turn} pos={transform.position}");
    }
}
