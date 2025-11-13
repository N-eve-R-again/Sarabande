using Sarabande.Core;
using Sarabande.Levels;
using UnityEngine;

public class FakeWallEntity : MonoBehaviour, IListener, IResettable
{
    public GameObject GameObject => gameObject;

    [SerializeField, Min(0f)] private float wallInset = 0.05f;
    [SerializeField, Min(0.1f)] private float wallHeight = 1f;
    [SerializeField, Min(0.01f)] private float thinThickness = 0.10f;

    [SerializeField] private Vector2Int gridPosition;
    [SerializeField] private bool revealed = false;
    [SerializeField] float alphaOnRevealed = 0.5f;
    public void Init(Vector2Int _gridPos, Vector3 _pos, string _name)
    {
        gameObject.name = _name;
        transform.position = _pos;


        transform.position = new Vector3(_pos.x, wallHeight * 0.5f, _pos.z);
        float scaleXZ = Mathf.Max(0.001f, LevelGlobalSettings.cellSize - 2f * wallInset);
        Vector3 initScale = new Vector3(scaleXZ, wallHeight, scaleXZ);
        transform.localScale = initScale;

        LevelEntitiesManager.Instance.RegisterListener(_gridPos, this);
    }

    private void Discovered()
    {
        gameObject.GetComponent<Renderer>().material.color = new Color(1, 1, 1, 0.25f);
        revealed = true;
    }

    public void ResetToInitial()
    {
        gameObject.GetComponent<Renderer>().material.color = Color.white;
        revealed = false;
        //reset implementation here        
    }

    public void OnInteract()
    {
        if (!revealed)
        {
            Discovered();
        }
    }
}
