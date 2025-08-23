using UnityEngine;
using System.Collections.Generic;
using System.Globalization;
using System;
using System.Linq;

[System.Serializable]
public class HoudiniLevelData
{
    [Header("Level Information")]
    public string levelName;
    public string levelId;
    public string version;
    public string formatVersion;
    public int gridWidth;
    public int gridHeight;
    
    [Header("Generation Parameters")]
    public int houdiniSeed;
    public int roomCount;
    public float enemyDensity;
    public float lootDensity;
    public float coinDensity;
    public float healthDensity;
    public float breakableDensity;
    public float edgeWallBias;
    public float noiseScale;
    public float noiseThreshold;
    public int minRoomSize;
    public int maxRoomSize;
    public float minPlayerExitDist;
    
    [Header("Grid Data")]
    public Dictionary<int, HoudiniCellType> cellTypes;
    public char[,] grid;
    
    [Header("Parsed Positions")]
    public Vector2Int playerSpawn;
    public Vector2Int exitPosition;
    public List<Vector2Int> enemyPositions;
    public List<Vector2Int> enemyShooterPositions;
    public List<Vector2Int> coinPositions;
    public List<Vector2Int> healthPositions;
    public List<Vector2Int> breakablePositions;
    
    public HoudiniLevelData()
    {
        cellTypes = new Dictionary<int, HoudiniCellType>();
        enemyPositions = new List<Vector2Int>();
        enemyShooterPositions = new List<Vector2Int>();
        coinPositions = new List<Vector2Int>();
        healthPositions = new List<Vector2Int>();
        breakablePositions = new List<Vector2Int>();
    }
}

[System.Serializable]
public class HoudiniCellType
{
    public int id;
    public char symbol;
    public string name;
    public bool passable;
    public int prefabIndex;
    
    public HoudiniCellType(int id, char symbol, string name, bool passable, int prefabIndex)
    {
        this.id = id;
        this.symbol = symbol;
        this.name = name;
        this.passable = passable;
        this.prefabIndex = prefabIndex;
    }
}

public class HoudiniLevelImporter : MonoBehaviour
{
    [Header("Debug")]
    public bool logParsingDetails = false;
    
    /// <summary>
    /// TextAsset'ten HoudiniLevelData oluşturur (Component method)
    /// </summary>
    public HoudiniLevelData LoadLevelData(TextAsset levelFile)
    {
        if (levelFile == null)
        {
            Debug.LogError("[HoudiniLevelImporter] Level file is null!");
            return null;
        }
        
        Debug.Log($"[HoudiniLevelImporter] Parsing level file: {levelFile.name}");
        return ImportFromText(levelFile.text, true); // Force logging ON for debugging
    }
    
