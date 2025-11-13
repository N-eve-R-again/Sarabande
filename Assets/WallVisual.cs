using Sarabande.Levels;
using UnityEngine;
using static UnityEditor.PlayerSettings;

public class WallVisual : MonoBehaviour
{
    [SerializeField, Min(0f)] private float wallInset = 0.05f;
    [SerializeField, Min(0.1f)] private float wallHeight = 1f;
    [SerializeField, Min(0.01f)] private float thinThickness = 0.10f;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public void Init(Vector3 _pos, string _name)
    {
        gameObject.name = _name;
        transform.position = _pos;


        transform.position = new Vector3(_pos.x, wallHeight * 0.5f, _pos.z);
        float scaleXZ = Mathf.Max(0.001f, LevelGlobalSettings.cellSize - 2f * wallInset);
        Vector3 initScale = new Vector3(scaleXZ, wallHeight, scaleXZ);
        transform.localScale = initScale;

        //assignation de layer
        int obsLayer = LayerMask.NameToLayer(LevelGlobalSettings.obstaclesLayerName);
        if (obsLayer != -1) gameObject.layer = obsLayer;
        else Debug.LogWarning($"[LevelLoader] Layer '{LevelGlobalSettings.obstaclesLayerName}' introuvable. Crée-le dans Project Settings > Tags and Layers.");

    }

}
