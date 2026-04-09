using Sarabande.Core;
using Sarabande.Levels;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


public class BootStrapper : MonoBehaviour
{
    [SerializeField] private LevelRoot levelRoot;
    [SerializeField] private LevelLoader levelLoader;

    public List<IListener> listeners = new List<IListener>();
    public List<ITriggerable> triggerables = new List<ITriggerable>();
    public List<IActor> actors = new List<IActor>();
    public List<IObstacle> obstacles = new List<IObstacle>();
    public List<IInitializable> initializables = new List<IInitializable>();

    private void Start()
    {
        Boot();
    }

    private void Boot() //Logique pour lancer le niveau
    {
        ClearLists(); //Nettoyer le scrap, si jamais

        if(!GetLevelRoot()) return; //on arrete tout si on trouve pas level root
        InitLevelRoot(); //Initialiser LevelRoot

        if (FindLevelLoader()) ConstructLevelOnStart();

        ScrapObjects();
        LogDetection();

        BuildNavigation();

        RegisterTriggerables();
        RegisterListeners();
        RegisterActors();

        InitEntities();

        LogGen.LogAs(this, "Everything Done");
        LogGen.LogAs(this, "Started Game", "cyan");
    }
    private bool GetLevelRoot()
    {
        levelRoot = FindFirstObjectByType<LevelRoot>();
        if (levelRoot == null)
        {
            LogGen.ErrorAs(this, "Level Root Not Found");
            return false;
        }

        LogGen.LogAs(this, "Level Root Found, will Start Boot Sequence", "green");
        return true;
    }
    private void InitLevelRoot() => levelRoot.Init();
    private void ConstructLevelOnStart()
    {

        levelLoader.Construct(levelRoot.transform);

        if (!levelLoader.ready)
            LogGen.ErrorAs(this, "LevelLoader construct failed", "red");


    }


    private List<T> ScrapAll<T>() where T : class
    => FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
        .OfType<T>()
        .ToList();

    private bool FindLevelLoader()
    {
        levelLoader = FindFirstObjectByType<LevelLoader>();

        if (levelLoader == null) {
            LogGen.LogAs(this, "LevelLoader wasn't found, skipping Construct Step", "grey");
            return false; 
        }
        if (!levelLoader.enabled)
        {
            LogGen.LogAs(this, "LevelLoader isn't active, skipping Construct Step", "grey");
            return false;
        }

        LogGen.LogAs(this, "LevelLoader up and running, will Construct", "orange");
        return true;
    }

    private void ClearLists()
    {
        obstacles.Clear();
        listeners.Clear();
        triggerables.Clear();
        actors.Clear();
        initializables.Clear();
    }

    private void ScrapObjects()
    {

        obstacles = ScrapAll<IObstacle>();
        listeners = ScrapAll<IListener>();
        triggerables = ScrapAll<ITriggerable>();
        actors = ScrapAll<IActor>();
        initializables = ScrapAll<IInitializable>();

    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    private void RegisterTriggerables()
    {
        if (triggerables.Count == 0) {
            LogGen.LogAs(this, "No Triggerables Found, skipped Registry");
            return;
        }

        LogGen.LogAs(this, "Started Triggerable Registry", "orange");
        foreach (var triggerable in triggerables)
        {
            triggerable.Register();
        }
        LogGen.LogAs(this, "Finished Triggerable Registry", "green");

    }
    private void RegisterListeners()
    {
        if (listeners.Count == 0)
        {
            LogGen.LogAs(this, "No Listeners Found, skipped Registry");
            return;
        }

        LogGen.LogAs(this, "Started Listener Registry", "orange");

        foreach (var listener in listeners)
        {
            listener.Register();
        }
        LogGen.LogAs(this, "Finished Listener Registry", "green");

    }
    private void RegisterActors()
    {
        if (actors.Count == 0)
        {
            LogGen.LogAs(this, "No Actors Found, skipped Registry");
            return;
        }

        LogGen.LogAs(this, "Started Actor Registry", "orange");
        foreach (var actor in actors)
        {
            LogGen.LogAs(this, "No need to register actor for now");
        }

        //aucune implementation
        LogGen.LogAs(this, "Finished Actor Registry", "green");

    }



    private void InitEntities()
    {
        LogGen.LogAs(this, "Starting Entities Initialization", "orange");
        foreach (var entity in initializables)
            entity.Init();

        LogGen.LogAs(this, "Finished Entities Initialization", "orange");
    }

    public void BuildNavigation()
    {
        if(obstacles.Count == 0)
        {
            LogGen.LogAs(this, "No Obstacles Found, Skipped Navigation Build");
            return ;
        }

        LogGen.LogAs(this, "Starting Navigation Build", "orange");

        foreach (var obstacle in obstacles)
        {
            obstacle.Build();
        }

        LogGen.LogAs(this, "Finished Navigation Build", "green");
    }


    private void LogDetection()
    {
        LogGen.LogAs(this, 
            $"Detected: \n" +
            $"{triggerables.Count} Triggerables, " +
            $"{listeners.Count} Listeners, " +
            $"{actors.Count} Actors, " +
            $"{obstacles.Count} Obstacles, " +
            $"{initializables.Count} Initializables"
            );
    }




}