    /// <summary>
    /// INI formatındaki string'i parse ederek HoudiniLevelData'ya dönüştürür
    /// </summary>
    public static HoudiniLevelData ImportFromText(string iniContent, bool enableLogging = false)
    {
        HoudiniLevelData levelData = new HoudiniLevelData();
        
        try
        {
            string[] lines = iniContent.Split('\n');
            string currentSection = "";
            List<string> gridLines = new List<string>();
            
            if (enableLogging)
                Debug.Log("[HoudiniImporter] Starting INI parsing...");
            
            foreach (string line in lines)
            {
                string trimmedLine = line.Trim().Replace("\r", "");
                
                // Skip empty lines (only outside GRID_ASCII section)
                if (currentSection != "GRID_ASCII" && string.IsNullOrEmpty(trimmedLine))
                    continue;
                
                // Check for section headers
                if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                {
                    currentSection = trimmedLine.Substring(1, trimmedLine.Length - 2);
                    if (enableLogging)
                        Debug.Log($"[HoudiniImporter] Entering section: {currentSection}");
                    continue;
                }
                
                // Parse based on current section
                switch (currentSection)
                {
                    case "CELL_TYPES":
                        ParseCellType(trimmedLine, levelData, enableLogging);
                        break;
                        
                    case "LEVEL_CONFIG":
                        ParseLevelConfig(trimmedLine, levelData, enableLogging);
                        break;
                        
                    case "GENERATION_PARAMS":
                        ParseGenerationParams(trimmedLine, levelData, enableLogging);
                        break;
                        
                    case "GRID_ASCII":
                        if (enableLogging)
                            Debug.Log($"[HoudiniImporter] GRID_ASCII line: '{trimmedLine}' (length: {trimmedLine.Length})");
                        gridLines.Add(trimmedLine);
                        break;
                }
            }
            
            // Parse grid data and extract positions
            if (enableLogging)
                Debug.Log($"[HoudiniImporter] Collected {gridLines.Count} grid lines");
                
            if (gridLines.Count > 0)
            {
                ParseGrid(gridLines, levelData, enableLogging);
                ExtractSpecialPositions(levelData, enableLogging);
            }
            else if (enableLogging)
            {
                Debug.LogWarning("[HoudiniImporter] No grid lines found!");
            }
            
            if (enableLogging)
            {
                Debug.Log($"[HoudiniImporter] Import completed: {levelData.levelName}");
                Debug.Log($"[HoudiniImporter] Grid size: {levelData.gridWidth}x{levelData.gridHeight}");
                Debug.Log($"[HoudiniImporter] Found: {levelData.enemyPositions.Count} enemies, {levelData.coinPositions.Count} coins, {levelData.healthPositions.Count} health items");
            }
            
            return levelData;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[HoudiniImporter] Failed to import level: {e.Message}");
            return null;
        }
    }
    
    public static HoudiniLevelData ImportFromFile(string filePath, bool enableLogging = false)
    {
        try
        {
            string iniContent = System.IO.File.ReadAllText(filePath);
            return ImportFromText(iniContent, enableLogging);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[HoudiniImporter] Failed to read file {filePath}: {e.Message}");
            return null;
        }
    }
    
    private static void ParseCellType(string line, HoudiniLevelData levelData, bool enableLogging)
    {
        // Format: ID=Symbol,Name,Passable,Prefab_Index
        string[] parts = line.Split('=');
        if (parts.Length != 2) return;
        
        if (!int.TryParse(parts[0], out int id)) return;
        
        string[] values = parts[1].Split(',');
        if (values.Length != 4) return;
        
        char symbol = values[0].Length > 0 ? values[0][0] : ' ';
        string name = values[1];
        bool passable = bool.Parse(values[2]);
        int prefabIndex = int.Parse(values[3]);
        
        HoudiniCellType cellType = new HoudiniCellType(id, symbol, name, passable, prefabIndex);
        levelData.cellTypes[id] = cellType;
        
        if (enableLogging)
            Debug.Log($"[HoudiniImporter] Cell Type: {id}='{symbol}' ({name}) passable:{passable} prefab:{prefabIndex}");
    }
    
    private static void ParseLevelConfig(string line, HoudiniLevelData levelData, bool enableLogging)
    {
        string[] parts = line.Split('=');
        if (parts.Length != 2) return;
        
        string key = parts[0].Trim();
        string value = parts[1].Trim();
        
        switch (key)
        {
            case "VERSION":
                levelData.version = value;
                break;
            case "FORMAT_VERSION":
                levelData.formatVersion = value;
                break;
            case "LEVEL_NAME":
                levelData.levelName = value;
                break;
            case "LEVEL_ID":
                levelData.levelId = value;
                break;
            case "GRID_WIDTH":
                levelData.gridWidth = int.Parse(value);
                break;
            case "GRID_HEIGHT":
                levelData.gridHeight = int.Parse(value);
                break;
        }
        
        if (enableLogging)
            Debug.Log($"[HoudiniImporter] Level Config: {key} = {value}");
    }
    
