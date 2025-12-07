using JetBrains.Annotations;
using Sarabande.Core;
using Sarabande.Levels;

using System.Collections.Generic;
using System.Linq;

using UnityEditor;

using UnityEngine;


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
    Listener,
    Triggerable,
    Actor
}

[System.Flags]
public enum DisplayFilter
{
    None = 0,
    Obstacles = 1 << 0,      // 1
    Listeners = 1 << 1,      // 2
    Triggerables = 1 << 2,   // 4
    TriggerLinks = 1 << 4,   // 16

    Everything = ~0          // Tous les bits à 1
}



public class TriggerLink
{
    public Vector2Int cellA;
    public Vector2Int cellB;
    
    public string triggerkey;
    private bool globalKey;

    public TriggerLink(Vector2Int _cellA, Vector2Int _cellB, string _triggerkey, bool globalKey)
    {
        this.cellA = _cellA;
        this.cellB = _cellB;
        this.triggerkey = _triggerkey;
        this.globalKey = globalKey;
    }

    public bool IsGlobal()
    {
        return globalKey;
    }
    public bool valid()
    {
        return cellB != -Vector2Int.one || globalKey;
    }
}

public class LevelEditor : MonoBehaviour
{
    // Usage
    public DisplayFilter displayFilter = DisplayFilter.Everything;

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

    private string originalJson; // Stocke l'état initial

    Vector3[] bounds;

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

