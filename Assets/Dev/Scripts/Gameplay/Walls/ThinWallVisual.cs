using Sarabande.Core;
using Sarabande.Levels;
using UnityEngine;

public class ThinWallVisual : MonoBehaviour
{

    [SerializeField, Min(0.1f)] private float wallHeight = 1f;
    [SerializeField, Min(0.01f)] private float thinThickness = 0.10f;
    [SerializeField] private bool isVertical = false;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public void Init(EdgeBlocker _coord, string _name, bool vertical)
    {
        gameObject.name = _name;
        isVertical = vertical;

        transform.position = SetPosition(isVertical, _coord);
        transform.localScale = SetSize();

        LevelGlobalSettings.SetLayerForObstacle(gameObject);
    }

    private Vector3 SetSize()
    {
        Vector3 size = Vector3.zero;

        if (isVertical) // Séparation verticale entre deux Z donc thin sur l'axe Z
        {
            size = new Vector3(LevelGlobalSettings.cellSize, wallHeight, thinThickness);
        }
        else    // Séparation horizontale entre deux X donc thin sur l'axe X
        {
            size = new Vector3(thinThickness, wallHeight, LevelGlobalSettings.cellSize);
        }

        return size;
    }

    private Vector3 SetPosition(bool vertical, EdgeBlocker coord)
    {
        float x; float z;

        if (isVertical)// Séparation horizontale entre deux rangées : x au centre de la colonne, z sur la ligne entre les 2 cases
        {
            x = coord.a.x * LevelGlobalSettings.cellSize + LevelGlobalSettings.cellSize * 0.5f;
            z = Mathf.Min(coord.a.z, coord.b.z) * LevelGlobalSettings.cellSize + LevelGlobalSettings.cellSize; // ligne entre z et z+1
        }
        else // Séparation verticale entre deux colonnes : z au centre de la rangée, x sur la ligne entre les 2 cases
        {
            x = Mathf.Min(coord.a.x, coord.b.x) * LevelGlobalSettings.cellSize + LevelGlobalSettings.cellSize; // ligne entre x et x+1
            z = coord.a.z * LevelGlobalSettings.cellSize + LevelGlobalSettings.cellSize * 0.5f;
        }

        return new Vector3(x, wallHeight * 0.5f, z);
    }
}
