using Sarabande.Levels;
using System.Linq;
using Unity.VisualScripting;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;
using static Sarabande.Levels.ObstacleData;


[CustomEditor(typeof(LevelEditor))]
public class LevelEditorInspector : Editor
{

    private LevelEditor editor;
    
    private static Color notValidColor = new Color(1f,1f,1f,0.25f);
    private static Color selectColor = new Color(0f,0f,1f,0.25f);

    private static Color placeColor = new Color(0f,1f,0f,0.5f);

    private static Color deleteColor = new Color(1f,0f,0f,0.5f);
    void OnEnable()
    {
        editor = (LevelEditor)target;
    }

    // Pour l'Inspector UI
    public override void OnInspectorGUI()
    {
        //DrawDefaultInspector(); // Affiche les champs normaux
        serializedObject.Update();
        GUI.contentColor = Color.white;

        EditorGUILayout.LabelField("♥ ♥ ♥ GROOVY Level Editor - prototype v0.1 ♥ ♥ ♥", EditorStyles.centeredGreyMiniLabel);

        GUIStyle windowStyle = new GUIStyle(GUI.skin.window);
        windowStyle.padding = new RectOffset(10, 10, 10, 10);
        GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.padding = new RectOffset(10, 10, 10, 10);

        EditorGUILayout.BeginVertical(windowStyle);
        EditorGUILayout.LabelField("File", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();


        SerializedProperty data = serializedObject.FindProperty("levelData");
        SerializedProperty copy = serializedObject.FindProperty("levelDataCopy");




        if (editor.levelData == null)
        {
            if (editor.hotCopyCreated)
            {
                GUI.backgroundColor = Color.gray;
                EditorGUILayout.PropertyField(data, GUIContent.none);

                GUI.backgroundColor = Color.white;

                GUI.enabled = false;
                if (GUILayout.Button("None"))
                {
                    editor.ReImportLevel();

                }

                EditorGUILayout.EndHorizontal();

                
                EditorGUILayout.PropertyField(copy);
                GUI.enabled = true;

                EditorGUILayout.Space();

                EditorGUILayout.BeginVertical(boxStyle);


                GUI.contentColor = Color.red;
                
                EditorGUILayout.LabelField("⚠ Level Data File not Found - You are editing the WORKING COPY" +
                    "\n\n⚠ Relink the file or Save as new file NOW !! \n\n⚠YOUR PROGRESS CAN BE LOST", EditorStyles.wordWrappedLabel);
                GUI.contentColor = Color.white;
                GUI.backgroundColor = Color.white;

                EditorGUILayout.Space();

                EditorGUILayout.BeginHorizontal();
                GUI.backgroundColor = Color.red;
                if (GUILayout.Button("Discard"))
                {
                    editor.DeleteWorkingCopy();

                }

                GUI.backgroundColor = Color.green;
                if (GUILayout.Button("Save as new"))
                {
                    //editor.ReImportLevel();

                }

                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();
            }
            else
            {
                EditorGUILayout.PropertyField(data, GUIContent.none);

                if (GUILayout.Button("New (+)"))
                {
                    editor.ReImportLevel();

                }

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space();

                EditorGUILayout.LabelField("Select a Level Data Asset or press [New] to begin", EditorStyles.helpBox);
            }



        }
        else
        {
            EditorGUILayout.PropertyField(data, GUIContent.none);

            if (!editor.hotCopyCreated)
            {

                if (GUILayout.Button("Import"))
                {
                    editor.ReImportLevel();

                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Click on [Import] to load the level into the workspace", EditorStyles.helpBox);


            }
            else
            {
                GUI.backgroundColor = Color.red;
                if (GUILayout.Button("Unload"))
                {
                    editor.DeleteWorkingCopy();

                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space();
                EditorGUILayout.BeginHorizontal();

                GUI.backgroundColor = Color.white;

                if (editor.hotCopyModified)
                {
                    if (GUILayout.Button("Discard Changes"))
                    {
                        editor.ReImportLevel();

                    }
                }
                else
                {
                    GUI.enabled = false;
                    if (GUILayout.Button("Discard Changes"))
                    {
                        editor.ReImportLevel();

                    }
                    GUI.enabled = true;
                }




                if (GUILayout.Button("Save as"))
                {
                    //editor.ReImportLevel();

                }
                EditorGUILayout.EndHorizontal();



                EditorGUILayout.Space();

                //EditorGUILayout.BeginHorizontal();


                EditorGUILayout.BeginVertical(boxStyle);

                if (editor.hotCopyModified)
                {
                    GUI.contentColor = Color.cyan;

                    EditorGUILayout.LabelField("You have Unsaved Changes - Please Save !", EditorStyles.largeLabel);
                }
                else
                {
                    EditorGUILayout.LabelField("No changes detected", EditorStyles.largeLabel);
                }

                    GUI.contentColor = Color.white;

                EditorGUILayout.EndVertical();

                EditorGUILayout.Space();

                if (editor.hotCopyModified)
                {
                    if (GUILayout.Button("SAVE"))
                    {
                        //editor.ReImportLevel();

                    }
                }
                else
                {
                    GUI.enabled = false;
                    if (GUILayout.Button("Saved"))
                    {
                        //editor.ReImportLevel();

                    }
                    GUI.enabled = true;
                }
                //EditorGUILayout.EndHorizontal();
            }
            
        }
        EditorGUILayout.EndVertical();



        if (editor.hotCopyCreated == true)
        {
            DrawToolBar();
        }

        serializedObject.ApplyModifiedProperties();

    }

    private void DrawToolBar()
    {
        EditorGUILayout.Space();
        GUIStyle windowStyle = new GUIStyle(GUI.skin.window);
        windowStyle.padding = new RectOffset(10, 10, 10, 10);
        EditorGUILayout.BeginVertical(windowStyle);
        // Tes boutons, toolbar, etc.
        EditorGUILayout.LabelField("Tools", EditorStyles.boldLabel);

        // Toolbar avec sélection automatique
        EditorGUILayout.BeginHorizontal();

        Color[] toolColors = {
            new Color(0.5f, 1f, 0.5f),  // Place = vert
            new Color(1f, 0.5f, 0.5f),  // Remove = rouge
            new Color(0.5f, 0.8f, 1f),   // Select = bleu
            new Color(0.5f, 0.5f, 0.5f)   // Select = bleu
        };

        for (int i = 0; i < 4; i++)
        {
            bool isSelected = (int)editor.currentTool == i;

            GUI.backgroundColor = isSelected ? toolColors[i] : Color.white;

            if (GUILayout.Button(((EditorToolType)i).ToString(), GUILayout.Height(30)))
            {
                editor.currentTool = (EditorToolType)i;
            }
        }

        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        switch (editor.currentTool)
        {
            case EditorToolType.Edit: SelectToolInspector(); break;
            case EditorToolType.Misc: MiscInspector(); break;
                //case LevelEditor.ToolType.Remove: RemoveTool(e, gridPos); break;
        }
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space();

    }

    private void MiscInspector()
    {
        SerializedProperty levelDatacop = serializedObject.FindProperty("levelDataCopy");
        GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.padding = new RectOffset(10, 10, 10, 10);

        EditorGUILayout.BeginVertical(boxStyle);

        EditorGUILayout.LabelField("Level Dimensions:", EditorStyles.boldLabel);
        GUI.contentColor = Color.gray;
        EditorGUILayout.LabelField("X => Level's Width || Y => Level's Height", EditorStyles.miniLabel);
        GUI.contentColor = Color.white;
        SerializedProperty leveldim = serializedObject.FindProperty("levelDim");
        SerializedProperty widthLevel = serializedObject.FindProperty("levelDim.x");
        SerializedProperty heightlevel = serializedObject.FindProperty("levelDim.y");


        EditorGUILayout.PropertyField(leveldim, GUIContent.none);

        EditorGUILayout.Space();
        editor.Updatebounds();

        EditorGUILayout.EndVertical();
    }


    private void SelectToolInspector()
    {

        if (editor.objectIsSelected)
        {
            // Récupère via SerializedProperty
            string basePath = editor.selectedObjectType switch
            {
                SelectedObjectType.Obstacle => "obstacles",
                SelectedObjectType.Message => "messageConfigs",

                _ => null
            };

            if (basePath != null)
            {
                SerializedProperty listProp = serializedObject.FindProperty(basePath);
                SerializedProperty itemProp = listProp.GetArrayElementAtIndex(editor.selectedObjectIndex);

                // Dessine les propriétés

                // etc...
                if (itemProp != null)
                {
                    GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
                    boxStyle.padding = new RectOffset(10, 10, 10, 10);
                    EditorGUILayout.BeginVertical(boxStyle);

                    EditorGUILayout.LabelField($"Selected {editor.selectedObjectType.ToString()} :", EditorStyles.boldLabel);
                    // Style avec fond et bordure
                    switch (editor.selectedObjectType)
                    {
                        case SelectedObjectType.Obstacle: InspectObstacle(itemProp); break;
                        case SelectedObjectType.Message: InspectMessage(itemProp); break;
                            
                    }


                    EditorGUILayout.EndVertical();
                }
            }
            

        }
        else
        {
            EditorGUILayout.LabelField("No Object Selected - Click on a object to see its properties", EditorStyles.helpBox);
        }
    }

    private void InspectObstacle(SerializedProperty item)
    {


        EditorGUILayout.Space();

        SerializedProperty cell = item.FindPropertyRelative("cell");
        SerializedProperty type = item.FindPropertyRelative("type");
        SerializedProperty dir = item.FindPropertyRelative("thinWallDirection");

        EditorGUILayout.PropertyField(type, GUIContent.none);

        EditorGUILayout.PropertyField(cell, GUIContent.none);


        EditorGUILayout.BeginHorizontal();
        //EditorGUILayout.PropertyField(dir, GUIContent.none);

        // Boutons direction (supposant que c'est un enum ou int)
        //EditorGUILayout.LabelField("Dir:", GUILayout.Width(30));
        if (type.intValue == (int)ObstacleType.ThinWall)
        {
            string[] dirLabels = { "↑", "→", "↓", "←" };
            int currentDir = dir.intValue; // ou dir.enumValueIndex si c'est un enum

            int newDir = GUILayout.Toolbar(currentDir, dirLabels, GUILayout.Width(120));

            if (newDir != currentDir)
            {
                dir.intValue = newDir; // ou dir.enumValueIndex = newDir
            }

        }

        EditorGUILayout.EndHorizontal();

    }

    private void InspectMessage(SerializedProperty item)
    {
        EditorGUILayout.PropertyField(item, GUIContent.none);
    }

    private void OnSceneGUI()
    {
        Event e = Event.current;

        Vector2Int gridPos = GetGridPositionFromMouse(e.mousePosition);

        if (!editor.hotCopyCreated) return;

        if (!editor.InsideBounds(gridPos))
        {
            //DrawSelector(gridPos, notValidColor);
            return;
        }
        switch (editor.currentTool)
        {
            case EditorToolType.Edit: SelectTool(e,gridPos); break;
            case EditorToolType.Place: PlaceTool(e, gridPos); break;
            case EditorToolType.Erase: RemoveTool(e, gridPos); break;
            case EditorToolType.Misc: break;
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
