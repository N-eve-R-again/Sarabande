using Sarabande.Core;
using Sarabande.Levels;
using UnityEngine;
using Sarabande.Obstacles;
public class WallEntity : MonoBehaviour, IObstacle
{
    [SerializeField] ObstacleData config;

    [SerializeField, Min(0f)] private float wallInset = 0.05f;
    [SerializeField, Min(0.1f)] private float wallHeight = 1f;

    public ObstacleData obstacleData => config;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public void Init(ObstacleData _config, string _name)
    {
        gameObject.name = _name;
        config = _config;
        transform.position = SetPosition(config.cell);
        transform.localScale = SetSize();

        LevelGlobalSettings.SetLayerForObstacle(gameObject);
    }

    private Vector3 SetSize()
    {
        float scaleXZ = Mathf.Max(0.001f, LevelGlobalSettings.cellSize - 2f * wallInset);
        Vector3 initScale = new Vector3(scaleXZ, wallHeight, scaleXZ);
        return initScale;
    }

    private Vector3 SetPosition(Vector2Int _coord)
    {
        return GridUtils.CenterXZ(_coord) + Vector3.up *  (wallHeight * 0.5f);
    }
}
