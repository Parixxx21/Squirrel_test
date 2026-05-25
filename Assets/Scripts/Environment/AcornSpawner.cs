using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AcornSpawner : MonoBehaviour
{
    [Header("Setup")]
    public GameObject acornPrefab;
    public Terrain terrain;

    [Header("Parameters")]
    public int   maxAcorns     = 15;
    public float spawnRadius   = 25f;
    public float respawnDelay  = 5f;
    public float heightOffset  = 0.3f;

    private readonly List<Acorn> pool = new();

    void Start() => SpawnAll();

    void SpawnAll()
    {
        for (int i = 0; i < maxAcorns; i++)
        {
            GameObject obj = Instantiate(acornPrefab, RandomTerrainPos(), Quaternion.identity, transform);
            Acorn acorn = obj.GetComponent<Acorn>();
            acorn.spawner = this;
            pool.Add(acorn);
        }
    }

    public void ScheduleRespawn(Acorn acorn) => StartCoroutine(RespawnAfter(acorn, respawnDelay));

    private IEnumerator RespawnAfter(Acorn acorn, float delay)
    {
        yield return new WaitForSeconds(delay);
        acorn.transform.position = RandomTerrainPos();
        acorn.gameObject.SetActive(true);
    }

    public void ResetAll()
    {
        StopAllCoroutines();
        foreach (var a in pool)
        {
            a.transform.position = RandomTerrainPos();
            a.gameObject.SetActive(true);
        }
    }

    private Vector3 RandomTerrainPos()
    {
        Vector3 center = transform.position;
        float x = center.x + Random.Range(-spawnRadius, spawnRadius);
        float z = center.z + Random.Range(-spawnRadius, spawnRadius);
        float y = terrain != null
            ? terrain.SampleHeight(new Vector3(x, 0f, z)) + heightOffset
            : center.y + heightOffset;
        return new Vector3(x, y, z);
    }
}
