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

        // Ta logique ici 
        if (editor.CellOccuped(gridPos))
        {
            DrawSelector(gridPos, selectColor, true);


            if (e.type == EventType.MouseDown)
            {
                editor.SelectCell(gridPos);
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
