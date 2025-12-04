using Sarabande.Core;
using Sarabande.Levels;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using static UnityEditor.PlayerSettings;
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
    TriggerObject,
    Gate,
    ArrowTrap
}

public class TriggerLink
{
    public Vector2Int cellA;
    public Vector2Int cellB;

    public TriggerLink(Vector2Int _cellA, Vector2Int _cellB)
    {
        this.cellA = _cellA;
        this.cellB = _cellB;
    }

    public bool valid()
    {
        return cellB != -Vector2Int.one;
    }
}

public class LevelEditor : MonoBehaviour
{


    public EditorToolType currentTool = EditorToolType.Place;
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    public LevelData levelData;
    public LevelData dataCopy;
    public bool hotCopyCreated;
    public bool hotCopyModified;

    [Header("SELECTED OBJECT")]
    public bool objectIsSelected;
    public Color selectedColor;

    public SelectedObjectType selectedObjectType;
    public int selectedObjectIndex;
    public List<object> conflictedSelection;
    public Vector2Int selectedCell;

    public Dictionary<Vector2Int,List<object>> lookupTable = new();
    public List<TriggerLink> links = new();
    public Dictionary<string, Vector2Int> availableKeys = new();


    Vector3[] bounds;

    private void OnEnable()
    {

    }

