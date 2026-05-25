using UnityEngine;

// Attach to a trigger collider. Squirrel tag detection is handled by SquirrelAgent.
[RequireComponent(typeof(Collider))]
public class SafeZone : MonoBehaviour
{
    void Awake()
    {
        gameObject.tag = "Safezone";
        GetComponent<Collider>().isTrigger = true;
    }
}
