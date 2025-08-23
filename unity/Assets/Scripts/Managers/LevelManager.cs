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
    public string currentLevelName = "LEVEL_0001_v1.0.0_v4.3";
    public bool randomizeLevel = false;
    
    [Header("Level Paths")]
    public string levelsPath = "Assets/Levels/";
    
    private LevelData currentLevelData;
    private LevelLoader levelLoader;
    private List<string> availableLevels;
    
    public event System.Action<LevelData> OnLevelLoaded;
    public event System.Action OnLevelReset;
    
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
            levelLoader = gameObject.AddComponent<LevelLoader>();
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
        
        string fullPath = Path.Combine(Application.dataPath, "Levels");
        if (Directory.Exists(fullPath))
        {
            string[] iniFiles = Directory.GetFiles(fullPath, "*.ini");
            foreach (string file in iniFiles)
            {
                string fileName = Path.GetFileNameWithoutExtension(file);
                availableLevels.Add(fileName);
            }
        }
        
        Debug.Log($"[LevelManager] Found {availableLevels.Count} levels: {string.Join(", ", availableLevels)}");
    }
    
    public void LoadCurrentLevel()
    {
        if (randomizeLevel && availableLevels.Count > 0)
        {
            currentLevelName = availableLevels[Random.Range(0, availableLevels.Count)];
        }
        
        LoadLevel(currentLevelName);
    }
    
    public void LoadLevel(string levelName)
    {
        string levelPath = Path.Combine(Application.dataPath, "Levels", levelName + ".ini");
        
        if (!File.Exists(levelPath))
        {
            Debug.LogError($"[LevelManager] Level file not found: {levelPath}");
            return;
        }
        
        try
        {
            // Try Houdini format first (new system)
            HoudiniLevelData houdiniData = HoudiniLevelImporter.ImportFromFile(levelPath, true);
            
            if (houdiniData != null)
            {
                // Convert HoudiniLevelData to LevelData for compatibility
                currentLevelData = ConvertHoudiniToLevelData(houdiniData);
                currentLevelData.levelName = levelName;
                
                // Note: LevelLoader will handle its own level loading
                // LevelManager just provides query interface for parsed Houdini data
                
                OnLevelLoaded?.Invoke(currentLevelData);
                Debug.Log($"[LevelManager] Houdini level loaded: {levelName} ({currentLevelData.width}x{currentLevelData.height})");
                Debug.Log($"[LevelManager] Generation params - Seed: {houdiniData.houdiniSeed}, Rooms: {houdiniData.roomCount}, Enemy Density: {houdiniData.enemyDensity}");
            }
            else
            {
                // Fallback to old parsing system
                string iniContent = File.ReadAllText(levelPath);
                currentLevelData = ParseINI(iniContent);
                currentLevelData.levelName = levelName;
                
                // LevelLoader will use its existing system for legacy levels
                
                OnLevelLoaded?.Invoke(currentLevelData);
                Debug.Log($"[LevelManager] Legacy level loaded: {levelName} ({currentLevelData.width}x{currentLevelData.height})");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[LevelManager] Failed to load level {levelName}: {e.Message}");
        }
    }
    
    private LevelData ParseINI(string iniContent)
    {
        LevelData levelData = new LevelData();
        levelData.cellTypes = new Dictionary<char, CellType>();
        levelData.enemyPositions = new List<Vector2Int>();
        levelData.collectiblePositions = new List<Vector2Int>();
        
        string[] lines = iniContent.Split('\n');
        string currentSection = "";
        List<string> gridLines = new List<string>();
        
        foreach (string line in lines)
        {
            string trimmedLine = line.Trim();
            
            // Skip comments and empty lines
            if (trimmedLine.StartsWith("#") || string.IsNullOrEmpty(trimmedLine))
                continue;
            
            // Check for section headers
            if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
            {
                currentSection = trimmedLine.Substring(1, trimmedLine.Length - 2);
                continue;
            }
            
            // Parse based on current section
            switch (currentSection)
            {
                case "CELL_TYPES":
                    ParseCellType(trimmedLine, levelData);
                    break;
                    
                case "MAP_DATA":
                    if (trimmedLine.StartsWith("WIDTH="))
                    {
                        levelData.width = int.Parse(trimmedLine.Substring(6));
                    }
                    else if (trimmedLine.StartsWith("HEIGHT="))
                    {
                        levelData.height = int.Parse(trimmedLine.Substring(7));
                    }
                    break;
                    
                case "GRID":
                    gridLines.Add(trimmedLine);
                    break;
            }
        }
        
        // Parse grid data
        if (gridLines.Count > 0)
        {
            ParseGrid(gridLines, levelData);
        }
        
        return levelData;
    }
    
    private void ParseCellType(string line, LevelData levelData)
    {
        // Format: ID=Symbol,Name,Passable,Prefab_Index
        string[] parts = line.Split('=');
        if (parts.Length != 2) return;
        
        string[] values = parts[1].Split(',');
        if (values.Length != 4) return;
        
        CellType cellType = new CellType
        {
            symbol = values[0][0],
            name = values[1],
            passable = bool.Parse(values[2]),
            prefabIndex = int.Parse(values[3])
        };
        
        levelData.cellTypes[cellType.symbol] = cellType;
    }
    
    private void ParseGrid(List<string> gridLines, LevelData levelData)
    {
        if (levelData.width == 0 || levelData.height == 0)
        {
            levelData.height = gridLines.Count;
            levelData.width = gridLines[0].Length;
        }
        
        levelData.grid = new char[levelData.width, levelData.height];
        
        for (int y = 0; y < levelData.height && y < gridLines.Count; y++)
        {
            string line = gridLines[y];
            for (int x = 0; x < levelData.width && x < line.Length; x++)
            {
                char cell = line[x];
                levelData.grid[x, levelData.height - 1 - y] = cell; // Flip Y for Unity coordinates
                
                Vector2Int pos = new Vector2Int(x, levelData.height - 1 - y);
                
                // Track special positions
                switch (cell)
                {
                    case 'P':
                        levelData.playerSpawn = pos;
                        break;
                    case 'E':
                        levelData.enemyPositions.Add(pos);
                        break;
                    case 'C':
                        levelData.collectiblePositions.Add(pos);
                        break;
                    case 'X':
                        levelData.exitPosition = pos;
                        break;
                }
            }
        }
    }
    
    public void ResetLevel()
    {
        if (currentLevelData != null && levelLoader != null)
        {
            // LevelLoader uses TextAsset from inspector, so just reload from file
            levelLoader.LoadLevelFromFile();
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