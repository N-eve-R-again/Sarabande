using Sarabande.Core;
using Sarabande.Levels;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEditor;
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
    public LevelData dataCopy;
    public bool hotCopyCreated;
    public bool hotCopyModified;

    /*public List<ObstacleData> obstacles;
    public List<MessageConfig> messageConfigs;
    public List<TriggerObjectConfig> triggerObjects;
    public int2 levelDim = new int2();*/


    [Header("SELECTED OBJECT")]
    public bool objectIsSelected;
    public Color selectedColor;

    public SelectedObjectType selectedObjectType;
    public int selectedObjectIndex;

    public Vector2Int selectedCell;

    Vector3[] bounds;

    private void OnDrawGizmos()
    {
        if (dataCopy == null) return;

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



        foreach (var item in dataCopy.obstacles)
        {
            DrawObstacle(item);
        }


        foreach (var item in dataCopy.messages)
        {
            DrawSpriteIcon("Gizmo_Message", item.cell, Color.yellow);
        }


        if (objectIsSelected && currentTool == EditorToolType.Edit)
        {
            if(selectedObjectType == SelectedObjectType.Obstacle)
            {
                DrawObstacle(dataCopy.obstacles[selectedObjectIndex], true);
                return;
            }

            if (selectedObjectType == SelectedObjectType.Message)
            {
                DrawSpriteIcon("Gizmo_Message", dataCopy.messages[selectedObjectIndex].cell, selectedColor, true);
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
        foreach (var item in dataCopy.obstacles)
        {
            if (item.cell == targetcell) return true;

        }
        foreach (var item in dataCopy.messages)
        {
            if (item.cell == targetcell) return true;
        }
        return false;

    }

    public bool InsideBounds(Vector2Int targetcell)
    {
        return (targetcell.x >= 0 && targetcell.x < dataCopy.width && targetcell.y >= 0 && targetcell.y < dataCopy.height);
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
            Vector3.forward * dataCopy.height,

            Vector3.zero,
            Vector3.right * dataCopy.width,

            Vector3.forward * dataCopy.height,
            Vector3.forward * dataCopy.height + Vector3.right * dataCopy.width,

            Vector3.right * dataCopy.width,
            Vector3.forward * dataCopy.height + Vector3.right * dataCopy.width,

        };
    }

    public Vector3[] GetBounds() => bounds;

    private void ReImportLevel()
    {
        if (levelData == null) return;

        DeleteWorkingCopy();
        hotCopyCreated = true;
        dataCopy = levelData.Clone();
        dataCopy.name = "copy_" + levelData.name;
        EditorUtility.SetDirty(dataCopy);
        RefreshCopy();
    }
    public void LoadLevel()
    {
        ReImportLevel();
    }
    public void UnloadLevel()
    {
        if (hotCopyModified)
        {
            bool confirm = EditorUtility.DisplayDialog(
                "Unsaved Changes",
                "You have unsaved changes. Unload anyway?",
                "Unload",
                "Cancel"
            );

            if (!confirm) return; // Annule l'action
        }

        DeleteWorkingCopy(); // Continue
    }

    public void DiscardChanges()
    {
        bool confirm = EditorUtility.DisplayDialog(
            "Discard Changes",
            "This will lose all your modifications. Continue?",
            "Discard",
            "Cancel"
        );

        if (confirm)
        {
            ReImportLevel(); // Recharge depuis le fichier original
        }
    }
    public void CreateNewFile()
    {

    }

    public void SaveFile()
    {
        dataCopy.name = levelData.name;
        EditorUtility.SetDirty(dataCopy);

        EditorUtility.CopySerialized(dataCopy, levelData); // Copie toutes les données
        EditorUtility.SetDirty(levelData);
        AssetDatabase.SaveAssets();

        // Réinitialise le JSON pour la détection de changements
        //originalJson = JsonUtility.ToJson(dataCopy);
        hotCopyModified = false;
        ReImportLevel();
    }

    public void SaveAsNew()
    {

    }

    public void DeleteWorkingCopy()
    {
        dataCopy = null;
        hotCopyCreated = false;
        hotCopyModified= false;

        objectIsSelected = false;
    }

    public void SelectCell(Vector2Int cell)
    {

        foreach (var item in dataCopy.obstacles)
        {
            if (item.cell == cell) {

                SelectObject(item);

                objectIsSelected = true;
                hotCopyModified = true;
                return;
            }


        }
        foreach (var item in dataCopy.messages)
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
            selectedObjectIndex = dataCopy.obstacles.IndexOf((ObstacleData)obj);
        }
        else if (obj is MessageConfig)
        {
            objectIsSelected = true;
            selectedObjectType = SelectedObjectType.Message;
            selectedObjectIndex = dataCopy.messages.IndexOf((MessageConfig)obj);
        }
    }


    public void DeleteCell(Vector2Int cell)
    {
        dataCopy.obstacles.RemoveAll(buffer =>  // Pour chaque buffer
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
        dataCopy.obstacles.Add(temp);
        ReorderList();

        hotCopyModified = true;
    }

    private void ReorderList()
    {
        var sortedObstacles = dataCopy.obstacles
          .OrderByDescending(o => o.cell.y) // Plus loin en premier
          .ThenByDescending(o => o.cell.x);
        dataCopy.obstacles = new List<ObstacleData>(sortedObstacles);
    }

    private void RefreshCopy()
    {
        hotCopyModified = false;
        objectIsSelected = false;
        ReorderList();
        Updatebounds();
    }
}



