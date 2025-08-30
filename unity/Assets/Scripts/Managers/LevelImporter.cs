using UnityEngine;
using System.Collections.Generic;
using Debug = UnityEngine.Debug;

/// <summary>
/// Manages level importing and organized spawning of all game objects
/// Takes parsed HoudiniLevelData and handles the systematic creation of all elements
/// </summary>
public class LevelImporter : MonoBehaviour
{
    public static LevelImporter Instance { get; private set; }
    
    
    [Header("Debug")]
    public bool logImportDetails = true;
    
    // References  
    private HoudiniLevelParser levelParser;
    
    // Current parsed data
    private HoudiniLevelData currentLevelData;
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        // Get parser reference
        levelParser = HoudiniLevelParser.Instance;
        
        if (levelParser == null)
        {
            levelParser = FindObjectOfType<HoudiniLevelParser>();
        }
    }
    
    /// <summary>
    /// Import level from TextAsset and transfer data to LevelLoader
    /// </summary>
    public HoudiniLevelData ImportLevel(TextAsset levelFile)
    {
        if (levelFile == null)
        {
            Debug.LogError("[LevelImporter] Level file is null!");
            return null;
        }
        
        if (levelParser == null)
        {
            // Try to find existing one first
            levelParser = FindObjectOfType<HoudiniLevelParser>();
            
            // If still not found, create one
            if (levelParser == null)
            {
                GameObject parserGO = new GameObject("HoudiniLevelParser");
                levelParser = parserGO.AddComponent<HoudiniLevelParser>();
                Debug.Log("[LevelImporter] Created HoudiniLevelParser automatically");
            }
        }
        
        Debug.Log($"[LevelImporter] Starting data import of: {levelFile.name}");
        
        // Parse level data and return it
        currentLevelData = levelParser.ParseLevelData(levelFile);
        if (currentLevelData == null)
        {
            Debug.LogError("[LevelImporter] Failed to parse level data!");
            return null;
        }
        
        Debug.Log("[LevelImporter] Level data import completed successfully");
        return currentLevelData;
    }
    
}