                        DrawObstacle(obstacle, false);
                        break;
                    case ListenerData listener:
                        DrawListener(listener, false);
                        break;
                    case TriggerableData triggerable:
                        DrawTriggerable(triggerable, false);
                        break;
                }
            }
        }

        /*if (displayFilter.HasFlag(DisplayFilter.TriggerLinks))
        {
            foreach (var item in links)
            {
                if (item.valid() && !item.IsGlobal())
                {
                    Gizmos.color = new Color(1, 0.5f, 0);
                    Gizmos.DrawLine(GridUtils.CenterXZ(item.cellA), GridUtils.CenterInCell(item.cellB));
                }
                else
                {
                    Gizmos.color = Color.red;
                    Gizmos.DrawLine(GridUtils.CenterXZ(item.cellA), GridUtils.CenterXZ(item.cellA) + Vector3.up * 0.4f);
                }

            }
        }
        */


        Gizmos.color = Color.white;

        if (objectIsSelected && currentTool == EditorToolType.Edit)
        {
            switch (selectedObjectType)
            {
                case SelectedObjectType.Obstacle:
                    DrawObstacle(dataCopy.obstacles[selectedObjectIndex], true);
                    break;
                case SelectedObjectType.Listener:

                    DrawListener(dataCopy.listeners[selectedObjectIndex], true);

                    break;

                case SelectedObjectType.Triggerable:
                    DrawTriggerable(dataCopy.triggerables[selectedObjectIndex], true);
                    break;
            }
            
        }

    }

    public void ChangeDisplayFlag(DisplayFilter flag)
    {
        displayFilter ^= flag; // Toggle le flag
    }
    private void DrawListener(ListenerData listener, bool selectionGizmo)
    {
        if (!displayFilter.HasFlag(DisplayFilter.Listeners)) return;


        switch (listener)
        {
            case MessageConfig message:

                DrawSpriteIcon("Gizmo_Message", message.cell, Color.yellow, selectionGizmo);
                break;
            case TriggerObjectConfig triggerObject:
                DrawTriggerObject(triggerObject,selectionGizmo);
                break;
            case FakeWallData fakeWallData:
                Gizmos.color = Color.gray;
                DrawCubeAtCell(fakeWallData.cell, true);
                break;
            default:

                break;
        }
    }

    private void DrawArrowTrap(Vector2Int cell, bool selectionGizmo)
    {
        Gizmos.color = Color.red;
        Vector3 size = Vector3.one * LevelGlobalSettings.cellSize * 0.25f;
        Gizmos.DrawWireCube(GridUtils.CenterInCell(cell), size);
        

    }

    private void DrawTriggerable(TriggerableData triggerable, bool selectionGizmo)
    {
        if (!displayFilter.HasFlag(DisplayFilter.Triggerables)) return;

        switch (triggerable)
        {
            case ArrowTrapConfig arrowTrapConfig:
                DrawArrowTrap(arrowTrapConfig.cell, selectionGizmo);
                break;
            case GateConfig gateConfig:
                DrawGate(gateConfig, selectionGizmo);
                break;
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
        if (!displayFilter.HasFlag(DisplayFilter.Obstacles)) return;
        if (selectionGizmo)
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
        Vector3 size = Vector3.one * LevelGlobalSettings.cellSize * 0.99f;
        Vector3 pos = GridUtils.CenterInCell(cell);

        Gizmos.DrawCube(pos, size);
        if (wire)
        {
            Gizmos.DrawWireCube(pos, size);
        }
    }
    public bool CellOccuped(Vector2Int targetcell)
    {
         return lookupTable.TryGetValue(targetcell, out var objects);
    }

    private void ReorderList()
    {
        var sortedObstacles = dataCopy.obstacles
          .OrderByDescending(o => o.cell.y) // Plus loin en premier
          .ThenByDescending(o => o.cell.x);
        dataCopy.obstacles = new List<ObstacleData>(sortedObstacles);

        var sortedListeners = dataCopy.listeners
        .OrderByDescending(o => o.cell.y) // Plus loin en premier
        .ThenByDescending(o => o.cell.x);
        dataCopy.listeners = new List<ListenerData>(sortedListeners);

        var sortedTriggerables = dataCopy.triggerables
            .OrderByDescending(o => o.cell.y) // Plus loin en premier
            .ThenByDescending(o => o.cell.x);
        dataCopy.triggerables = new List<TriggerableData>(sortedTriggerables);
    }

    private void RefreshCopy()
    {
        if(currentTool == EditorToolType.Misc)
        {
            currentTool = EditorToolType.Edit;
        }


        objectIsSelected = false;
        ReorderList();
        Updatebounds();
        UpdateLookUpList();
        UpdateFlags();
    }

    public void Refresh()
    {
        Updatebounds();
        UpdateLookUpList();
    }

    private void AddTriggerableToLookUpTable(TriggerableData item)
    {
        if (!lookupTable.TryGetValue(item.cell, out var list))
        {
            list = new List<object>();
            lookupTable[item.cell] = list;
        }

        availableKeys[item.triggerKey] = item.cell;
        list.Add(item);
    }

    private void AddListenerToLookUpTable(ListenerData item)
    {
        if (!lookupTable.TryGetValue(item.cell, out var list))
        {
            list = new List<object>();
            lookupTable[item.cell] = list;
        }
        list.Add(item);
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

        foreach (var item in dataCopy.triggerables)
        {

            AddTriggerableToLookUpTable(item);

        }

        foreach (var item in dataCopy.listeners)
        {
            AddListenerToLookUpTable(item);
        }

        UpdateLinks();
    }

    public void UpdateLinks()
    {
        links.Clear();
        List<string > global = new List<string>();
        foreach (var item in dataCopy.discoSequencesConfigs)
        {
            global.Add(item.triggerKey);
        }

        foreach (var item in dataCopy.listeners)
        {
            if (item.triggerKeys.Length <= 0) continue;
            foreach (var key in item.triggerKeys)
            {
                if (global.Contains(key))
                {
                    TriggerLink triggerLink = new TriggerLink(item.cell, -Vector2Int.one, key, true);
                    links.Add(triggerLink);
                }
                else
                {
                    if (availableKeys.TryGetValue(key, out Vector2Int linkcell))
                    {
                        TriggerLink triggerLink = new TriggerLink(item.cell, linkcell, key, false);
                        links.Add(triggerLink);
                    }
                    else
                    {
                        TriggerLink triggerLink = new TriggerLink(item.cell, -Vector2Int.one, key, false);
                        links.Add(triggerLink);
                    }
                }


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

    private bool HasChanges()
    {
        return (CreateSnapshot(dataCopy) != originalJson);
    }

    public void UpdateFlags()
    {

        hotCopyCreated = (dataCopy != null);
        hotCopyModified = HasChanges();

    }

    string CreateSnapshot(LevelData data)
    {
        if(data == null) return null;
        var snapshot = new LevelDataSnapshot
        {
            obstacles = data.obstacles,
            listeners = data.listeners,
            triggerables = data.triggerables,
            width = data.width,
            height = data.height,
        };
        return EditorJsonUtility.ToJson(snapshot);
    }

    private void ReImportLevel()
    {
        if (levelData == null) return;

        DeleteWorkingCopy();
        originalJson = CreateSnapshot(levelData);
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

    public void MoveSelectedObject(Vector2Int dir)
    {
        switch (selectedObjectType)
        {
            case SelectedObjectType.Listener:
                selectedCell = dataCopy.listeners[selectedObjectIndex].cell += dir; break;
            case SelectedObjectType.Triggerable:
                selectedCell = dataCopy.triggerables[selectedObjectIndex].cell += dir; break;
            case SelectedObjectType.Obstacle:
                selectedCell = dataCopy.obstacles[selectedObjectIndex].cell += dir; break;
        }
        UpdateLookUpList();
    }

    public bool SelectCell(Vector2Int cell)
    {
        Undo.RecordObject(this, $"Select {cell}");
        if (lookupTable.TryGetValue(cell, out var table)){
            if(table.Count > 1)
            {
                //Debug.Log("Multiple objects");
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

    public void ResolveConflictedMenu(object obj)
    {
        switch (currentTool)
        {
            case EditorToolType.Erase:
                ResolveConflictedDelete(obj); break;
            case EditorToolType.Edit:
                ResolveConflictedSelection(obj); break;
        }

    }
    private void ResolveConflictedSelection(object obj) {
        conflictedSelection = null;
        objectIsSelected = true;
        hotCopyModified = true;
        SelectObject(obj);

    }
    private void ResolveConflictedDelete(object obj)
    {

        SelectObject(null); //failsafe au cas ou on detruit un objet selectionné
        DeleteObject(obj);
        conflictedSelection = null;
    }

    public void SelectObject(object obj)
    {
        Undo.RecordObject(dataCopy, $"Inspect {obj}");
        switch (obj)
        {
            case null:
                selectedObjectType = SelectedObjectType.None;
                selectedObjectIndex = -1;
                objectIsSelected = false;
                selectedCell = new Vector2Int(-1, -1);
                break;
            case ListenerData:

                objectIsSelected = true;
                selectedObjectType = SelectedObjectType.Listener;
                selectedObjectIndex = dataCopy.listeners.IndexOf((ListenerData)obj);
                break;
            case ObstacleData:
                objectIsSelected = true;
                selectedObjectType = SelectedObjectType.Obstacle;
                selectedObjectIndex = dataCopy.obstacles.IndexOf((ObstacleData)obj);
                selectedCell = dataCopy.obstacles[selectedObjectIndex].cell;
                break;
            case TriggerableData:
                selectedObjectType = SelectedObjectType.Triggerable;
                selectedObjectIndex = dataCopy.triggerables.IndexOf((TriggerableData)obj);
                selectedCell = dataCopy.triggerables[selectedObjectIndex].cell;
                break;

            default:
                selectedObjectType = SelectedObjectType.None;
                selectedObjectIndex = -1;
                objectIsSelected = false;
                selectedCell = new Vector2Int(-1, -1);
                break;

        }
        EditorUtility.SetDirty(dataCopy);
        UpdateFlags();

    }


    public bool DeleteCell(Vector2Int cell)
    {
        if (lookupTable.TryGetValue(cell, out var table))
        {
            if(table.Count > 1)
            {
                conflictedSelection = table;
                return true;
            }
            DeleteObject(table[0]);
        
        }
        return false;



    }

    private void DeleteObject(object obj)
    {
        Undo.RecordObject(dataCopy, "Delete Obj");
        EditorUtility.SetDirty(dataCopy);

        bool confirm0 = EditorUtility.DisplayDialog(
            $"Delete {obj}",
            "You are going to delete this object, are you sure?",
            "Delete",
            "Cancel"
        );

        if (!confirm0) return; // Annule l'action

        switch (obj)
        {
            case null: 
                return;

            case TriggerableData triggerable: 
                dataCopy.triggerables.Remove(triggerable);
                break;

            case ListenerData listener:

                dataCopy.listeners.Remove(listener);
                break;

            case ObstacleData obstacle:
                dataCopy.obstacles.Remove(obstacle);
                break;

        }


        ReorderList();
        UpdateLookUpList();
        EditorUtility.SetDirty(dataCopy);
        UpdateFlags();

    }

    public void CreateCell(Vector2Int cell)
    {
        ObstacleData temp = new ObstacleData(ObstacleData.ObstacleType.Wall, cell,CardinalDirection.North );
        dataCopy.obstacles.Add(temp);
        ReorderList();
        UpdateLookUpList();

        EditorUtility.SetDirty(dataCopy);
        UpdateFlags();
    }

}

[System.Serializable]
public class LevelDataSnapshot
{
    public List<ObstacleData> obstacles;
    [SerializeReference] public List<ListenerData> listeners;
    [SerializeReference] public List<TriggerableData> triggerables;
    [SerializeReference] public List<DiscoSequenceConfig> discoSequences;
    public int width;
    public int height;


    // ... seulement tes données de gameplay
}


