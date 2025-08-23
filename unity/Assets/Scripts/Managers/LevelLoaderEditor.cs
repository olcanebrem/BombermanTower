#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Linq;

[CustomEditor(typeof(LevelLoader))]
public class LevelLoaderEditor : Editor
{
    private bool showLevelFiles = true;
    private Vector2 scrollPosition = Vector2.zero;
    
    public override void OnInspectorGUI()
    {
        LevelLoader levelLoader = (LevelLoader)target;
        
        // Default inspector
        DrawDefaultInspector();
        
        GUILayout.Space(10);
        
        // Level Files Section
        showLevelFiles = EditorGUILayout.BeginFoldoutHeaderGroup(showLevelFiles, "Level Files Management");
        
        if (showLevelFiles)
        {
            EditorGUI.indentLevel++;
            
            // Refresh button
            if (GUILayout.Button("Refresh Level Files", GUILayout.Height(30)))
            {
                levelLoader.RefreshLevelFilesInEditor();
            }
            
            GUILayout.Space(5);
            
            // Level files list
            var availableLevels = levelLoader.GetAvailableLevels();
            
            if (availableLevels.Count == 0)
            {
                EditorGUILayout.HelpBox("No level files found. Click 'Refresh Level Files' to scan for .ini files.", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.LabelField($"Found {availableLevels.Count} level files:", EditorStyles.boldLabel);
                
                // Current selection info
                var selectedLevel = levelLoader.GetSelectedLevel();
                if (!string.IsNullOrEmpty(selectedLevel.fileName))
                {
                    EditorGUILayout.LabelField("Current Selection:", EditorStyles.boldLabel);
                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField("File Name:", selectedLevel.fileName);
                    EditorGUILayout.LabelField("Level:", selectedLevel.levelNumber.ToString());
                    EditorGUILayout.LabelField("Version:", selectedLevel.version);
                    EditorGUI.indentLevel--;
                }
                
                GUILayout.Space(10);
                
                // Level selection dropdown
                string[] levelNames = availableLevels.Select(l => 
                    $"Level {l.levelNumber:D3} (v{l.version}) - {l.fileName}").ToArray();
                
                int currentIndex = availableLevels.FindIndex(l => l.fileName == selectedLevel.fileName);
                if (currentIndex == -1) currentIndex = 0;
                
                EditorGUI.BeginChangeCheck();
                int newIndex = EditorGUILayout.Popup("Select Level:", currentIndex, levelNames);
                if (EditorGUI.EndChangeCheck() && newIndex != currentIndex)
                {
                    levelLoader.SelectLevel(newIndex);
                    EditorUtility.SetDirty(levelLoader);
                }
                
                GUILayout.Space(10);
                
                // Level files scroll list
                EditorGUILayout.LabelField("All Available Levels:", EditorStyles.boldLabel);
                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(200));
                
                for (int i = 0; i < availableLevels.Count; i++)
                {
                    var level = availableLevels[i];
                    bool isSelected = level.fileName == selectedLevel.fileName;
                    
                    GUI.backgroundColor = isSelected ? Color.green : Color.white;
                    
                    EditorGUILayout.BeginHorizontal("box");
                    
                    EditorGUILayout.LabelField($"Level {level.levelNumber:D3}", GUILayout.Width(70));
                    EditorGUILayout.LabelField($"v{level.version}", GUILayout.Width(60));
                    EditorGUILayout.LabelField(level.fileName, EditorStyles.miniLabel);
                    
                    if (GUILayout.Button("Select", GUILayout.Width(60)))
                    {
                        levelLoader.SelectLevel(i);
                        EditorUtility.SetDirty(levelLoader);
                    }
                    
                    EditorGUILayout.EndHorizontal();
                    GUI.backgroundColor = Color.white;
                }
                
                EditorGUILayout.EndScrollView();
            }
            
            EditorGUI.indentLevel--;
        }
        
        EditorGUILayout.EndFoldoutHeaderGroup();
        
        // Runtime controls
        if (Application.isPlaying)
        {
            GUILayout.Space(10);
            EditorGUILayout.LabelField("Runtime Controls:", EditorStyles.boldLabel);
            
            if (GUILayout.Button("Reload Current Level", GUILayout.Height(25)))
            {
                levelLoader.LoadSelectedLevel();
            }
        }
        
        // Save changes
        if (GUI.changed)
        {
            EditorUtility.SetDirty(levelLoader);
        }
    }
}
#endif