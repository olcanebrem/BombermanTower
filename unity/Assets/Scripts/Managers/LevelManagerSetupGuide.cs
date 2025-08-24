using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Setup guide for LevelManager + LevelLoader combination
/// This component helps configure the required components properly
/// </summary>
[System.Serializable]
public class LevelManagerSetupGuide : MonoBehaviour
{
    [Header("Setup Guide")]
    [TextArea(6, 10)]
    public string setupInstructions = 
        "LEVEL MANAGER SETUP GUIDE:\n\n" +
        "1. This GameObject needs both LevelManager AND LevelLoader components\n" +
        "2. Configure LevelLoader:\n" +
        "   - Assign Player Prefab\n" +
        "   - Configure Tile Prefabs array (Empty, Wall, Floor, Enemy, etc.)\n" +
        "   - Assign Sprite Database\n" +
        "3. LevelManager will automatically detect available levels\n" +
        "4. Enable 'Auto Select First Level' for automatic level selection\n\n" +
        "REQUIRED COMPONENTS ON THIS GAMEOBJECT:\n" +
        "✓ LevelManager\n" +
        "✓ LevelLoader\n" +
        "✓ HoudiniLevelImporter (auto-added)\n\n" +
        "Click 'Setup Components' to automatically add missing components.";
    
    [Header("Quick Setup")]
    public bool showAdvancedSettings = false;
    
    private void OnValidate()
    {
        CheckComponents();
    }
    
    private void CheckComponents()
    {
        var levelManager = GetComponent<LevelManager>();
        var levelLoader = GetComponent<LevelLoader>();
        
        if (levelManager == null)
        {
            setupInstructions = "❌ MISSING: LevelManager component required!";
        }
        else if (levelLoader == null)
        {
            setupInstructions = "❌ MISSING: LevelLoader component required!";
        }
        else
        {
            // Check LevelLoader configuration
            string status = "✅ Components found! Configuration status:\n\n";
            
            if (levelLoader.playerPrefab == null)
                status += "❌ Player Prefab not assigned\n";
            else
                status += "✅ Player Prefab assigned\n";
                
            if (levelLoader.tilePrefabs == null || levelLoader.tilePrefabs.Length == 0)
                status += "❌ Tile Prefabs array empty\n";
            else
                status += $"✅ Tile Prefabs: {levelLoader.tilePrefabs.Length} entries\n";
                
            if (levelLoader.spriteDatabase == null)
                status += "❌ Sprite Database not assigned\n";
            else
                status += "✅ Sprite Database assigned\n";
            
            var availableLevels = levelManager.GetAvailableLevels();
            status += $"📁 Available Levels: {availableLevels.Count}\n";
            
            if (availableLevels.Count > 0)
            {
                status += $"📂 Current Level: {levelManager.GetCurrentLevelName()}\n";
            }
            
            setupInstructions = status;
        }
    }
    
    [ContextMenu("Setup Components")]
    public void SetupComponents()
    {
        // Add LevelManager if missing
        if (GetComponent<LevelManager>() == null)
        {
            gameObject.AddComponent<LevelManager>();
            Debug.Log("[LevelManagerSetupGuide] Added LevelManager component");
        }
        
        // Add LevelLoader if missing
        if (GetComponent<LevelLoader>() == null)
        {
            gameObject.AddComponent<LevelLoader>();
            Debug.Log("[LevelManagerSetupGuide] Added LevelLoader component");
        }
        
        // Refresh validation
        CheckComponents();
        
        Debug.Log("[LevelManagerSetupGuide] Component setup completed. Please configure LevelLoader in Inspector.");
    }
    
    [ContextMenu("Remove Setup Guide")]
    public void RemoveSetupGuide()
    {
        Debug.Log("[LevelManagerSetupGuide] Setup guide removed. System is ready!");
        DestroyImmediate(this);
    }
    
    private void Start()
    {
        // Auto-remove after successful setup (optional)
        var levelManager = GetComponent<LevelManager>();
        var levelLoader = GetComponent<LevelLoader>();
        
        if (levelManager != null && levelLoader != null && 
            levelLoader.playerPrefab != null && 
            levelLoader.tilePrefabs != null && levelLoader.tilePrefabs.Length > 0 &&
            levelLoader.spriteDatabase != null)
        {
            Debug.Log("[LevelManagerSetupGuide] All components configured successfully! Setup guide can be removed.");
        }
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(LevelManagerSetupGuide))]
public class LevelManagerSetupGuideEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var guide = (LevelManagerSetupGuide)target;
        
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(guide.setupInstructions, MessageType.Info);
        EditorGUILayout.Space();
        
        if (GUILayout.Button("Setup Required Components", GUILayout.Height(30)))
        {
            guide.SetupComponents();
        }
        
        EditorGUILayout.Space();
        
        if (guide.showAdvancedSettings)
        {
            DrawDefaultInspector();
        }
        
        guide.showAdvancedSettings = EditorGUILayout.Foldout(guide.showAdvancedSettings, "Advanced Settings");
        
        EditorGUILayout.Space();
        
        if (GUILayout.Button("Remove Setup Guide (When Complete)", GUILayout.Height(25)))
        {
            if (EditorUtility.DisplayDialog("Remove Setup Guide", 
                "Are you sure you want to remove the setup guide? Make sure all components are properly configured.", 
                "Remove", "Cancel"))
            {
                guide.RemoveSetupGuide();
            }
        }
    }
}
#endif