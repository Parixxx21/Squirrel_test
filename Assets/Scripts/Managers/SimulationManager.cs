using System.Collections.Generic;
using UnityEngine;

public class SimulationManager : MonoBehaviour
{
    public static SimulationManager Instance { get; private set; }

    [Header("Scene References")]
    public List<SquirrelAgent> agents;
    public AcornSpawner acornSpawner;

    private MetricsRecorder metrics;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        metrics = GetComponent<MetricsRecorder>();
    }

    // Called by SquirrelAgent at episode end
    public void OnAgentEpisodeEnd(SquirrelAgent agent)
    {
        metrics?.RecordEpisode(agent);
        acornSpawner?.ResetAll();
    }
}
