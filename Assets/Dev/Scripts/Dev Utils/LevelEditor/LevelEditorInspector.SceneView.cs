using Sarabande.Core;
using Sarabande.Levels;
using System.Collections.Generic;
using System.Linq;

using UnityEditor;
using UnityEngine;
using Sarabande.Triggerables;
using Sarabande.Listeners;
using Sarabande.Actors;
using Sarabande.Obstacles;


public partial class LevelEditorInspector
{
    private static Color notValidColor = new Color(1f, 1f, 1f, 0.25f);
    private static Color selectColor = new Color(0f, 0f, 1f, 0.25f);

    private static Color placeColor = new Color(0f, 1f, 0f, 0.5f);

    private static Color deleteColor = new Color(1f, 0f, 0f, 0.5f);

    private void OnSceneGUI()
    {
        Event e = Event.current;

        if (editor.dataCopy == null) return;


        if (!editor.hotCopyCreated) return;


        if (editor.displayFilter.HasFlag(DisplayFilter.TriggerLinks))
        {
            DrawTriggerLinks();
        }

        if (e.button != 0) return;

        Vector2Int gridPos = GetGridPositionFromMouse(e.mousePosition);


        if (!editor.InsideBounds(gridPos))
        {
            SceneView.RepaintAll();
            return;
        }



        switch (editor.currentTool)
        {
            case EditorToolType.Edit:
                if (editor.movingSubToolActivated)
                {
                    MoveToTool(e,gridPos);
                }
                else
                {
                    SelectTool(e, gridPos);
                }

                break;
            case EditorToolType.Place: PlaceTool(e, gridPos); break;
            case EditorToolType.Erase: RemoveTool(e, gridPos); break;
            case EditorToolType.Misc: break;
        }

        if (e.type == EventType.MouseDown || e.type == EventType.MouseDrag)
        {
            e.Use();
            SceneView.RepaintAll(); // Force le refresh de la scène
        }

        // Toujours repeindre quand la souris bouge pour voir les previews
        if (e.type == EventType.MouseMove)
        {
            SceneView.RepaintAll();
        }
    }

    private void DrawTriggerLinks()
    {
        Color keyColor = new Color(1f, 0.5f, 0f, 0.95f);
        Color listenerkey = new Color(0f, 0.5f, 1f, 0.95f);
        Color globalkey = new Color(0.5f, 1f, 1f, 0.95f);
        List<Vector2Int> exploredCells = new List<Vector2Int>();

        foreach (var item in editor.links)
        {
            int countA = exploredCells.Count(cell => cell == item.cellA);



            if (item.valid())
            {
                if (item.IsGlobal())
                {
                    DrawTextBubble(item.cellA, 0, $"{item.triggerkey}", globalkey);
                    exploredCells.Add(item.cellA);
                }
                else
                {
                    DrawLine(item.cellA, item.cellB, keyColor, 3);
                    //DrawTextBubble(item.cellA, countA, $"{item.triggerkey}", listenerkey);
                    exploredCells.Add(item.cellA);

                    countA = exploredCells.Count(cell => cell == item.cellA);
                    int countB = exploredCells.Count(cell => cell == item.cellB);

                    if(countB <= 0)
                    {
                        DrawTextBubble(item.cellB, 0, $"{item.triggerkey}", keyColor);
                    }


                }


            }
            else
            {
                DrawTextBubble(item.cellA, countA, $"[{countA}] {item.triggerkey}", Color.red);
            }


            exploredCells.Add(item.cellB);
        }
    }
    private void DrawLine(Vector2Int a, Vector2Int b,Color color, float thickness)
    {
        Handles.color = color;
        Handles.DrawLine(GridUtils.CenterInCell(a),GridUtils.CenterInCell(b), thickness);
        Handles.color = Color.white;
    }
    private void DrawTextBubble(Vector2Int cell,int visibilityOffset, string text, Color bgColor)
    {
        Vector3 worldPos = GridUtils.CenterInCell(cell) ;
        Vector3 offset = Vector3.up *0.5f* visibilityOffset;
        worldPos += offset;

        Handles.color = bgColor;
        //Handles.DrawLine(GridUtils.CenterInCell(cell), worldPos);
        Handles.color = Color.white;
        GUIStyle style = new GUIStyle(GUI.skin.button);
        style.normal.textColor = Color.white;
        style.alignment = TextAnchor.MiddleCenter;
        style.fontSize = 10;

        Handles.BeginGUI();

        Vector2 screenPos = HandleUtility.WorldToGUIPoint(worldPos);
        GUIContent content = new GUIContent(text);
        Vector2 size = style.CalcSize(content);
        Rect rect = new Rect(screenPos.x - size.x / 2, screenPos.y - size.y / 2, size.x + 10, size.y + 5);

        GUI.backgroundColor = bgColor;
        GUI.Box(rect, content, style);
        GUI.backgroundColor = Color.white;

        Handles.EndGUI();

    }

