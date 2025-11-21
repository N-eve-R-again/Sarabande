using Sarabande.Core;
using Sarabande.Levels;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using static UnityEditor.PlayerSettings;



public class LevelEditorViewer : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public LevelContext levelContext;
    public LevelData levelDataCopy;
    public Texture messageSprite;
    public Texture heroSprite;
    public Texture heroStartSprite;
    public Vector3 triggerpadsize = Vector3.one;
    public Vector3 gizmosOffsets = Vector3.one;
    [Range(0.05f,1f)]
    public float arrowtraplinesize = 1f;
    [Range(0.05f, 1f)]
    public float arrowtrapsize = 1f;
    public List<Vector3> walls = new List<Vector3>();
    public List<Vector3> thinwalls = new List<Vector3>();
    public List<CardinalDirection> thinwallsdirs = new List<CardinalDirection>();
    public List<Vector3> triggerpads = new List<Vector3>();
    public List<Vector3> triggerpadsgoto = new List<Vector3>();
    public List<Vector3> arrowtraps = new List<Vector3>();
    public List<Vector3> messages = new List<Vector3>();
    public List<Vector3> fakewalls = new List<Vector3>();
    public List<float> arrowtrapdirections = new List<float>();
    public Vector3[] bounds = new Vector3[4];
    public Dictionary<int,Vector3> links = new();

    public Color transparent = Color.white;

    public ActorSpawn herospawn;

    // Update is called once per frame
    void OnDrawGizmos()
    {
        if (!this.enabled) return;
        Gizmos.color = Color.red;
        Gizmos.DrawLine(bounds[0], bounds[1]);
        Gizmos.DrawLine(bounds[1], bounds[2]);
        Gizmos.DrawLine(bounds[2], bounds[3]);
        Gizmos.DrawLine(bounds[3], bounds[0]);
        Gizmos.color = Color.white;



        foreach (var pos in walls)
        {
            Gizmos.DrawWireCube(pos, Vector3.one * LevelGlobalSettings.cellSize * 0.95f);
            
        }
        int i = 0;
        foreach (var pos in thinwalls)
        {

            Vector3 a = pos;
            Vector3 b = pos + ThinWallSetPosition(thinwallsdirs[i]);
            Gizmos.color = Color.white * transparent;

            Gizmos.DrawWireCube(a, Vector3.one *  0.05f);
            Gizmos.DrawLine(a,b);
            Gizmos.color = Color.white;

            Gizmos.DrawWireCube(b, ThinWallSetSize(thinwallsdirs[i]));
            i++;
        }


        Gizmos.color = Color.cyan;
        foreach (var pos in fakewalls)
        {
            Gizmos.DrawWireCube(pos, Vector3.one * LevelGlobalSettings.cellSize * 0.8f);

        }

        foreach (var pos in messages)
        {

            Gizmos.DrawIcon(pos, messageSprite.name,true);
        }

        i = 0;
        foreach (var pos in triggerpads)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(pos, triggerpadsize * LevelGlobalSettings.cellSize);
            Gizmos.color = Color.yellow * transparent;
            Gizmos.DrawLine(pos, triggerpadsgoto[i]);
            i++;
        }


        i = 0;
        
        foreach (var pos in arrowtraps)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawSphere(pos, arrowtrapsize);
            Gizmos.DrawLine(pos, pos + Quaternion.Euler(0, arrowtrapdirections[i], 0) * Vector3.forward * arrowtraplinesize);
            i++;
        }
        Vector3 heropos = GridUtils.CenterXZ(herospawn.spawnCell) + gizmosOffsets;
        Vector3 heroposstart = heropos + Quaternion.Euler(0, SetRotation(herospawn.spawnDirection), 0) * Vector3.forward * LevelGlobalSettings.cellSize;
        
        if(heroSprite != null) Gizmos.DrawIcon(heropos, heroSprite.name);
        Gizmos.color = Color.white;
        Gizmos.DrawIcon(heroposstart, heroStartSprite.name);
        Gizmos.DrawLine(heropos, heroposstart);
    }

    public void ReImportLevel()
    {
        levelDataCopy = levelContext.LevelData;
        walls.Clear();
        thinwalls.Clear();
        thinwallsdirs.Clear();

        triggerpads.Clear();
        arrowtraps.Clear();
        arrowtrapdirections.Clear();
        triggerpadsgoto.Clear();
        fakewalls.Clear();
        messages.Clear();
        
        Vector3 offsety = Vector3.up * 0.2f;
        bounds[0] = offsety + Vector3.zero;
        bounds[1] = offsety + Vector3.forward * levelDataCopy.height;
        bounds[2] = offsety + (Vector3.forward + Vector3.right) * levelDataCopy.width;
        bounds[3] = offsety + Vector3.right * levelDataCopy.width;
        herospawn = levelDataCopy.heroSpawnConfig;

        foreach (var item in levelDataCopy.obstacles)
        {
            if(item.type == Obstacle.ObstacleType.Wall)
            {
                walls.Add((Vector3.up * 0.5f) + GridUtils.CenterGrid(item.cell, LevelGlobalSettings.cellSize));
            }
            else
            {
                thinwalls.Add((Vector3.up * 0.5f) + GridUtils.CenterGrid(item.cell, LevelGlobalSettings.cellSize));
                thinwallsdirs.Add(item.direction);
            }


        }

        foreach (var item in levelDataCopy.newArrowTraps)
        {
            Vector3 newpos = (Vector3.up * 0.5f) + GridUtils.CenterGrid(item.cell);
            newpos -= Quaternion.Euler(0, SetRotation(item.travelDir), 0) * Vector3.forward * 0.5f;
            arrowtraps.Add(newpos);
            links[item.triggerKey] = newpos;
            arrowtrapdirections.Add(SetRotation(item.travelDir));
        }

        foreach (var item in levelDataCopy.newTriggerPads)
        {
            triggerpads.Add((Vector3.up * triggerpadsize.y * 0.5f) + GridUtils.CenterXZ(item.cell));
            if(links.TryGetValue(item.triggerKey,out Vector3 link)){
                triggerpadsgoto.Add(link);
            }
        }

        foreach (var item in levelDataCopy.messages)
        {
            Vector3 messagepos = GridUtils.CenterXZ(item.cell) + gizmosOffsets;
            messages.Add(messagepos);
        }

        foreach (var item in levelDataCopy.passThroughWalls)
        {
            fakewalls.Add((Vector3.up * 0.5f) + GridUtils.CenterXZ(item));
        }
    }

    private float SetRotation(CardinalDirection dir)
    {
        float yRot = 0f;
        switch (dir)
        {
            case CardinalDirection.North:
                yRot = 0f; break;
            case CardinalDirection.South: yRot = 180f; break;
            case CardinalDirection.East: yRot = 90f; break;
            case CardinalDirection.West: yRot = 270f; break;

        }
        return yRot;
    }


    private Vector3 ThinWallSetSize(CardinalDirection dir)
    {
        Vector3 size = Vector3.zero;

        if (dir == CardinalDirection.North || dir == CardinalDirection.South) // Séparation verticale entre deux Z donc thin sur l'axe Z
        {
            size = new Vector3(LevelGlobalSettings.cellSize, 0.8f, 0.15f);
        }
        else    // Séparation horizontale entre deux X donc thin sur l'axe X
        {
            size = new Vector3(0.15f, 0.8f, LevelGlobalSettings.cellSize);
        }

        return size;
    }

    private Vector3 ThinWallSetPosition(CardinalDirection dir)
    {

        Vector2Int vecDir = GridUtils.DirToVec(dir);

        float offset = LevelGlobalSettings.cellSize * 0.5f;

        Vector3 vecDir3 = new Vector3(vecDir.x, 0f, vecDir.y);


        return vecDir3 * offset;
    }

}

[CustomEditor(typeof(LevelEditorViewer))]
public class YourScriptEditor : Editor
{

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        if (GUILayout.Button("Refresh"))
            test(target.GameObject());
        
    }

    public void test(GameObject go)
    {
        go.GetComponent<LevelEditorViewer>().ReImportLevel();
    }

}

