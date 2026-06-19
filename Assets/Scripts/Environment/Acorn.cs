using UnityEngine;

[RequireComponent(typeof(Collider))]
public class Acorn : MonoBehaviour
{
    [HideInInspector] public AcornSpawner spawner;

    void Awake()
    {
        gameObject.tag = "Acorn";
        GetComponent<Collider>().isTrigger = true;

        if (GetComponent<AcornVisual>() == null)
            gameObject.AddComponent<AcornVisual>();
    }

    public void OnCollected()
    {
        gameObject.SetActive(false);
        spawner?.ScheduleRespawn(this);
    }
}