    private static void ParseGenerationParams(string line, HoudiniLevelData levelData, bool enableLogging)
    {
        string[] parts = line.Split('=');
        if (parts.Length != 2) return;
        
        string key = parts[0].Trim();
        string value = parts[1].Trim();
        
        try
        {
            switch (key)
            {
                case "HOUDINI_SEED":
                    levelData.houdiniSeed = int.Parse(value);
                    break;
                case "ROOM_COUNT":
                    levelData.roomCount = int.Parse(value);
                    break;
                case "ENEMY_DENSITY":
                    levelData.enemyDensity = float.Parse(value, CultureInfo.InvariantCulture);
                    break;
                case "LOOT_DENSITY":
                    levelData.lootDensity = float.Parse(value, CultureInfo.InvariantCulture);
                    break;
                case "COIN_DENSITY":
                    levelData.coinDensity = float.Parse(value, CultureInfo.InvariantCulture);
                    break;
                case "HEALTH_DENSITY":
                    levelData.healthDensity = float.Parse(value, CultureInfo.InvariantCulture);
                    break;
                case "BREAKABLE_DENSITY":
                    levelData.breakableDensity = float.Parse(value, CultureInfo.InvariantCulture);
                    break;
                case "EDGE_WALL_BIAS":
                    levelData.edgeWallBias = float.Parse(value, CultureInfo.InvariantCulture);
                    break;
                case "NOISE_SCALE":
                    levelData.noiseScale = float.Parse(value, CultureInfo.InvariantCulture);
                    break;
                case "NOISE_THRESHOLD":
                    levelData.noiseThreshold = float.Parse(value, CultureInfo.InvariantCulture);
                    break;
                case "MIN_ROOM_SIZE":
                    levelData.minRoomSize = int.Parse(value);
                    break;
                case "MAX_ROOM_SIZE":
                    levelData.maxRoomSize = int.Parse(value);
                    break;
                case "MIN_PLAYER_EXIT_DIST":
                    levelData.minPlayerExitDist = float.Parse(value, CultureInfo.InvariantCulture);
                    break;
            }
            
            if (enableLogging)
                Debug.Log($"[HoudiniImporter] Generation Param: {key} = {value}");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[HoudiniImporter] Failed to parse generation param {key}={value}: {e.Message}");
        }
    }
    
    private static void ParseGrid(List<string> gridLines, HoudiniLevelData levelData, bool enableLogging)
    {
        // Use dimensions from LEVEL_CONFIG (GRID_WIDTH, GRID_HEIGHT)
        // If not specified, fallback to grid content dimensions
        if (levelData.gridWidth == 0 || levelData.gridHeight == 0)
        {
            levelData.gridHeight = gridLines.Count;
            levelData.gridWidth = gridLines.Count > 0 ? gridLines[0].Length : 0;
            
            if (enableLogging)
                Debug.Log($"[HoudiniImporter] Using auto-detected grid size: {levelData.gridWidth}x{levelData.gridHeight}");
        }
        else
        {
            if (enableLogging)
                Debug.Log($"[HoudiniImporter] Using configured grid size: {levelData.gridWidth}x{levelData.gridHeight}");
        }
        
        // Create grid array with specified dimensions
        levelData.grid = new char[levelData.gridWidth, levelData.gridHeight];
        
        // Initialize entire grid with empty cells first
        for (int x = 0; x < levelData.gridWidth; x++)
        {
            for (int y = 0; y < levelData.gridHeight; y++)
            {
                levelData.grid[x, y] = '.'; // Default empty cell
            }
        }
        
        // Fill grid from ASCII data
        for (int y = 0; y < Math.Min(levelData.gridHeight, gridLines.Count); y++)
        {
            string line = gridLines[y];
            for (int x = 0; x < Math.Min(levelData.gridWidth, line.Length); x++)
            {
                levelData.grid[x, y] = line[x];
            }
        }
        
        if (enableLogging)
        {
            Debug.Log($"[HoudiniImporter] Grid parsed: {levelData.gridWidth}x{levelData.gridHeight}");
            Debug.Log($"[HoudiniImporter] ASCII data: {gridLines.Count} lines, max line length: {(gridLines.Count > 0 ? gridLines.Max(l => l.Length) : 0)}");
        }
    }
    
