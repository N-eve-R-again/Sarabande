using UnityEditor;
using UnityEngine;

public partial class LevelEditorInspector
{
    private void DrawNoFile()
    {
        EditorGUILayout.BeginHorizontal();

        SerializedProperty data = serializedObject.FindProperty("levelData");
        EditorGUILayout.PropertyField(data, GUIContent.none);

        if (GUILayout.Button("New (+)"))
        {
            editor.CreateNewFile();
        }

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space();

        CustomStylesGUI.DrawHelpBox("Select a Level Data Asset or press [New] to begin");
    }

    private void DrawLinkBrokenWarning()
    {

        GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.padding = new RectOffset(10, 10, 10, 10);

        SerializedProperty data = serializedObject.FindProperty("levelData");
        SerializedProperty copy = serializedObject.FindProperty("dataCopy");

        EditorGUILayout.BeginHorizontal();
        GUI.backgroundColor = Color.gray;
        EditorGUILayout.PropertyField(data, GUIContent.none);

        GUI.backgroundColor = Color.white;


        CustomButtonsGUI.DrawDisabledButton("None");

        EditorGUILayout.EndHorizontal();

        CustomFieldsGUI.DrawDisabledField(copy);

        EditorGUILayout.Space();

        EditorGUILayout.BeginVertical(boxStyle);


        GUI.contentColor = Color.red;

        EditorGUILayout.LabelField("⚠ Level Data File not Found - You are editing the WORKING COPY" +
            "\n\n⚠ Relink the file or Save as new file NOW !! \n\n⚠YOUR PROGRESS CAN BE LOST", EditorStyles.wordWrappedLabel);
        GUI.contentColor = Color.white;

        CustomStylesGUI.ResetBGColor();

        EditorGUILayout.Space();

        EditorGUILayout.BeginHorizontal();

        GUI.backgroundColor = Color.red;
        if (GUILayout.Button("Discard")) 
            editor.UnloadLevel();


        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("Save as new")) 
            editor.SaveAsNew();


        CustomStylesGUI.ResetBGColor();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    private void DrawFileLoaded()
    {
        GUIStyle boxStyle = StyleLibrary.GetClassicBox();

        SerializedProperty data = serializedObject.FindProperty("levelData");
        SerializedProperty copy = serializedObject.FindProperty("dataCopy");

        EditorGUILayout.BeginHorizontal();

            CustomFieldsGUI.DrawDisabledField(data);

            GUI.backgroundColor = Color.red;
            if (GUILayout.Button("Unload"))
            {
                editor.UnloadLevel();

            }
            GUI.backgroundColor = Color.white;

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        EditorGUILayout.BeginHorizontal();

            if (editor.hotCopyModified)
            {
                if (GUILayout.Button("Discard Changes"))
                {
                    editor.DiscardChanges();
                }
            }
            else
            {
                CustomButtonsGUI.DrawDisabledButton("Discard Changes");
            }


            if (GUILayout.Button("Save as"))
            {
                editor.SaveAsNew();

            }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        EditorGUILayout.BeginVertical(boxStyle);

            if (editor.hotCopyModified)
            {
                GUI.contentColor = Color.cyan;

                CustomStylesGUI.DrawLargeLabel("You have Unsaved Changes - Please Save !");

                CustomStylesGUI.ResetContentColor();
            }
            else
            {
                CustomStylesGUI.DrawLargeLabel("No changes detected");
            }

        EditorGUILayout.EndVertical();

        EditorGUILayout.Space();

        if (editor.hotCopyModified)
        {
            if (GUILayout.Button("SAVE"))
            {
                editor.SaveFile();
            }
        }
        else
        {
            CustomButtonsGUI.DrawDisabledButton("Saved");
        }

    }




    private void DrawImportMessage()
    {
        SerializedProperty data = serializedObject.FindProperty("levelData");
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PropertyField(data, GUIContent.none);

        if (GUILayout.Button("Import")) 
            editor.LoadLevel();

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space();

        CustomStylesGUI.DrawHelpBox("Click on [Import] to load the level into the workspace");

    }


}