    // Utilisation
    private void MoveToTool(Event e, Vector2Int gridPos)
    {
        DrawSelector(gridPos, selectColor, true);
        DrawLine(editor.selectedCell, gridPos, Color.blue, 0.2f);

        if (e.type == EventType.MouseDown)
        {
            editor.MoveSelectedObjectToCell(gridPos);
            editor.movingSubToolActivated = false;
        }

    }

    private void SelectTool(Event e, Vector2Int gridPos)
    {

        if (editor.objectIsSelected)
        {

            DrawMoveHandles(editor.selectedCell);
        }
        // Ta logique ici 
        if (editor.CellOccuped(gridPos))
        {
            DrawSelector(gridPos, selectColor, true);


            if (e.type == EventType.MouseDown)
            {
                if (editor.SelectCell(gridPos))
                {
                    ShowSelectionMenu();
                }

            }
        }
        else
        {
            //DrawSelector(gridPos, notValidColor);

            if (e.type == EventType.MouseDown)
            {
                editor.SelectCell(gridPos);
            }
        }


    }

    void DrawMoveHandles(Vector2Int cell)
    {
        Vector3 center = GridUtils.CenterXZ(cell) + LevelGlobalSettings.cellSize * Vector3.up * 0.5f;
        float offsetfromCenter = 0.3f;
        float handleSize = 0.15f;

        Handles.color = Color.blue;

        // Flèche haut
        if(editor.InsideBounds(cell + Vector2Int.up))
        {
            if (Handles.Button(center + Vector3.forward * offsetfromCenter, Quaternion.LookRotation(Vector3.forward), handleSize, handleSize, Handles.ConeHandleCap))
                editor.MoveSelectedObject(Vector2Int.up);
        }
        if (editor.InsideBounds(cell + Vector2Int.right))
        {
            // Flèche droite
            if (Handles.Button(center + Vector3.right * offsetfromCenter, Quaternion.LookRotation(Vector3.right), handleSize, handleSize, Handles.ConeHandleCap))
                editor.MoveSelectedObject(Vector2Int.right);
        }
        if (editor.InsideBounds(cell + Vector2Int.down))
        {
            // Flèche bas
            if (Handles.Button(center + Vector3.back * offsetfromCenter, Quaternion.LookRotation(Vector3.back), handleSize, handleSize, Handles.ConeHandleCap))
                editor.MoveSelectedObject(Vector2Int.down);
        }
        if (editor.InsideBounds(cell + Vector2Int.left))
        {
            // Flèche gauche
            if (Handles.Button(center + Vector3.left * offsetfromCenter, Quaternion.LookRotation(Vector3.left), handleSize, handleSize, Handles.ConeHandleCap))
                editor.MoveSelectedObject(Vector2Int.left);
        }






        Handles.color = Color.white;
        // Pareil pour droite, bas, gauche
    }


