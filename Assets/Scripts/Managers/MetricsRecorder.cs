using System.IO;
using System.Text;
using UnityEngine;

public class MetricsRecorder : MonoBehaviour
{
    [Header("Output")]
    public string csvPath = "Metrics/results.csv";

    private StreamWriter writer;
    private int episodeIndex;

    void Start()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(csvPath)));
        writer = new StreamWriter(csvPath, false, Encoding.UTF8);
        writer.WriteLine("Episode,AcornsCollected,TotalDistance,Collisions,FinalHunger,FinalEnergy,FinalFear,CumulativeReward");
    }

    public void RecordEpisode(SquirrelAgent a)
    {
        episodeIndex++;
        writer.WriteLine(
            $"{episodeIndex},{a.AcornsCollected},{a.TotalDistance:F2}," +
            $"{a.CollisionCount},{a.hunger:F3},{a.energy:F3},{a.fear:F3},{a.GetCumulativeReward():F3}");
        writer.Flush();
    }

    void OnDestroy() => writer?.Close();
}
