using UnityEngine;

[RequireComponent(typeof(Collider))]
public class Obstacle : MonoBehaviour
{
    public enum ObstacleType
    {
        Rock,
        DeepPit,
        Trash,
        MudPuddle
    }

    [Header("Obstacle Type")]
    public ObstacleType type = ObstacleType.Rock;

    [Tooltip("If enabled, this object is forced to use the Obstacle tag on load and in the editor.")]
    public bool enforceObstacleTag = true;

    [Tooltip("If enabled, the collider mode is chosen from the obstacle type.")]
    public bool autoConfigureCollider = true;

    [Header("Training Effects")]
    public float collisionPenalty = 0.1f;
    public float fearIncrease = 0.2f;
    public float energyPenalty = 0f;

    void Awake()
    {
        ApplyConfiguration();
    }

    void OnValidate()
    {
        ApplyConfiguration();
    }

    public void ApplyConfiguration()
    {
        if (enforceObstacleTag && gameObject.tag != "Obstacle")
            gameObject.tag = "Obstacle";

        if (!TryGetComponent(out Collider obstacleCollider)) return;

        if (autoConfigureCollider)
            obstacleCollider.isTrigger = IsAreaHazard();

        SetDefaultEffects();
    }

    public bool IsAreaHazard()
    {
        return type == ObstacleType.DeepPit || type == ObstacleType.MudPuddle;
    }

    private void SetDefaultEffects()
    {
        switch (type)
        {
            case ObstacleType.Rock:
                collisionPenalty = 0.1f;
                fearIncrease = 0.15f;
                energyPenalty = 0.02f;
                break;
            case ObstacleType.DeepPit:
                collisionPenalty = 0.5f;
                fearIncrease = 0.4f;
                energyPenalty = 0.15f;
                break;
            case ObstacleType.Trash:
                collisionPenalty = 0.08f;
                fearIncrease = 0.25f;
                energyPenalty = 0.03f;
                break;
            case ObstacleType.MudPuddle:
                collisionPenalty = 0.03f;
                fearIncrease = 0.05f;
                energyPenalty = 0.08f;
                break;
        }
    }
}