    void ShowSelectionMenu()
    {
        GenericMenu menu = new GenericMenu();

        int i = 0;
        foreach (var item in editor.conflictedSelection)
        {
            switch (item)
            {
                case ObstacleData obstacle:
                    menu.AddItem(
                        new GUIContent($"[{i}] Obstacle - {obstacle.type}"),
                        false,
                        () => editor.ResolveConflictedMenu(obstacle)
                    );
                    break;

                case MessageConfig message:
                    menu.AddItem(
                        new GUIContent($"[{i}] Message - {message.text}"),
                        false,
                        () => editor.ResolveConflictedMenu(message)
                    );
                    break;

                case TriggerObjectConfig triggerObject:
                    menu.AddItem(
                        new GUIContent($"[{i}] TriggerObject - {triggerObject.type}"),
                        false,
                        () => editor.ResolveConflictedMenu(triggerObject)
                    );
                    break;
                case ArrowTrapConfig arrowtrap:
                    menu.AddItem(
                        new GUIContent($"[{i}] ArrowTrap - {arrowtrap.triggerKey}"),
                        false,
                        () => editor.ResolveConflictedMenu(arrowtrap)
                    );
                    break;
                case GateConfig gate:
                    menu.AddItem(
                        new GUIContent($"[{i}] TriggerObject - {gate.triggerKey}"),
                        false,
                        () => editor.ResolveConflictedMenu(gate)
                    );
                    break;
                case HeroData hero:
                    menu.AddItem(
                    new GUIContent($"[{i}] Hero - Spawn"),
                    false,
                    () => editor.ResolveConflictedMenu(hero)
                    );
                    break;

                default:
                    menu.AddItem(
                        new GUIContent($"[{i}] TriggerObject - {item}"),
                        false,
                        () => editor.ResolveConflictedMenu(item)
                    );
                    break;
            }
            i++;
        }

        menu.ShowAsContext();

    }


    private void RemoveTool(Event e, Vector2Int gridPos)
    {
        if (editor.InsideBounds(gridPos))
        {
            // Ta logique ici 
            if (editor.CellOccuped(gridPos))
            {
                DrawSelector(gridPos, deleteColor, true);

                if (e.type == EventType.MouseDown)
                {
                    if (editor.DeleteCell(gridPos))
                    {
                        ShowSelectionMenu();
                    }
                }

            }
            else
            {
                //DrawSelector(gridPos, notValidColor);
            }
        }

    }

    private void PlaceTool(Event e, Vector2Int gridPos)
    {
        if (editor.InsideBounds(gridPos))
        {
            // Ta logique ici 
            switch (editor.placeObjectType)
            {
                case PlaceObjectType.Obstacle:
                    DrawSelector(gridPos, placeColor, true);
                    DrawPlace(gridPos, editor.obstacleDummy.type.ToString());
                    break;
                case PlaceObjectType.Listener:
                    DrawPlace(gridPos, editor.listenerDummiesNames[editor.selectedPlaceTypeIndex]);
                    DrawSelector(gridPos, placeColor, false);
                    break;
                case PlaceObjectType.Triggerable:
                    DrawPlace(gridPos, editor.triggerableDummiesNames[editor.selectedPlaceTypeIndex]);
                    DrawSelector(gridPos, placeColor, false);
                    break;
                case PlaceObjectType.Actor:
                    break;
                default:
                    break;
            }

            DrawSelector(gridPos, placeColor, false);
            if (e.type == EventType.MouseDown)
            {
                editor.CreateCell(gridPos);
            }
        }

    }

    private void DrawPlace(Vector2Int cell, string name)
    {
        Handles.color = placeColor;
        Vector3 pos = GridUtils.CenterXZ(cell);
        Handles.DrawWireCube(pos, new Vector3(0.8f, 0f, 0.8f));
        DrawTextBubble(cell, 0, name, Color.green);
    }


    Vector3 GetMouseWorldPosition(Vector2 mousePosition)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);
        Plane groundPlane = new Plane(Vector3.up, Vector3.zero); // Plan XZ à Y=0

        if (groundPlane.Raycast(ray, out float distance))
        {
            return ray.GetPoint(distance);
        }

        return Vector3.zero;
    }

    // Et pour directement avoir la position grille
    void DrawSelector(Vector2Int cell, Color color, bool solid = false)
    {
        Handles.color = color;
        Vector3 pos = new Vector3(cell.x * LevelGlobalSettings.cellSize, 0, cell.y * LevelGlobalSettings.cellSize);
        pos += Vector3.one * LevelGlobalSettings.cellSize * 0.5f;


        if (solid)
        {
            Handles.CubeHandleCap(0, pos, Quaternion.identity, 1f, EventType.Repaint);
        }

        Handles.DrawWireCube(pos, Vector3.one);
    }

    Vector2Int GetGridPositionFromMouse(Vector2 mousePosition)
    {
        Vector3 worldPos = GetMouseWorldPosition(mousePosition);

        // Convertir en coordonnées grille
        int x = Mathf.FloorToInt(worldPos.x / LevelGlobalSettings.cellSize);
        int z = Mathf.FloorToInt(worldPos.z / LevelGlobalSettings.cellSize);

        return new Vector2Int(x, z);
    }
}