    private static void ExtractSpecialPositions(HoudiniLevelData levelData, bool enableLogging)
    {
        for (int y = 0; y < levelData.gridHeight; y++)
        {
            for (int x = 0; x < levelData.gridWidth; x++)
            {
                char cell = levelData.grid[x, y];
                Vector2Int pos = new Vector2Int(x, y);
                
                switch (cell)
                {
                    case 'P':
                        levelData.playerSpawn = pos;
                        break;
                    case 'X':
                        levelData.exitPosition = pos;
                        break;
                    case 'E':
                        levelData.enemyPositions.Add(pos);
                        break;
                    case 'S':
                        levelData.enemyShooterPositions.Add(pos);
                        break;
                    case 'C':
                        levelData.coinPositions.Add(pos);
                        break;
                    case 'H':
                        levelData.healthPositions.Add(pos);
                        break;
                    case 'B':
                        levelData.breakablePositions.Add(pos);
                        break;
                }
            }
        }
        
        if (enableLogging)
        {
            Debug.Log($"[HoudiniImporter] Player spawn: {levelData.playerSpawn}");
            Debug.Log($"[HoudiniImporter] Exit: {levelData.exitPosition}");
            Debug.Log($"[HoudiniImporter] Enemies: {levelData.enemyPositions.Count}");
            Debug.Log($"[HoudiniImporter] Enemy Shooters: {levelData.enemyShooterPositions.Count}");
            Debug.Log($"[HoudiniImporter] Coins: {levelData.coinPositions.Count}");
            Debug.Log($"[HoudiniImporter] Health: {levelData.healthPositions.Count}");
            Debug.Log($"[HoudiniImporter] Breakables: {levelData.breakablePositions.Count}");
        }
    }
    
    // Utility methods for editor/debugging
    public static void LogLevelData(HoudiniLevelData levelData)
    {
        if (levelData == null) return;
        
        Debug.Log("=== HOUDINI LEVEL DATA ===");
        Debug.Log($"Name: {levelData.levelName} (ID: {levelData.levelId})");
        Debug.Log($"Version: {levelData.version} | Format: {levelData.formatVersion}");
        Debug.Log($"Grid: {levelData.gridWidth}x{levelData.gridHeight}");
        Debug.Log($"Seed: {levelData.houdiniSeed} | Rooms: {levelData.roomCount}");
        Debug.Log($"Densities - Enemy:{levelData.enemyDensity} Loot:{levelData.lootDensity} Coin:{levelData.coinDensity}");
        Debug.Log($"Objects - Enemies:{levelData.enemyPositions.Count} Coins:{levelData.coinPositions.Count} Health:{levelData.healthPositions.Count}");
        Debug.Log($"Player: {levelData.playerSpawn} | Exit: {levelData.exitPosition}");
    }
    
    // Test method for editor
    [ContextMenu("Test Import Sample")]
    public void TestImportSample()
    {
        string sampleINI = @"[CELL_TYPES]
0=.,EMPTY,true,0
1=#,WALL,false,1
3=P,PLAYER,true,3
4=E,ENEMY,true,4
6=C,COIN,true,6

[LEVEL_CONFIG]
LEVEL_NAME=Test Level
LEVEL_ID=0001
GRID_WIDTH=5
GRID_HEIGHT=5

[GRID_ASCII]
#####
#P.C#
#...#
#.E.#
#####";
        
        HoudiniLevelData testData = ImportFromText(sampleINI, true);
        if (testData != null)
        {
            LogLevelData(testData);
        }
    }
}