    private void OnDrawGizmos()
    {
        if(lookupTable.Count == 0 && dataCopy != null)
        {
            UpdateLookUpList();
            Debug.Log("refreshedLookup");
        }

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


        foreach (var space in lookupTable)
        {
            foreach (var item in space.Value)
            {
                switch (item)
                {
                    case ObstacleData obstacle:

                        DrawObstacle(obstacle);
                        break;
                    case MessageConfig message:

                        DrawSpriteIcon("Gizmo_Message", message.cell, Color.yellow);
                        break;
                    case TriggerObjectConfig triggerObject:
                        DrawTriggerObject(triggerObject); 
                        break;
                    case GateConfig gateConfig:
                        DrawGate(gateConfig,false);
                        break;
                    case ArrowTrapConfig arrowTrapConfig:
                        Gizmos.color = Color.magenta;
                        DrawCubeAtCell(arrowTrapConfig.cell, true);
                        break;
                    default:

                        break;
                }
            }
        }


        foreach (var item in links)
        {
            if (item.valid())
            {
                Gizmos.color = new Color(1, 0.5f, 0);
                Gizmos.DrawLine(GridUtils.CenterXZ(item.cellA), GridUtils.CenterInCell(item.cellB));
            }
            else
            {
                Gizmos.color =Color.red;
                Gizmos.DrawLine(GridUtils.CenterXZ(item.cellA), GridUtils.CenterXZ(item.cellA) + Vector3.up * 0.4f);
            }

        }
        Gizmos.color = Color.white;

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

            if(selectedObjectType == SelectedObjectType.TriggerObject)
            {
                DrawTriggerObject(dataCopy.triggerObjects[selectedObjectIndex],true);
            }
            if(selectedObjectType == SelectedObjectType.Gate)
            {
                DrawGate(dataCopy.gates[selectedObjectIndex],true);
            }
            if(selectedObjectType == SelectedObjectType.ArrowTrap)
            {
                //DrawTriggerObject(dataCopy.triggerObjects[selectedObjectIndex],true);
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

    private void DrawObstacle(ObstacleData obstacle,  bool selectionGizmo = false)
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

    private void DrawGate(GateConfig gate, bool selectionGizmo = false)
    {
        if (selectionGizmo)
        {
            Gizmos.color = selectedColor;
        }
        else
        {
            Gizmos.color = Color.yellow;
        
        }
        DrawGateAt(gate.cell,gate.direction,selectionGizmo);
    }

    public void DrawTriggerObject(TriggerObjectConfig triggerObject, bool selectionGizmo = false)
    {
        if (selectionGizmo)
        {
            Gizmos.color = selectedColor;
        }
        else
        {

            Gizmos.color = new Color(0.2f, 0.9f, 0.9f);

        }
        if(triggerObject.type == TriggerObjectType.TriggerPad)
        {
            DrawPressurePadAt(triggerObject.cell, selectionGizmo);
        }
        else
        {
            DrawLeverAt(triggerObject.cell,triggerObject.attachedTo, selectionGizmo);
        }


    }



    private void DrawPressurePadAt(Vector2Int cell, bool wire)
    {

        Vector3 size = new Vector3(1f,0.1f,1f);

        float halfcell = LevelGlobalSettings.cellSize * 0.5f;

        size *= LevelGlobalSettings.cellSize;

        Vector3 pos = new Vector3(cell.x * LevelGlobalSettings.cellSize, 0, cell.y * LevelGlobalSettings.cellSize);

        pos += new Vector3(halfcell, size.y * 0.5f, halfcell);

        Gizmos.DrawWireCube(pos, size*0.5f);
        Gizmos.DrawCube(pos, size*0.25f);
        Gizmos.DrawWireCube(pos, size*0.75f);
        if (wire)
        {
            Gizmos.DrawCube(pos, size * 0.75f);

        }
    }

    private void DrawLeverAt(Vector2Int cell,CardinalDirection dir, bool wire)
    {
        Vector3 cellpos = GridUtils.CenterInCell(cell);
        Vector3 vecDir3 = GridUtils.DirToVec3(dir);



        float offset = LevelGlobalSettings.cellSize * 0.5f;

        vecDir3 *= offset;

        Vector3 size = new Vector3(0.3f, 0.1f, 0.3f);
        //Gizmos.DrawCube(cellpos, size);
        Gizmos.DrawWireCube(cellpos, new Vector3(0.8f,0.8f,0.8f));
        Gizmos.DrawLine(cellpos, cellpos+ vecDir3);
        Gizmos.DrawWireCube(cellpos + vecDir3,Vector3.one * 0.2f);
        Gizmos.DrawCube(cellpos + vecDir3,Vector3.one * 0.18f);
    }

    public void MoveSelectedObject(Vector2Int dir)
    {
        switch (selectedObjectType)
        {
            case SelectedObjectType.Obstacle:
                selectedCell = dataCopy.obstacles[selectedObjectIndex].cell += dir; break;
                
            case SelectedObjectType.Message:
                selectedCell = dataCopy.messages[selectedObjectIndex].cell += dir; break;
            case SelectedObjectType.TriggerObject:
                selectedCell = dataCopy.triggerObjects[selectedObjectIndex].cell += dir; break;
            case SelectedObjectType.Gate:
                selectedCell = dataCopy.gates[selectedObjectIndex].cell += dir; break;
            case SelectedObjectType.ArrowTrap:
                selectedCell = dataCopy.newArrowTraps[selectedObjectIndex].cell += dir; break;

        }
        UpdateLookUpList();
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


        Vector3 cellpos = GridUtils.CenterInCell(cell);
        
        float offset = LevelGlobalSettings.cellSize * 0.5f;

        Vector3 vecDir3 = GridUtils.DirToVec3(dir);

        Vector3 pos = cellpos + vecDir3* offset;
        

        Gizmos.DrawCube(cellpos, Vector3.one * 0.2f);
        Gizmos.DrawLine(cellpos, pos);
        Gizmos.DrawCube(pos, size);

        if (wire)
        {
            Gizmos.DrawWireCube(cellpos, Vector3.one * 0.2f);
            
            Gizmos.DrawWireCube(pos, size);
        }
    }

    private void DrawGateAt(Vector2Int cell, CardinalDirection dir, bool wire)
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

        Vector3 cellpos = GridUtils.CenterInCell(cell);

        float offset = (LevelGlobalSettings.cellSize * 0.5f) - 0.15f * 0.5f;

        Vector3 vecDir3 = GridUtils.DirToVec3(dir);

        Vector3 pos = cellpos + vecDir3 * offset;

        Gizmos.DrawWireCube(GridUtils.CenterXZ(cell),( Vector3.forward + Vector3.right) * 0.99f);
        Gizmos.DrawLine(GridUtils.CenterXZ(cell), pos);
        Gizmos.DrawWireCube(pos, size);
        Gizmos.DrawWireCube(pos, size * 0.8f);

        if (wire)
        {

            Gizmos.DrawCube(pos, size);
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
        if (lookupTable.TryGetValue(targetcell, out var objects))
        {
            return true; 


        }
        return false;
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
        UpdateLookUpList();
    }

    private void UpdateLookUpList()
    {
        lookupTable.Clear();
        availableKeys.Clear();

        foreach (var item in dataCopy.obstacles)
        {
            if (!lookupTable.TryGetValue(item.cell, out var list))
            {
                list = new List<object>();
                lookupTable[item.cell] = list;
            }
            list.Add(item);
        }

        foreach (var item in dataCopy.messages)
        {
            if (!lookupTable.TryGetValue(item.cell, out var list))
            {
                list = new List<object>();
                lookupTable[item.cell] = list;
            }
            list.Add(item);
        }

        foreach (var item in dataCopy.gates)
        {
            if (!lookupTable.TryGetValue(item.cell, out var list))
            {
                list = new List<object>();
                lookupTable[item.cell] = list;

            }
            availableKeys[item.triggerKey] = item.cell;
            list.Add(item);
        }
        foreach (var item in dataCopy.newArrowTraps)
        {
            if (!lookupTable.TryGetValue(item.cell, out var list))
            {
                list = new List<object>();
                lookupTable[item.cell] = list;

            }
            availableKeys[item.triggerKey] = item.cell;
            list.Add(item);
        }

        foreach (var item in dataCopy.triggerObjects)
        {
            if (!lookupTable.TryGetValue(item.cell, out var list))
            {
                list = new List<object>();
                lookupTable[item.cell] = list;

            }
            if (availableKeys.TryGetValue(item.triggerKeys[0], out Vector2Int linkcell))
            {
                TriggerLink triggerLink = new TriggerLink(item.cell, linkcell);
                links.Add(triggerLink);
            }
            else
            {
                TriggerLink triggerLink = new TriggerLink(item.cell, -Vector2Int.one);
                links.Add(triggerLink);
            }
            list.Add(item);
        }
        UpdateLinks();
    }

    public void UpdateLinks()
    {
        links.Clear();
        foreach (var item in dataCopy.triggerObjects)
        {

            if (availableKeys.TryGetValue(item.triggerKeys[0], out Vector2Int linkcell))
            {
                TriggerLink triggerLink = new TriggerLink(item.cell, linkcell);
                links.Add(triggerLink);
            }
            else
            {
                TriggerLink triggerLink = new TriggerLink(item.cell, -Vector2Int.one);
                links.Add(triggerLink);
            }
        }
    }

    public bool InsideBounds(Vector2Int targetcell)
    {
        return (targetcell.x >= 0 && targetcell.x < dataCopy.width && targetcell.y >= 0 && targetcell.y < dataCopy.height);
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

    public bool SelectCell(Vector2Int cell)
    {
        if(lookupTable.TryGetValue(cell, out var table)){
            if(table.Count > 1)
            {
                Debug.Log("Multiple objects");
                conflictedSelection = table;
                return true;
            }
            else
            {
                SelectObject(table[0]);
                conflictedSelection = null;
                objectIsSelected = true;
                hotCopyModified = true;
                selectedCell = cell;
                return false;
            }
        }

        objectIsSelected = false;
        conflictedSelection = null;
        SelectObject(null);
        selectedCell = new Vector2Int(-1, -1);

        return false;
    }

    public void ResolveConflictedSelection(object obj)
    {
        SelectObject(obj);
        conflictedSelection = null;
        objectIsSelected = true;
        hotCopyModified = true;
    }

    public void SelectObject(object obj)
    {
        switch (obj)
        {
            case null:
                selectedObjectType = SelectedObjectType.None;
                selectedObjectIndex = -1;
                objectIsSelected = false;
                selectedCell = new Vector2Int(-1, -1);
                break;
            case ObstacleData:
                objectIsSelected = true;
                selectedObjectType = SelectedObjectType.Obstacle;
                selectedObjectIndex = dataCopy.obstacles.IndexOf((ObstacleData)obj);
                selectedCell = dataCopy.obstacles[selectedObjectIndex].cell;
                break;
            case MessageConfig:
                objectIsSelected = true;
                selectedObjectType = SelectedObjectType.Message;
                selectedObjectIndex = dataCopy.messages.IndexOf((MessageConfig)obj);
                selectedCell = dataCopy.messages[selectedObjectIndex].cell;
                break;
            case TriggerObjectConfig:
                objectIsSelected = true;
                selectedObjectType = SelectedObjectType.TriggerObject;
                selectedObjectIndex = dataCopy.triggerObjects.IndexOf((TriggerObjectConfig)obj);
                selectedCell = dataCopy.triggerObjects[selectedObjectIndex].cell;
                break;
            case GateConfig:
                objectIsSelected = true;
                selectedObjectType = SelectedObjectType.Gate;
                selectedObjectIndex = dataCopy.gates.IndexOf((GateConfig)obj);
                selectedCell = dataCopy.gates[selectedObjectIndex].cell;
                break;
            case ArrowTrapConfig:
                objectIsSelected = true;
                selectedObjectType = SelectedObjectType.ArrowTrap;
                selectedObjectIndex = dataCopy.newArrowTraps.IndexOf((ArrowTrapConfig)obj);
                selectedCell = dataCopy.newArrowTraps[selectedObjectIndex].cell;
                break;
            default:
                selectedObjectType = SelectedObjectType.None;
                selectedObjectIndex = -1;
                objectIsSelected = false;
                selectedCell = new Vector2Int(-1, -1);
                break;

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
        UpdateLookUpList();
    }


    public void CreateCell(Vector2Int cell)
    {
        ObstacleData temp = new ObstacleData(ObstacleData.ObstacleType.Wall, cell,CardinalDirection.North );
        dataCopy.obstacles.Add(temp);
        ReorderList();
        UpdateLookUpList();
        hotCopyModified = true;
    }

}



