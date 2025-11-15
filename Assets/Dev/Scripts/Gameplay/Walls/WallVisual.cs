using Sarabande.Core;
using Sarabande.Levels;
using UnityEngine;
public class WallVisual : MonoBehaviour
{
    [SerializeField, Min(0f)] private float wallInset = 0.05f;
    [SerializeField, Min(0.1f)] private float wallHeight = 1f;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public void Init(GridCoord _coord, string _name)
    {
        gameObject.name = _name;

        transform.position = SetPosition(_coord);
        transform.localScale = SetSize();

        LevelGlobalSettings.SetLayerForObstacle(gameObject);
    }

    private Vector3 SetSize()
    {
        float scaleXZ = Mathf.Max(0.001f, LevelGlobalSettings.cellSize - 2f * wallInset);
        Vector3 initScale = new Vector3(scaleXZ, wallHeight, scaleXZ);
        return initScale;
    }

    private Vector3 SetPosition(GridCoord _coord)
    {
        float x = (_coord.x + 0.5f) * LevelGlobalSettings.cellSize;
        float z = (_coord.z + 0.5f) * LevelGlobalSettings.cellSize;
        return new Vector3(x, wallHeight * 0.5f,z);
    }
}
