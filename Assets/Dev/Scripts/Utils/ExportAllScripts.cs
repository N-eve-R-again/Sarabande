// Assets/Editor/ExportAllScripts.cs
// Unity Editor script — export all .cs files from a chosen folder (recursive) into a single .txt.

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Text;

public static class ExportAllScripts
{
    [MenuItem("Tools/Export/Export C# Scripts to .txt")]
    public static void ExportScriptsToTxt()
    {
        try
        {
            // 1) Ask for the source folder (inside or outside Assets both work)
            string startDir = Application.dataPath;
            string sourceFolder = EditorUtility.OpenFolderPanel(
                "Select folder containing C# scripts",
                startDir,
                ""
            );

            if (string.IsNullOrEmpty(sourceFolder))
                return; // user canceled

            // 2) Ask for the output .txt file
            string defaultName = "AllScripts_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".txt";
            string savePath = EditorUtility.SaveFilePanel(
                "Save merged scripts as .txt",
                sourceFolder,
                defaultName,
                "txt"
            );

            if (string.IsNullOrEmpty(savePath))
                return; // user canceled

            // 3) Gather files
            string[] csFiles = Directory.GetFiles(sourceFolder, "*.cs", SearchOption.AllDirectories);
            if (csFiles.Length == 0)
            {
                EditorUtility.DisplayDialog("Export C# Scripts", "Aucun fichier .cs trouvé dans ce dossier.", "OK");
                return;
            }

            // 4) Build output
            var sb = new StringBuilder(1024 * 64);
            sb.AppendLine("==== Unity C# Scripts Export ====");
            sb.AppendLine("Date: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("Source Folder: " + sourceFolder);
            sb.AppendLine("Files Found: " + csFiles.Length);
            sb.AppendLine(new string('=', 80));
            sb.AppendLine();

            // For nice relative paths when under the project
            string projectRoot = Directory.GetParent(Application.dataPath).FullName.Replace('\\', '/');

            for (int i = 0; i < csFiles.Length; i++)
            {
                string file = csFiles[i];
                EditorUtility.DisplayProgressBar("Exporting C# Scripts", file, (float)i / csFiles.Length);

                string normalizedPath = file.Replace('\\', '/');
                string relPath = normalizedPath.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase)
                    ? normalizedPath.Substring(projectRoot.Length + 1)
                    : normalizedPath;

                string content;
                try
                {
                    // Read as UTF-8 (with BOM or without)
                    content = File.ReadAllText(file, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                }
                catch (Exception readEx)
                {
                    content = $"// [ERROR READING FILE]\n// {readEx.GetType().Name}: {readEx.Message}\n";
                }

                // Normalize line endings to \n for consistency
                content = content.Replace("\r\n", "\n").Replace("\r", "\n");

                // Write header + content
                sb.AppendLine();
                sb.AppendLine(new string('-', 80));
                sb.AppendLine($"// FILE: {relPath}");
                sb.AppendLine($"// SIZE: {new FileInfo(file).Length} bytes");
                sb.AppendLine(new string('-', 80));
                sb.AppendLine(content.TrimEnd()); // avoid extra newlines
                sb.AppendLine();
            }

            EditorUtility.ClearProgressBar();

            // 5) Save the merged .txt (UTF-8)
            File.WriteAllText(savePath, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            // 6) Reveal result
            EditorUtility.RevealInFinder(savePath);
            EditorUtility.DisplayDialog("Export C# Scripts", $"Export terminé.\nFichier :\n{savePath}", "OK");
        }
        catch (Exception ex)
        {
            EditorUtility.ClearProgressBar();
            Debug.LogError("[ExportAllScripts] " + ex);
            EditorUtility.DisplayDialog("Export C# Scripts - Erreur", ex.Message, "OK");
        }
    }
}
#endif
