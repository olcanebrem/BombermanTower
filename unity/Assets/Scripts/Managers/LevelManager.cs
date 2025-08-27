using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;

[System.Serializable]
public class LevelData
{
    public string levelName;
    public string version;
    public int width;
    public int height;
    public Dictionary<char, CellType> cellTypes;
    public char[,] grid;
    public Vector2Int playerSpawn;
    public Vector2Int exitPosition;
    public List<Vector2Int> enemyPositions;
    public List<Vector2Int> collectiblePositions;
}

[System.Serializable]
public class CellType
{
    public char symbol;
    public string name;
    public bool passable;
    public int prefabIndex;
}

public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance { get; private set; }
    
    [Header("Level Settings")]
    [SerializeField] private string defaultLevelName = ""; // Auto-detected first level
    public bool randomizeLevel = false;
    public bool autoSelectFirstLevel = true;
    
    [Header("Level Paths")]
    public string levelsPath = "Assets/Levels/";
    
    private LevelData currentLevelData;
    private LevelLoader levelLoader;
    private List<string> availableLevels;
    private string currentLevelName; // Now private, auto-managed
    
    public event System.Action<LevelData> OnLevelLoaded;
    public event System.Action OnLevelReset;
    
    // RL Training Integration - Write Only
    public bool enableRLTrainingLogging = true;
    
    private void Awake()
    {
        // Singleton pattern
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
        
        levelLoader = GetComponent<LevelLoader>();
        if (levelLoader == null)
        {
            Debug.LogError("[LevelManager] LevelLoader component is required on the same GameObject! " +
                          "Please add LevelLoader component manually to this GameObject and configure it properly.");
            return;
        }
        
        ScanAvailableLevels();
    }
    
    private void Start()
    {
        LoadCurrentLevel();
    }
    
    private void ScanAvailableLevels()
    {
        availableLevels = new List<string>();
        
        // Use LevelLoader's level scanning system
        if (levelLoader != null)
        {
            try
            {
                levelLoader.ScanForLevelFiles();
                var levelEntries = levelLoader.GetAvailableLevels();
                
                if (levelEntries != null)
                {
                    foreach (var entry in levelEntries)
                    {
                        if (!string.IsNullOrEmpty(entry.fileName))
                        {
                            availableLevels.Add(entry.fileName);
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[LevelManager] Error scanning levels: {e.Message}");
            }
            
            Debug.Log($"[LevelManager] Found {availableLevels.Count} levels via LevelLoader: {string.Join(", ", availableLevels)}");
            
            // Auto-select first level if no default set
            if (autoSelectFirstLevel && availableLevels.Count > 0)
            {
                if (string.IsNullOrEmpty(defaultLevelName) || !availableLevels.Contains(defaultLevelName))
                {
                    defaultLevelName = availableLevels[0];
                    Debug.Log($"[LevelManager] Auto-selected first level: {defaultLevelName}");
                }
                currentLevelName = defaultLevelName;
            }
            
            UpdateInspectorStatus();
        }
        else
        {
            Debug.LogWarning("[LevelManager] LevelLoader not available for scanning levels");
        }
    }
    
    public void LoadCurrentLevel()
    {
        Debug.Log($"[LevelManager] LoadCurrentLevel - randomizeLevel: {randomizeLevel}, currentLevelName: '{currentLevelName}', availableLevels: [{string.Join(", ", availableLevels)}]");
        
        if (randomizeLevel && availableLevels.Count > 0)
        {
            currentLevelName = availableLevels[Random.Range(0, availableLevels.Count)];
            Debug.Log($"[LevelManager] Randomized level selected: {currentLevelName}");
        }
        else if (string.IsNullOrEmpty(currentLevelName) && availableLevels.Count > 0)
        {
            // Fallback: use first available level
            currentLevelName = availableLevels[0];
            Debug.Log($"[LevelManager] No level set, using first available: {currentLevelName}");
        }
        
        if (!string.IsNullOrEmpty(currentLevelName))
        {
            Debug.Log($"[LevelManager] Loading level: {currentLevelName}");
            LoadLevel(currentLevelName);
        }
        else
        {
            Debug.LogError("[LevelManager] No level to load! Make sure level files exist in Assets/Levels/");
        }
    }
    
    public void LoadLevel(string levelName)
    {
        Debug.Log($"[LevelManager] LoadLevel called with: '{levelName}'");
        
        if (levelLoader == null)
        {
            Debug.LogError("[LevelManager] LevelLoader not available!");
            return;
        }
        
        try
        {
            // Find level by name and select it
            var levelEntries = levelLoader.GetAvailableLevels();
            Debug.Log($"[LevelManager] Available level entries: [{string.Join(", ", levelEntries.Select(e => e.fileName))}]");
            
            int levelIndex = levelEntries.FindIndex(entry => entry.fileName == levelName);
            Debug.Log($"[LevelManager] Level '{levelName}' found at index: {levelIndex}");
            
            if (levelIndex >= 0)
            {
                // Select and load via LevelLoader
                Debug.Log($"[LevelManager] Selecting level index {levelIndex}");
                levelLoader.SelectLevel(levelIndex);
                Debug.Log($"[LevelManager] Loading selected level");
                levelLoader.LoadSelectedLevel();
                
                // Get the HoudiniLevelData for LevelManager's interface
                HoudiniLevelData houdiniData = levelLoader.GetCurrentLevelData();
                if (houdiniData != null)
                {
                    // Update current level name
                    currentLevelName = levelName;
                    
                    // Convert HoudiniLevelData to LevelData for compatibility
                    currentLevelData = ConvertHoudiniToLevelData(houdiniData);
                    currentLevelData.levelName = levelName;
                    
                    OnLevelLoaded?.Invoke(currentLevelData);
                    UpdateInspectorStatus();
                    Debug.Log($"[LevelManager] Level loaded via LevelLoader: {levelName} ({currentLevelData.width}x{currentLevelData.height})");
                    Debug.Log($"[LevelManager] Generation params - Seed: {houdiniData.houdiniSeed}, Rooms: {houdiniData.roomCount}, Enemy Density: {houdiniData.enemyDensity}");
                }
                else
                {
                    Debug.LogError($"[LevelManager] Failed to get HoudiniLevelData from LevelLoader for {levelName}");
                }
            }
            else
            {
                Debug.LogError($"[LevelManager] Level not found in LevelLoader: {levelName}");
                Debug.Log($"[LevelManager] Available levels: {string.Join(", ", levelEntries.Select(e => e.fileName))}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[LevelManager] Failed to load level {levelName}: {e.Message}");
        }
    }
    
    // Legacy parsing methods removed - now using LevelLoader/HoudiniLevelImporter system
    
    public void ResetLevel()
    {
        if (currentLevelData != null && levelLoader != null)
        {
            // Reload current level via LevelLoader
            levelLoader.LoadSelectedLevel();
            OnLevelReset?.Invoke();
            Debug.Log($"[LevelManager] Level reset: {currentLevelData.levelName}");
        }
    }
    
    public void LoadNextLevel()
    {
        if (availableLevels.Count == 0) return;
        
        int currentIndex = availableLevels.IndexOf(currentLevelName);
        int nextIndex = (currentIndex + 1) % availableLevels.Count;
        
        LoadLevel(availableLevels[nextIndex]);
    }
    
    public void LoadRandomLevel()
    {
        if (availableLevels.Count == 0) return;
        
        string randomLevel = availableLevels[Random.Range(0, availableLevels.Count)];
        LoadLevel(randomLevel);
    }
    
    // Public getters
    public LevelData GetCurrentLevelData() => currentLevelData;
    public List<string> GetAvailableLevels() => new List<string>(availableLevels);
    public string GetCurrentLevelName() => currentLevelName;
    
    // Inspector display (read-only)
    [Header("Current Status")]
    [SerializeField, TextArea(2, 4)] private string inspectorStatus = "Loading...";
    
    private void UpdateInspectorStatus()
    {
        inspectorStatus = $"Current Level: {currentLevelName ?? "None"}\n" +
                         $"Available Levels: {availableLevels?.Count ?? 0}\n" +
                         $"Auto Select First: {autoSelectFirstLevel}\n" +
                         $"Randomize: {randomizeLevel}";
    }
    
    // RL Training Integration Methods
    public void SaveRLTrainingResults(RLTrainingData trainingData)
    {
        if (enableRLTrainingLogging && LevelTrainingManager.Instance != null)
        {
            string levelFileName = GetCurrentLevelFileName();
            if (!string.IsNullOrEmpty(levelFileName))
            {
                LevelTrainingManager.Instance.SaveTrainingData(levelFileName, trainingData);
                Debug.Log($"[LevelManager] Saved RL training data for {levelFileName}");
            }
        }
    }
    
    public string GetCurrentLevelFileName()
    {
        if (!string.IsNullOrEmpty(currentLevelName))
        {
            // Check if it already has .txt extension
            if (currentLevelName.EndsWith(".txt"))
                return currentLevelName;
            else
                return currentLevelName + ".txt";
        }
        return null;
    }
    
    // Helper methods
    public Vector2Int WorldToGrid(Vector3 worldPosition)
    {
        return new Vector2Int(
            Mathf.FloorToInt(worldPosition.x + 0.5f),
            Mathf.FloorToInt(worldPosition.y + 0.5f)
        );
    }
    
    public Vector3 GridToWorld(Vector2Int gridPosition)
    {
        return new Vector3(gridPosition.x, gridPosition.y, 0);
    }
    
    public bool IsValidGridPosition(Vector2Int gridPos)
    {
        return gridPos.x >= 0 && gridPos.x < currentLevelData.width &&
               gridPos.y >= 0 && gridPos.y < currentLevelData.height;
    }
    
    public char GetCellAtGrid(Vector2Int gridPos)
    {
        if (!IsValidGridPosition(gridPos)) return '#'; // Return wall for out of bounds
        return currentLevelData.grid[gridPos.x, gridPos.y];
    }
    
    public bool IsCellPassable(Vector2Int gridPos)
    {
        char cell = GetCellAtGrid(gridPos);
        if (currentLevelData.cellTypes.ContainsKey(cell))
        {
            return currentLevelData.cellTypes[cell].passable;
        }
        return false; // Unknown cells are impassable
    }
    
    // Convert HoudiniLevelData to legacy LevelData format
    private LevelData ConvertHoudiniToLevelData(HoudiniLevelData houdiniData)
    {
        LevelData levelData = new LevelData();
        
        // Basic info
        levelData.levelName = houdiniData.levelName;
        levelData.version = houdiniData.version;
        levelData.width = houdiniData.gridWidth;
        levelData.height = houdiniData.gridHeight;
        
        // Convert cell types
        levelData.cellTypes = new Dictionary<char, CellType>();
        foreach (var kvp in houdiniData.cellTypes)
        {
            HoudiniCellType houdiniCell = kvp.Value;
            CellType cellType = new CellType
            {
                symbol = houdiniCell.symbol,
                name = houdiniCell.name,
                passable = houdiniCell.passable,
                prefabIndex = houdiniCell.prefabIndex
            };
            levelData.cellTypes[houdiniCell.symbol] = cellType;
        }
        
        // Copy grid
        levelData.grid = new char[houdiniData.gridWidth, houdiniData.gridHeight];
        for (int x = 0; x < houdiniData.gridWidth; x++)
        {
            for (int y = 0; y < houdiniData.gridHeight; y++)
            {
                levelData.grid[x, y] = houdiniData.grid[x, y];
            }
        }
        
        // Set positions
        levelData.playerSpawn = houdiniData.playerSpawn;
        levelData.exitPosition = houdiniData.exitPosition;
        
        // Combine all enemy types
        levelData.enemyPositions = new List<Vector2Int>();
        levelData.enemyPositions.AddRange(houdiniData.enemyPositions);
        levelData.enemyPositions.AddRange(houdiniData.enemyShooterPositions);
        
        // Combine all collectible types
        levelData.collectiblePositions = new List<Vector2Int>();
        levelData.collectiblePositions.AddRange(houdiniData.coinPositions);
        levelData.collectiblePositions.AddRange(houdiniData.healthPositions);
        
        Debug.Log($"[LevelManager] Converted Houdini data: {levelData.enemyPositions.Count} enemies, {levelData.collectiblePositions.Count} collectibles");
        
        return levelData;
    }
    
}