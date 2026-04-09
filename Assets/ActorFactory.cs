using UnityEngine;
using Sarabande.Core;
using Sarabande.Levels;
using Sarabande.Player;

public class ActorFactory : MonoBehaviour
{
    private Transform actorsFolder;
    private Transform heroParent;
    private Transform nmesParent;

    private int actorscount = 0;
    private Transform ParentOfAll;

    [SerializeField] private GameObject heroPrefab;
    [SerializeField] private GameObject nmePrefab;
    public int BuildActors(LevelData levelData, Transform Root)
    {
        actorscount = 0;
        ParentOfAll = Root;
        CreateFolders();

        CreateHero(levelData);
        return actorscount;
    }

    private void CreateHero(LevelData data)
    {
        actorscount++;
        GameObject temp = Instantiate(heroPrefab, heroParent);
        temp.name = "Player";
        HeroController controller = temp.GetComponent<HeroController>();
        controller.Sync(data.heroSpawnConfig);
    }

    public void CreateFolders()
    {
        actorsFolder = new GameObject("Actors").transform;
        actorsFolder.SetParent(transform.parent);


        heroParent = new GameObject("Hero").transform;
        heroParent.SetParent(actorsFolder, false);

        nmesParent = new GameObject("Nmes").transform;
        nmesParent.SetParent(actorsFolder, false);

        actorsFolder.SetParent(ParentOfAll);
    }
}
