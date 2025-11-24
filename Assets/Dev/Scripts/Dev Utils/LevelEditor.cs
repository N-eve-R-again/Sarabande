using Sarabande.Core;
using Sarabande.Levels;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;
using static UnityEditor.Progress;


public enum EditorToolType
{
    Place,
    Erase,
    Edit,
    Misc
}

public enum SelectedObjectType
{
    None,
    Obstacle,
    Message,
}

public class LevelEditor : MonoBehaviour
{


    public EditorToolType currentTool = EditorToolType.Place;
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    public LevelData levelData;
    public LevelData levelDataCopy;
    public bool hotCopyCreated;
    public bool hotCopyModified;

    public List<ObstacleData> obstacles;
    public List<MessageConfig> messageConfigs;
    public List<TriggerObjectConfig> triggerObjects;
    public int2 levelDim = new int2();
    public bool solidify = false;

    [Header("SELECTED OBJECT")]
    public bool objectIsSelected;
    public Color selectedColor;

    public SelectedObjectType selectedObjectType;
    public int selectedObjectIndex;

    public Vector2Int selectedCell;

    Vector3[] bounds;

    private void OnDrawGizmos()
    {
        if (levelDataCopy == null) return;

        if (!enabled) return;
        if (!hotCopyCreated) return;

        if(bounds == null)
        {
            Updatebounds();
        }
        if (bounds.Length == 0)
        {
            Updatebounds();
        }
        DrawBounds();



        foreach (var item in obstacles)
        {
            DrawObstacle(item);
        }


        foreach (var item in messageConfigs)
        {
            DrawSpriteIcon("Gizmo_Message", item.cell, Color.yellow);
        }


        if (objectIsSelected && currentTool == EditorToolType.Edit)
        {
            if(selectedObjectType == SelectedObjectType.Obstacle)
            {
                DrawObstacle(obstacles[selectedObjectIndex], true);
                return;
            }

            if (selectedObjectType == SelectedObjectType.Message)
            {
                DrawSpriteIcon("Gizmo_Message",messageConfigs[selectedObjectIndex].cell, selectedColor, true);
                return;
            }

        }

    }

    private void DrawSpriteIcon(string iconName, Vector2Int cell, Color outlinecolor, bool iconWithOutline = false)
    {

        if (iconWithOutline)
        {
            Gizmos.DrawIcon(GridUtils.CenterXZ(cell) + Vector3.up * 0.5f, iconName + "_O");
        }
        else
        {
            Gizmos.DrawIcon(GridUtils.CenterXZ(cell) + Vector3.up * 0.5f, iconName);
        }

        Gizmos.color = outlinecolor;
        Gizmos.DrawLine(GridUtils.CenterXZ(cell) + Vector3.up * 0.5f, GridUtils.CenterXZ(cell));
        Gizmos.DrawWireCube(GridUtils.CenterXZ(cell),(Vector3.right + Vector3.forward) * 0.6f);
        Gizmos.DrawWireCube(GridUtils.CenterXZ(cell),(Vector3.right + Vector3.forward) * 0.75f);
        Gizmos.color = Color.white;
    }

    private void DrawObstacle(ObstacleData obstacle, bool selectionGizmo = false)
    {
        if(selectionGizmo)
        {
            Gizmos.color = selectedColor;
        }
        else
        {
            Gizmos.color = Color.white;
        }

        if (obstacle.type == ObstacleData.ObstacleType.Wall)
        {
            DrawCubeAtCell(obstacle.cell, selectionGizmo);
        }
        else
        {
            DrawThinWallAt(obstacle.cell, obstacle.thinWallDirection, selectionGizmo);
        }
    }

