using UnityEngine;

public class PrefabLibrary : MonoBehaviour
{
    [SerializeField] private GameObject wallPrefab;
    [SerializeField] private GameObject fakeWallPrefab;

    public GameObject GetWallPrefab()
    {
        return wallPrefab;
    }

    public GameObject GetFakeWallPrefab() 
    { 
        return fakeWallPrefab;
    }

}
