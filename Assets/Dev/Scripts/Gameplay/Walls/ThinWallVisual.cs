using Sarabande.Core;
using Sarabande.Levels;
using UnityEngine;


public class ThinWallVisual : MonoBehaviour
{
    [SerializeField] Obstacle config;

    [SerializeField, Min(0.1f)] private float wallHeight = 1f;
    [SerializeField, Min(0.01f)] private float thinThickness = 0.10f;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public void Init(Obstacle _config, string _name)
    {
        gameObject.name = _name;

        config = _config;

        transform.position = SetPosition(config.direction, config.cell);
        transform.localScale = SetSize(config.direction);

        LevelGlobalSettings.SetLayerForObstacle(gameObject);
    }

    private Vector3 SetSize(CardinalDirection dir)
    {
        Vector3 size = Vector3.zero;

        if (dir == CardinalDirection.North || dir == CardinalDirection.South) // Séparation verticale entre deux Z donc thin sur l'axe Z
        {
            size = new Vector3(LevelGlobalSettings.cellSize, wallHeight, thinThickness);
        }
        else    // Séparation horizontale entre deux X donc thin sur l'axe X
        {
            size = new Vector3(thinThickness, wallHeight, LevelGlobalSettings.cellSize);
        }

        return size;
    }

    private Vector3 SetPosition(CardinalDirection dir, Vector2Int cell)
    {

        Vector2Int vecDir = GridUtils.DirToVec(dir);

        float offset = LevelGlobalSettings.cellSize * 0.5f;

        Vector3 vecDir3 = new Vector3(vecDir.x, 0f, vecDir.y);

        
        return GridUtils.CenterGrid(cell) + vecDir3 * offset;
    }
}
