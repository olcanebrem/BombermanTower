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
    
    [Header("Import Settings")]
    [Tooltip("Import all elements in organized batches")]
    public bool useOrganizedSpawning = true;
    
    [Header("Debug")]
    public bool logImportDetails = true;
    
    // References
    private LevelLoader levelLoader;
    private HoudiniLevelParser levelParser;
    
    // Import buffer data
    private HoudiniLevelData currentLevelData;
    private ImportBuffer importBuffer;
    
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
        
        // Get component references
        levelLoader = LevelLoader.instance;
        levelParser = HoudiniLevelParser.Instance;
        
        if (levelParser == null)
        {
            levelParser = FindObjectOfType<HoudiniLevelParser>();
        }
    }
    
    /// <summary>
    /// Import level from TextAsset using organized spawning system
    /// </summary>
    public void ImportLevel(TextAsset levelFile)
    {
        if (levelFile == null)
        {
            Debug.LogError("[LevelImporter] Level file is null!");
            return;
        }
        
        if (levelParser == null)
        {
            Debug.LogError("[LevelImporter] HoudiniLevelParser not found!");
            return;
        }
        
        if (levelLoader == null)
        {
            Debug.LogError("[LevelImporter] LevelLoader not found!");
            return;
        }
        
        Debug.Log($"[LevelImporter] Starting organized import of: {levelFile.name}");
        
        // Phase 1: Parse level data
        currentLevelData = levelParser.ParseLevelData(levelFile);
        if (currentLevelData == null)
        {
            Debug.LogError("[LevelImporter] Failed to parse level data!");
            return;
        }
        
        // Phase 2: Buffer all elements for organized spawning
        BufferLevelElements();
        
        // Phase 3: Execute organized spawn
        ExecuteOrganizedSpawn();
        
        Debug.Log("[LevelImporter] Level import completed successfully");
    }
    
    /// <summary>
    /// Buffer all level elements for organized spawning
    /// </summary>
    private void BufferLevelElements()
    {
        Debug.Log("[LevelImporter] Buffering level elements for organized spawn");
        
        importBuffer = new ImportBuffer();
        
        // Buffer grid-based elements
        BufferGridElements();
        
        // Buffer position-based elements (if any additional processing needed)
        BufferPositionElements();
        
        if (logImportDetails)
        {
            LogBufferContents();
        }
    }
    
    /// <summary>
    /// Buffer elements from grid data
    /// </summary>
    private void BufferGridElements()
    {
        for (int y = 0; y < currentLevelData.gridHeight; y++)
        {
            for (int x = 0; x < currentLevelData.gridWidth; x++)
            {
                char symbol = currentLevelData.grid[x, y];
                TileType type = TileSymbols.DataSymbolToType(symbol);
                
                if (type != TileType.Empty && type != TileType.Unknown)
                {
                    var element = new ImportElement
                    {
                        position = new Vector2Int(x, y),
                        tileType = type,
                        symbol = symbol,
                        source = ImportSource.Grid
                    };
                    
                    // Categorize elements
                    switch (type)
                    {
                        case TileType.Player:
                        case TileType.PlayerSpawn:
                            importBuffer.playerElements.Add(element);
                            break;
                            
                        case TileType.Enemy:
                        case TileType.EnemyShooter:
                            importBuffer.enemyElements.Add(element);
                            break;
                            
                        case TileType.Coin:
                        case TileType.Health:
                            importBuffer.collectibleElements.Add(element);
                            break;
                            
                        case TileType.Wall:
                        case TileType.Breakable:
                            importBuffer.staticElements.Add(element);
                            break;
                            
                        case TileType.Gate:
                            importBuffer.exitElements.Add(element);
                            break;
                            
                        default:
                            importBuffer.otherElements.Add(element);
                            break;
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// Buffer elements from position lists (for validation/additional processing)
    /// </summary>
    private void BufferPositionElements()
    {
        // This could be used for cross-validation or additional spawn points
        // For now, we rely on grid-based spawning which is more consistent
        Debug.Log($"[LevelImporter] Position lists - Enemies: {currentLevelData.enemyPositions.Count}, " +
                  $"Coins: {currentLevelData.coinPositions.Count}, Health: {currentLevelData.healthPositions.Count}");
    }
    
    /// <summary>
    /// Execute organized spawn of all buffered elements
    /// </summary>
    private void ExecuteOrganizedSpawn()
    {
        Debug.Log("[LevelImporter] Executing organized spawn");
        
        // Clear existing level content first
        levelLoader.ClearAllTiles();
        
        // Set level dimensions
        levelLoader.Width = currentLevelData.gridWidth;
        levelLoader.Height = currentLevelData.gridHeight;
        
        // Initialize arrays
        levelLoader.levelMap = new char[levelLoader.Width, levelLoader.Height];
        levelLoader.tileObjects = new GameObject[levelLoader.Width, levelLoader.Height];
        
        // Spawn in organized order
        SpawnStaticElements();   // Walls, breakables first
        SpawnCollectibles();     // Coins, health items
        SpawnEnemies();          // Enemy units
        SpawnExits();           // Exit points
        SpawnPlayer();          // Player last
        
        // Update level loader state
        levelLoader.currentLevelData = currentLevelData;
        
        Debug.Log("[LevelImporter] Organized spawn completed");
    }
    
    private void SpawnStaticElements()
    {
        Debug.Log($"[LevelImporter] Spawning {importBuffer.staticElements.Count} static elements");
        
        foreach (var element in importBuffer.staticElements)
        {
            SpawnElement(element);
        }
    }
    
    private void SpawnCollectibles()
    {
        Debug.Log($"[LevelImporter] Spawning {importBuffer.collectibleElements.Count} collectibles");
        
        foreach (var element in importBuffer.collectibleElements)
        {
            SpawnElement(element);
        }
    }
    
    private void SpawnEnemies()
    {
        Debug.Log($"[LevelImporter] Spawning {importBuffer.enemyElements.Count} enemies");
        
        foreach (var element in importBuffer.enemyElements)
        {
            SpawnElement(element);
        }
    }
    
    private void SpawnExits()
    {
        Debug.Log($"[LevelImporter] Spawning {importBuffer.exitElements.Count} exits");
        
        foreach (var element in importBuffer.exitElements)
        {
            SpawnElement(element);
        }
    }
    
    private void SpawnPlayer()
    {
        Debug.Log($"[LevelImporter] Spawning {importBuffer.playerElements.Count} player elements");
        
        foreach (var element in importBuffer.playerElements)
        {
            if (element.tileType == TileType.PlayerSpawn)
            {
                // Handle player spawn through LevelLoader's player system
                levelLoader.playerStartX = element.position.x;
                levelLoader.playerStartY = element.position.y;
                levelLoader.CreatePlayerAtSpawn();
            }
            else
            {
                SpawnElement(element);
            }
        }
    }
    
    /// <summary>
    /// Spawn individual element through LevelLoader
    /// </summary>
    private void SpawnElement(ImportElement element)
    {
        // Delegate to LevelLoader's prefab system
        if (levelLoader.CreateTileAt(element.position.x, element.position.y, element.tileType))
        {
            if (logImportDetails)
            {
                Debug.Log($"[LevelImporter] Spawned {element.tileType} at ({element.position.x}, {element.position.y})");
            }
        }
        else
        {
            Debug.LogWarning($"[LevelImporter] Failed to spawn {element.tileType} at ({element.position.x}, {element.position.y})");
        }
    }
    
    private void LogBufferContents()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("\n=== IMPORT BUFFER CONTENTS ===");
        sb.AppendLine($"Static Elements: {importBuffer.staticElements.Count}");
        sb.AppendLine($"Collectibles: {importBuffer.collectibleElements.Count}");
        sb.AppendLine($"Enemies: {importBuffer.enemyElements.Count}");
        sb.AppendLine($"Exits: {importBuffer.exitElements.Count}");
        sb.AppendLine($"Player: {importBuffer.playerElements.Count}");
        sb.AppendLine($"Other: {importBuffer.otherElements.Count}");
        sb.AppendLine("==============================");
        
        Debug.Log(sb.ToString());
    }
}

/// <summary>
/// Data structure for buffering import elements
/// </summary>
public class ImportBuffer
{
    public List<ImportElement> staticElements = new List<ImportElement>();
    public List<ImportElement> collectibleElements = new List<ImportElement>();
    public List<ImportElement> enemyElements = new List<ImportElement>();
    public List<ImportElement> exitElements = new List<ImportElement>();
    public List<ImportElement> playerElements = new List<ImportElement>();
    public List<ImportElement> otherElements = new List<ImportElement>();
}

/// <summary>
/// Individual element to be imported
/// </summary>
public class ImportElement
{
    public Vector2Int position;
    public TileType tileType;
    public char symbol;
    public ImportSource source;
}

/// <summary>
/// Source of import element
/// </summary>
public enum ImportSource
{
    Grid,
    PositionList,
    Generated
}