    void DrawBounds()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawLineList(bounds);
    }
    void DrawThinWallAt(Vector2Int cell, CardinalDirection dir, bool wire) 
    { 
        Vector3 size = Vector3.zero;

        if (dir == CardinalDirection.North || dir == CardinalDirection.South) // Séparation verticale entre deux Z donc thin sur l'axe Z
        {
            size = new Vector3(LevelGlobalSettings.cellSize, LevelGlobalSettings.cellSize, 0.15f);
        }
        else    // Séparation horizontale entre deux X donc thin sur l'axe X
        {
            size = new Vector3(0.15f, LevelGlobalSettings.cellSize, LevelGlobalSettings.cellSize);
        }
        Vector3 pos = new Vector3(cell.x * LevelGlobalSettings.cellSize, 0, cell.y * LevelGlobalSettings.cellSize);


        Vector3 cellpos = GridUtils.CenterXZ(cell);
        pos = cellpos;

        Vector2Int vecDir = GridUtils.DirToVec(dir);

        float offset = LevelGlobalSettings.cellSize * 0.5f;

        Vector3 vecDir3 = new Vector3(vecDir.x, 1f, vecDir.y);

              
        pos += vecDir3 * offset;



        Gizmos.DrawCube(cellpos, Vector3.one * 0.2f);
        Gizmos.DrawLine(cellpos, pos);
        Gizmos.DrawCube(pos, size);

        if (wire)
        {
            Gizmos.DrawWireCube(cellpos, Vector3.one * 0.2f);
            
            Gizmos.DrawWireCube(pos, size);
        }
    }


    void DrawCubeAtCell(Vector2Int cell, bool wire)
    {

        Vector3 size;

        float halfcell = LevelGlobalSettings.cellSize * 0.5f;

        size = Vector3.one * LevelGlobalSettings.cellSize * 0.99f;

        Vector3 pos = new Vector3(cell.x * LevelGlobalSettings.cellSize, 0, cell.y * LevelGlobalSettings.cellSize);

        pos += new Vector3(halfcell,size.y*0.5f, halfcell);



        Gizmos.DrawCube(pos, size);
        if (wire)
        {
            Gizmos.DrawWireCube(pos, size);

        }
    }
    public bool CellOccuped(Vector2Int targetcell)
    {
        foreach (var item in obstacles)
        {
            if (item.cell == targetcell) return true;

        }
        foreach (var item in messageConfigs)
        {
            if (item.cell == targetcell) return true;
        }
        return false;

    }

    public bool InsideBounds(Vector2Int targetcell)
    {
        return (targetcell.x >= 0 && targetcell.x < levelDim.x && targetcell.y >= 0 && targetcell.y < levelDim.y);
    }

    /*public Vector2Int GetBounds()
    {
        return new Vector2Int(levelDataCopy.width, levelDataCopy.height);

    }*/

    public void SetLevelSize(int add, int width)
    {
        //if()
    }

    public void Updatebounds()
    {
        bounds = new Vector3[] {
            Vector3.zero,
            Vector3.forward * levelDim.y,

            Vector3.zero,
            Vector3.right * levelDim.x,

            Vector3.forward * levelDim.y,
            Vector3.forward * levelDim.y + Vector3.right * levelDim.x,

            Vector3.right * levelDim.x,
            Vector3.forward * levelDim.y + Vector3.right * levelDim.x,

        };
    }

    public Vector3[] GetBounds() => bounds;

    public List<ObstacleData> GetObstacles() => obstacles;

    public void ReImportLevel()
    {
        if (levelData == null) return;

        DeleteWorkingCopy();
        hotCopyCreated = true;
        levelDataCopy = levelData.Clone();
        RefreshCopy();
    }

    public void DeleteWorkingCopy()
    {
        levelDataCopy = null;
        hotCopyCreated = false;
        hotCopyModified= false;

        objectIsSelected = false;
    }

    public void SelectCell(Vector2Int cell)
    {

        foreach (var item in obstacles)
        {
            if (item.cell == cell) {

                SelectObject(item);

                objectIsSelected = true;
                hotCopyModified = true;
                return;
            }


        }
        foreach (var item in messageConfigs)
        {
            if(item.cell == cell)
            {
                SelectObject(item);


                objectIsSelected = true;
                hotCopyModified = true;
                return;
            }
        }
        objectIsSelected = false;
        SelectObject(null);
        selectedCell = new Vector2Int(-1, -1);


    }

    public void SelectObject(object obj)
    {
        if (obj == null)
        {
            selectedObjectType = SelectedObjectType.None;
            selectedObjectIndex = -1;
            objectIsSelected= false;
            return;
        }

        if (obj is ObstacleData)
        {
            objectIsSelected = true;
            selectedObjectType = SelectedObjectType.Obstacle;
            selectedObjectIndex = obstacles.IndexOf((ObstacleData)obj);
        }
        else if (obj is MessageConfig)
        {
            objectIsSelected = true;
            selectedObjectType = SelectedObjectType.Message;
            selectedObjectIndex = messageConfigs.IndexOf((MessageConfig)obj);
        }
    }


    public void DeleteCell(Vector2Int cell)
    {
        obstacles.RemoveAll(buffer =>  // Pour chaque buffer
        {
            // Si les conditions sont remplies :
            if (buffer.cell == cell)
            {
                if(selectedCell == buffer.cell)
                {

                    objectIsSelected = false;
                }
                hotCopyModified = true;
                return true;  // buffer supprimé
            }
            return false;  // buffer gardé et ignoré
        });
        ReorderList();
    }


    public void CreateCell(Vector2Int cell)
    {
        ObstacleData temp = new ObstacleData(ObstacleData.ObstacleType.Wall, cell,CardinalDirection.North );
        obstacles.Add(temp);
        ReorderList();

        hotCopyModified = true;
    }

    private void ReorderList()
    {
        var sortedObstacles = obstacles
          .OrderByDescending(o => o.cell.y) // Plus loin en premier
          .ThenByDescending(o => o.cell.x);
        obstacles = new List<ObstacleData>(sortedObstacles);
    }

    public void RefreshCopy()
    {
        levelDim = new int2(levelDataCopy.width, levelData.height);
        hotCopyModified = false;
        obstacles = levelDataCopy.obstacles;
        messageConfigs = levelDataCopy.messages;

        objectIsSelected = false;
        ReorderList();
        Updatebounds();
    }
}



