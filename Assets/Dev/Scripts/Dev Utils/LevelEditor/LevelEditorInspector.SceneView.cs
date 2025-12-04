using Sarabande.Core;
using Sarabande.Levels;
using UnityEditor;
using UnityEngine;

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
        if (e.button != 0) return;

        Vector2Int gridPos = GetGridPositionFromMouse(e.mousePosition);

        if (!editor.hotCopyCreated) return;

        if (!editor.InsideBounds(gridPos))
        {
            return;
        }


        switch (editor.currentTool)
        {
            case EditorToolType.Edit: SelectTool(e, gridPos); break;
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
                        () => editor.ResolveConflictedSelection(obstacle)
                    );
                    break;

                case MessageConfig message:
                    menu.AddItem(
                        new GUIContent($"[{i}] Message - {message.text}"),
                        false,
                        () => editor.ResolveConflictedSelection(message)
                    );
                    break;

                case TriggerObjectConfig triggerObject:
                    menu.AddItem(
                        new GUIContent($"[{i}] TriggerObject - {triggerObject.type}"),
                        false,
                        () => editor.ResolveConflictedSelection(triggerObject)
                    );
                    break;
                case ArrowTrapConfig arrowtrap:
                    menu.AddItem(
                        new GUIContent($"[{i}] ArrowTrap - {arrowtrap.triggerKey}"),
                        false,
                        () => editor.ResolveConflictedSelection(arrowtrap)
                    );
                    break;
                case GateConfig gate:
                    menu.AddItem(
                        new GUIContent($"[{i}] TriggerObject - {gate.triggerKey}"),
                        false,
                        () => editor.ResolveConflictedSelection(gate)
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
                    editor.DeleteCell(gridPos);
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
            if (editor.CellOccuped(gridPos))
            {
                DrawSelector(gridPos, placeColor, false);

            }
            else
            {

                DrawSelector(gridPos, placeColor, true);
                if (e.type == EventType.MouseDown)
                {
                    editor.CreateCell(gridPos);
                }
            }
        }

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
