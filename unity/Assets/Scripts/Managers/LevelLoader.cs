using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Debug = UnityEngine.Debug;
#if UNITY_EDITOR
using UnityEditor;
#endif

[System.Serializable]
public struct TilePrefabEntry
{
    public TileType type;
    public TileBase prefab;
}

[System.Serializable]
public struct LevelFileEntry
{
    public string fileName;
    public string fullPath;
    public TextAsset textAsset;
    public int levelNumber;
    public string version;
}

public class LevelLoader : MonoBehaviour
{
    // --- Singleton ve Temel Ayarlar ---
    public static LevelLoader instance;
    public int tileSize = 30;
    
    // --- Prefab Yönetimi ---
    public TilePrefabEntry[] tilePrefabs;
    private Dictionary<TileType, TileBase> prefabMap;
    public GameObject playerPrefab;
    public SpriteDatabase spriteDatabase;
    
    // Component references minimized - services handle parsing
    
    // --- Service References ---
    private ILevelDataService levelDataService;
    private IContainerService containerService;
    private IPlayerService playerService;
    
    // --- Current Level Data ---
    public HoudiniLevelData currentLevelData; 
    
    public int Width { get; set; }
    public int Height { get; set; }
    
    // Layered grid system
    private LayeredGridService layeredGrid;
    
    private GameObject playerObject; // Runtime player instance reference
    public int playerStartX, playerStartY;
    
    // Container management moved to ContainerService
    // Legacy references kept for API compatibility
    [Header("Legacy Containers - Deprecated")]
    [System.Obsolete("Use ContainerService instead")]
    public Transform levelContentParent;
    [System.Obsolete("Use ContainerService instead")]
    public Transform gridParent;
    [System.Obsolete("Use ContainerService instead")]
    public Transform dynamicParent;
    
    // Private container references for backward compatibility
    private Transform currentLevelContainer;
    private Transform currentStaticContainer;
    private Transform currentDestructibleContainer;
    private Transform currentDynamicContainer;
    private Transform wallsContainer;
    private Transform breakablesContainer;
    private Transform gatesContainer;
    private Transform enemiesContainer;
    private Transform collectiblesContainer;
    private Transform effectsContainer;
    private Transform projectilesContainer;
    
    // ML-Agent support - object tracking
    private List<GameObject> enemies = new List<GameObject>();
    private List<GameObject> collectibles = new List<GameObject>();
    private GameObject exitObject;
    
    // Events for cache invalidation
    public event System.Action OnEnemyListChanged;
    public event System.Action OnCollectibleListChanged;
    
    // Level loading events (sequence events moved to LevelSequencer)
    public void DebugPrintMap()
    {
        // TurnManager'dan o anki tur sayısını alarak log'u daha bilgilendirici yapalım.
        int currentTurn = (TurnManager.Instance != null) ? TurnManager.Instance.TurnCount : 0;
        
        // Konsolda daha kolay bulmak için bir başlık oluşturalım.
        string mapOutput = $"--- MANTIKSAL HARİTA DURUMU: TUR {currentTurn} ---\n"; // '\n' yeni bir satır başlatır.

        // Layered system'den haritayı oluşturalım
        if (layeredGrid != null)
        {
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    // Layer bilgisini kontrol et
                    var staticMask = layeredGrid.GetStaticTile(x, y);
                    var destructibleMask = layeredGrid.GetDestructibleTile(x, y);
                    GameObject actor = layeredGrid.GetActorAt(x, y);
                    GameObject bomb = layeredGrid.GetBombAt(x, y);
                    
                    // Öncelik sırasına göre göster
                    if (actor != null)
                    {
                        var tileBase = actor.GetComponent<TileBase>();
                        if (tileBase != null)
                            mapOutput += TileSymbols.TypeToDataSymbol(tileBase.TileType);
                        else
                            mapOutput += "?";
                    }
                    else if (bomb != null)
                        mapOutput += TileSymbols.TypeToDataSymbol(TileType.Bomb);
                    else if ((staticMask & LayeredGridService.LayerMask.BlocksMovement) != 0)
                        mapOutput += TileSymbols.TypeToDataSymbol(TileType.Wall);
                    else if ((destructibleMask & LayeredGridService.LayerMask.Destructible) != 0)
                        mapOutput += TileSymbols.TypeToDataSymbol(TileType.Breakable);
                    else
                        mapOutput += TileSymbols.TypeToDataSymbol(TileType.Empty);
                }
                mapOutput += "\n";
            }
        }
        else
        {
            mapOutput += "LayeredGridService not available\n";
        }

        // Oluşturulan harita string'ini, konsolda öne çıkması için bir uyarı olarak yazdır.
    }
    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }
        instance = this;
        
        // Ensure GameEventBus is available
        if (GameEventBus.Instance == null)
        {
            var eventBusGO = new GameObject("GameEventBus");
            eventBusGO.AddComponent<GameEventBus>();
        }

        // HoudiniLevelParser is now managed by LevelDataService
        // No direct reference needed in LevelLoader
        
        // Initialize services
        InitializeServices();

        // Prefab sözlüğünü doldur - önce otomatik keşif dene
        prefabMap = new Dictionary<TileType, TileBase>();
        
        // Otomatik prefab keşif sistemi
        AutoDiscoverPrefabs();
        
        // Inspector'dan manual atamalar (varsa) - otomatiklerin üzerine yazar
        if (tilePrefabs != null && tilePrefabs.Length > 0)
        {
            // Debug.Log("[LevelLoader] === MANUAL PREFAB OVERRIDES ===");
            foreach (var entry in tilePrefabs)
            {
                if (entry.prefab != null)
                {
                    if (prefabMap.ContainsKey(entry.type))
                    {
                        // Debug.Log($"[LevelLoader] Overriding {entry.type}: {prefabMap[entry.type].name} -> {entry.prefab.name}");
                        prefabMap[entry.type] = entry.prefab;
                    }
                    else
                    {
                        prefabMap.Add(entry.type, entry.prefab);
                        // Debug.Log($"[LevelLoader] Manual assignment {entry.type} -> {entry.prefab.name}");
                    }
                }
            }
        }
        else
        {
            // Debug.Log("[LevelLoader] No manual prefab overrides - using auto-discovery only");
        }
        
        // Debug.Log($"[LevelLoader] Final prefab map: {prefabMap.Count} entries");
        // Sprite database'i initialize et
        if (spriteDatabase != null)
        {
            spriteDatabase.Initialize(); // Veri tabanını hazırla
            // Debug.Log("[LevelLoader] SpriteDatabase initialized");
        }
        else
        {
            // Debug.LogWarning("[LevelLoader] spriteDatabase is null! Please assign SpriteDatabase in Inspector.");
        }
        
        // Services handle container initialization now
    }

    void Start()
    {
        // Level data service handles file scanning
        levelDataService?.ScanForLevelFiles();
    }
    
    /// <summary>
    /// Delegate to LevelDataService for level file scanning
    /// </summary>
    public void ScanForLevelFiles()
    {
        levelDataService?.ScanForLevelFiles();
    }
    
    /// <summary>
    /// Editor method to refresh level files - delegates to LevelDataService
    /// </summary>
    public void RefreshLevelFilesInEditor()
    {
        levelDataService?.ScanForLevelFiles();
    }
    
    /// <summary>
    /// Initialize service references
    /// </summary>
    private void InitializeServices()
    {
        // Get or create service instances
        levelDataService = LevelDataService.Instance;
        if (levelDataService == null)
        {
            var serviceGO = new GameObject("LevelDataService");
            levelDataService = serviceGO.AddComponent<LevelDataService>();
        }
        
        containerService = ContainerService.Instance;
        if (containerService == null)
        {
            var serviceGO = new GameObject("ContainerService");
            containerService = serviceGO.AddComponent<ContainerService>();
        }
        
        playerService = PlayerService.Instance;
        if (playerService == null)
        {
            var serviceGO = new GameObject("PlayerService");
            playerService = serviceGO.AddComponent<PlayerService>();
            
            // Set player prefab on service
            if (playerPrefab != null)
            {
                playerService.PlayerPrefab = playerPrefab;
            }
        }
        
        // Initialize LayeredGridService
        if (LayeredGridService.Instance == null)
        {
            var layeredGridGO = new GameObject("LayeredGridService");
            layeredGrid = layeredGridGO.AddComponent<LayeredGridService>();
        }
        else
        {
            layeredGrid = LayeredGridService.Instance;
        }
    }
    
    // Helper methods moved to ContainerService
    
    // Container creation moved to ContainerService
    
    /// <summary>
    /// Get appropriate container for tile type using container service
    /// </summary>
    private Transform GetContainerForTileType(TileType tileType)
    {
        return containerService?.GetContainerForTileType(tileType) ?? levelContentParent;
    }
    
    /// <summary>
    /// Get projectiles container for external classes (like Projectile.Spawn)
    /// </summary>
    public Transform GetProjectilesContainer()
    {
        return containerService?.GetProjectilesContainer() ?? levelContentParent;
    }
    
    /// <summary>
    /// Get effects container for external classes (like explosion effects)
    /// </summary>
    public Transform GetEffectsContainer()
    {
        return containerService?.GetEffectsContainer() ?? levelContentParent;
    }
    
    // Level file scanning moved to LevelDataService
    
    // Level file scanning moved to LevelDataService
    
    // Level file scanning moved to LevelDataService
    
    // Level file validation moved to LevelDataService
    
    // Level file parsing moved to LevelDataService
    
    /// <summary>
    /// Load selected level using service layer
    /// </summary>
    public void LoadSelectedLevel()
    {
        if (levelDataService == null)
        {
            Debug.LogError("[LevelLoader] LevelDataService not available!");
            return;
        }
        
        var selectedLevel = levelDataService.GetSelectedLevel();
        if (selectedLevel.textAsset == null)
        {
            Debug.LogError("[LevelLoader] No level selected or level asset is null!");
            return;
        }
        
        var levelData = levelDataService.LoadLevelData(selectedLevel.textAsset);
        if (levelData != null)
        {
            LoadFromLevelData(levelData);
        }
    }
    
    /// <summary>
    /// Delegate to LevelDataService
    /// </summary>
    public void SelectLevel(int index)
    {
        // This method is deprecated - use LevelDataService directly
        Debug.LogWarning("[LevelLoader] SelectLevel is deprecated. Use LevelDataService.SelectLevelByNumber instead.");
    }
    
    /// <summary>
    /// Get available levels from service
    /// </summary>
    public List<LevelFileEntry> GetAvailableLevels()
    {
        return levelDataService?.GetAvailableLevels() ?? new List<LevelFileEntry>();
    }
    
    /// <summary>
    /// Get selected level from service
    /// </summary>
    public LevelFileEntry GetSelectedLevel()
    {
        return levelDataService?.GetSelectedLevel() ?? new LevelFileEntry();
    }
    
    /// <summary>
    /// Delegate to LevelDataService
    /// </summary>
    public bool SelectNextLevel()
    {
        if (levelDataService?.SelectNextLevel() == true)
        {
            LoadSelectedLevel();
            return true;
        }
        return false;
    }
    
    /// <summary>
    /// Delegate to LevelDataService
    /// </summary>
    public bool SelectPreviousLevel()
    {
        if (levelDataService?.SelectPreviousLevel() == true)
        {
            LoadSelectedLevel();
            return true;
        }
        return false;
    }
    
    /// <summary>
    /// Delegate to LevelDataService
    /// </summary>
    public bool SelectLevelByNumber(int levelNumber)
    {
        if (levelDataService?.SelectLevelByNumber(levelNumber) == true)
        {
            LoadSelectedLevel();
            return true;
        }
        return false;
    }
    
    /// <summary>
    /// Delegate to LevelDataService
    /// </summary>
    public bool SelectLevelByNumberAndVersion(int levelNumber, string version)
    {
        if (levelDataService?.SelectLevelByNumberAndVersion(levelNumber, version) == true)
        {
            LoadSelectedLevel();
            return true;
        }
        return false;
    }
    
    /// <summary>
    /// Delegate to LevelDataService
    /// </summary>
    public void ResetToFirstLevel()
    {
        levelDataService?.ResetToFirstLevel();
        LoadSelectedLevel();
    }
    
    /// <summary>
    /// Delegate to LevelDataService
    /// </summary>
    public void SelectLastLevel()
    {
        levelDataService?.SelectLastLevel();
        LoadSelectedLevel();
    }
    
    /// <summary>
    /// Get total level count from service
    /// </summary>
    public int GetTotalLevelCount()
    {
        return levelDataService?.GetTotalLevelCount() ?? 0;
    }
    
    /// <summary>
    /// Get current level index - deprecated, use LevelDataService
    /// </summary>
    [System.Obsolete("Use LevelDataService instead")]
    public int GetCurrentLevelIndex()
    {
        Debug.LogWarning("[LevelLoader] GetCurrentLevelIndex is deprecated. Use LevelDataService instead.");
        return 0;
    }
    
    /// <summary>
    /// Get current level number from service
    /// </summary>
    public int GetCurrentLevelNumber()
    {
        return levelDataService?.GetCurrentLevelNumber() ?? 1;
    }
    
    /// <summary>
    /// Check if next level exists using service
    /// </summary>
    public bool HasNextLevel()
    {
        return levelDataService?.HasNextLevel() ?? false;
    }
    
    /// <summary>
    /// Check if previous level exists using service
    /// </summary>
    public bool HasPreviousLevel()
    {
        return levelDataService?.HasPreviousLevel() ?? false;
    }

    /// <summary>
    /// TextAsset'ten seviye verisini okur, boyutları belirler ve levelMap'i doldurur.
    /// </summary>
    /// <summary>
    /// HoudiniLevelData'dan level verilerini alır ve sahneyi hazırlar
    /// </summary>
    public void LoadLevelFromHoudiniData(HoudiniLevelData levelData)
    {
        if (levelData == null)
        {
            Debug.LogError("[LevelLoader] HoudiniLevelData is null!");
            return;
        }
        
        // Publish level load started event
        GameEventBus.Instance?.Publish(new LevelLoadStarted(levelData.levelId, levelData.levelName));

        // Complete cleanup of existing level using services
        string previousLevelId = currentLevelData?.levelId ?? "Unknown";
        GameEventBus.Instance?.Publish(new LevelCleanupStarted(previousLevelId));
        
        ClearAllTilesUsingServices();
        
        GameEventBus.Instance?.Publish(new LevelCleanupCompleted(previousLevelId, true));

        // Set new dimensions
        Width = levelData.gridWidth;
        Height = levelData.gridHeight;
        
        // Initialize and verify layered grid system
        if (layeredGrid != null)
        {
            layeredGrid.Initialize(Width, Height);
            
            // Verify layers are completely clear
            bool layersClean = VerifyLayersClean();
            if (!layersClean)
            {
                Debug.LogError("[🚀 LEVEL_LOAD] ❌ Layers not properly cleared! Level sequencing may fail!");
                return;
            }
        }
        else
        {
            Debug.LogError("[🚀 LEVEL_LOAD] ❌ LayeredGridService is null! Cannot proceed with level loading!");
            return;
        }
        
        // Create level-specific containers using service
        containerService?.CreateLevelContainers(levelData.levelId, levelData.levelName);
        
        
        // Setup layered system with new static tiles
        if (layeredGrid != null && levelData.grid != null)
        {
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    char cellSymbol = (x < levelData.grid.GetLength(0) && y < levelData.grid.GetLength(1)) 
                        ? levelData.grid[x, y] 
                        : TileSymbols.TypeToDataSymbol(TileType.Empty);
                        
                    TileType cellType = TileSymbols.DataSymbolToType(cellSymbol);
                    
                    // Set static layer data
                    if (cellType == TileType.Wall)
                    {
                        layeredGrid.SetStaticTile(x, y, LayeredGridService.LayerMask.BlocksMovement | LayeredGridService.LayerMask.BlocksFire);
                    }
                    else if (cellType == TileType.Breakable)
                    {
                        layeredGrid.SetDestructibleTile(x, y, LayeredGridService.LayerMask.BlocksMovement | LayeredGridService.LayerMask.BlocksFire | LayeredGridService.LayerMask.Destructible);
                    }
                }
            }
        }
        else
        {
            Debug.LogError("[🚀 LEVEL_LOAD] ❌ Cannot process grid data - layeredGrid or levelData.grid is null!");
            return;
        }
        
        // Set player spawn position
        playerStartX = levelData.playerSpawn.x;
        playerStartY = levelData.playerSpawn.y;
        
        // Create visual objects and player using services
        CreateMapVisualUsingServices();
        CreatePlayerUsingService();
        
        // Verify tile counts match level data
        bool tileCountsMatch = VerifyTileCounts(levelData);
        
        if (!tileCountsMatch)
        {
            Debug.LogError($"[LEVEL_LOAD] CRITICAL ERROR: Tile counts don't match for level {levelData.levelName}!");
            
            // Publish level load failed event
            GameEventBus.Instance?.Publish(new LevelLoadCompleted(levelData.levelId, levelData.levelName, levelData, false));
            
            // Stop the game/application
            #if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
            #else
                Application.Quit();
            #endif
            return;
        }
        
        // Publish level load completed event
        GameEventBus.Instance?.Publish(new LevelLoadCompleted(levelData.levelId, levelData.levelName, levelData, true));
    }
    
    /// <summary>
    /// Wrapper method for LoadLevelFromHoudiniData - maintains API compatibility
    /// </summary>
    public void LoadFromLevelData(HoudiniLevelData levelData)
    {
        // Update currentLevelData to prevent caching issues
        currentLevelData = levelData;
        
        LoadLevelFromHoudiniData(levelData);
    }
    
    
    /// <summary>
    /// Create map visual using legacy method maintained for compatibility
    /// </summary>
    void CreateMapVisual()
    {
        // Create visual objects from level data
        if (currentLevelData != null && currentLevelData.grid != null)
        {
            CreateVisualTiles();
        }

        // Player creation with fresh instantiation
        CreatePlayerAtSpawn();
        
        // Log TurnManager state after level creation for debugging if needed
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.LogAllRegisteredObjects();
        }
    }
    
    /// <summary>
    /// Create visual tile objects and place them in layered system
    /// </summary>
    private void CreateVisualTiles()
    {
        int createdBreakables = 0, createdEnemies = 0, createdCoins = 0, createdHealth = 0;
        
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                char symbolChar = (x < currentLevelData.grid.GetLength(0) && y < currentLevelData.grid.GetLength(1)) 
                    ? currentLevelData.grid[x, y] 
                    : TileSymbols.TypeToDataSymbol(TileType.Empty);
                    
                TileType type = TileSymbols.DataSymbolToType(symbolChar);

                // Skip empty cells, player spawn, and player tiles (player created in CreatePlayerAtSpawn)
                if (type == TileType.Empty || type == TileType.PlayerSpawn || type == TileType.Player)
                {
                    continue;
                }

                // Create visual object
                if (prefabMap.TryGetValue(type, out var tileBasePrefab) && tileBasePrefab != null)
                {
                    Vector3 pos = layeredGrid.GridToWorld(x, y);
                    Transform parentContainer = GetContainerForTileType(type);
                    TileBase newTile = Instantiate(tileBasePrefab, pos, Quaternion.identity, parentContainer);
                    
                    // Setup tile
                    if (spriteDatabase != null)
                    {
                        newTile.SetVisual(spriteDatabase.GetSprite(type));
                    }
                    (newTile as IInitializable)?.Init(x, y);
                    
                    // Place in appropriate layer
                    PlaceTileInLayer(newTile.gameObject, x, y, type);
                    
                    // Count successfully created tiles
                    switch (type)
                    {
                        case TileType.Breakable: createdBreakables++; break;
                        case TileType.Enemy: 
                        case TileType.EnemyShooter: createdEnemies++; break;
                        case TileType.Coin: createdCoins++; break;
                        case TileType.Health: createdHealth++; break;
                    }
                    
                    // Legacy ML-Agent tracking
                    UpdateMLAgentTracking(type, newTile.gameObject);
                    
                }
                else
                {
                    Debug.LogWarning($"[LevelLoader] No prefab found for {type} at ({x}, {y})");
                }
            }
        }
    }
    
    /// <summary>
    /// Place tile in appropriate layer based on type
    /// </summary>
    private void PlaceTileInLayer(GameObject tileObj, int x, int y, TileType type)
    {
        switch (type)
        {
            case TileType.Player:
                // Special handling for Player - save reference and register with systems
                playerObject = tileObj;
                var playerController = tileObj.GetComponent<PlayerController>();
                if (playerController != null)
                {
                    // Register with GameManager
                    if (GameManager.Instance != null)
                    {
                        GameManager.Instance.RegisterPlayer(playerController);
                    }
                    Debug.Log($"[🎮 VISUAL_SPAWN] Player created from level data at ({x},{y})");
                }
                
                if (!layeredGrid.PlaceActor(tileObj, x, y))
                {
                    Debug.LogWarning($"[LevelLoader] Failed to place Player at ({x},{y}) - position occupied!");
                    Destroy(tileObj);
                    playerObject = null;
                }
                break;
                
            case TileType.Enemy:
            case TileType.EnemyShooter:
                if (!layeredGrid.PlaceActor(tileObj, x, y))
                {
                    Debug.LogWarning($"[LevelLoader] Failed to place {type} at ({x},{y}) - position occupied!");
                    Destroy(tileObj);
                }
                break;
                
            case TileType.Coin:
            case TileType.Health:
                if (!layeredGrid.PlaceItem(tileObj, x, y))
                {
                    Debug.LogWarning($"[LevelLoader] Failed to place {type} at ({x},{y}) - position occupied!");
                    Destroy(tileObj);
                }
                break;
                
            case TileType.Gate:
                // Gates are special - they don't block movement but are tracked
                exitObject = tileObj;
                break;
                
            // Static tiles are already set in layer masks during level data loading
            case TileType.Wall:
                // Walls are handled by static layer masks only
                break;
                
            case TileType.Breakable:
                // BreakableTiles need both layer mask AND GameObject reference for damage handling
                if (!layeredGrid.PlaceDestructibleObject(tileObj, x, y))
                {
                    Debug.LogError($"[BREAKABLE] Failed to place BreakableTile at ({x},{y}) - destroying object!");
                    Destroy(tileObj);
                }
                break;
                
            default:
                Debug.LogWarning($"[LevelLoader] Unknown tile type for layered placement: {type}");
                break;
        }
    }
    /// <summary>
    /// Clear all existing level objects before loading new level
    /// </summary>
    public void ClearAllTiles()
    {
        Debug.Log("[LevelLoader] ClearAllTiles - destroying existing level objects");
        
        // Unregister current player from singleton managers before cleanup
        if (PlayerAgentManager.Instance != null)
        {
            PlayerAgentManager.Instance.UnregisterPlayer();
        }
        
        // First, let's check all players in scene before cleanup
        var allPlayersBeforeCleanup = FindObjectsOfType<PlayerController>();
        Debug.Log($"[🧹 CLEANUP] Before cleanup: Found {allPlayersBeforeCleanup.Length} PlayerController(s) in scene");
        for (int i = 0; i < allPlayersBeforeCleanup.Length; i++)
        {
            var player = allPlayersBeforeCleanup[i];
            Debug.Log($"  Player {i+1}: '{player.name}' (Active: {player.gameObject.activeInHierarchy})");
        }
        
        // Clear TurnManager registrations first to prevent null reference issues
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.ClearAllRegistersExceptPlayer();
        }
        
        // Clear layered grid system - LayeredGridService now handles GameObject destruction internally
        if (layeredGrid != null)
        {
            Debug.Log($"[🧹 LAYERED_CLEAR] BEFORE CLEARING:");
            Debug.Log($"[🧹 LAYERED_CLEAR] Grid Size: {layeredGrid.Width}x{layeredGrid.Height}");
            
            // Just get counts for logging
            var allActors = layeredGrid.AllActors;
            var allBombs = layeredGrid.AllBombs;
            var allItems = layeredGrid.AllItems;
            var allEffects = layeredGrid.AllEffects;
            
            Debug.Log($"[🧹 LAYERED_CLEAR] Found - Actors: {allActors.Count}, Bombs: {allBombs.Count}, Items: {allItems.Count}, Effects: {allEffects.Count}");
            
            // LayeredGridService.ClearAllLayers() now handles all GameObject destruction internally
            layeredGrid.ClearAllLayers();
            Debug.Log("[🧹 FINAL_CLEAR] LayeredGridService cleanup completed");
        }
        
        // Manual cleanup of any orphaned explosion objects not tracked by layered grid
        var orphanedExplosions = FindObjectsOfType<MovingExplosion>();
        var orphanedExplosionTiles = FindObjectsOfType<ExplosionTile>();
        
        foreach (var explosion in orphanedExplosions)
        {
            if (explosion != null)
            {
                Destroy(explosion.gameObject);
            }
        }
        
        foreach (var explosionTile in orphanedExplosionTiles)
        {
            if (explosionTile != null)
            {
                Destroy(explosionTile.gameObject);
            }
        }
        
        if (orphanedExplosions.Length > 0 || orphanedExplosionTiles.Length > 0)
        {
            Debug.Log($"[LevelLoader] Manual cleanup: {orphanedExplosions.Length} MovingExplosions, {orphanedExplosionTiles.Length} ExplosionTiles");
        }
        
        // Clear ML-Agent tracking lists
        enemies.Clear();
        collectibles.Clear();
        exitObject = null;
        
        // COMPLETE CLEANUP: Clear playerObject reference since we're destroying everything
        playerObject = null;
        
        // CRITICAL: Clear all layered grid layers (static, destructible, etc.)
        if (layeredGrid != null)
        {
            Debug.Log("[🧹 FINAL_CLEAR] Clearing all layered grid layers");
            layeredGrid.ClearAllLayers();
            Debug.Log("[🧹 FINAL_CLEAR] All layers cleared successfully");
        }
        
        // Clear level-specific containers
        if (currentLevelContainer != null)
        {
            Debug.Log($"[🧹 CONTAINER_CLEAR] Destroying current level container: {currentLevelContainer.name}");
            Destroy(currentLevelContainer.gameObject);
            currentLevelContainer = null;
            currentStaticContainer = null;
            currentDestructibleContainer = null;
            currentDynamicContainer = null;
        }
        
        // Clear legacy container references
        wallsContainer = null;
        breakablesContainer = null;
        gatesContainer = null;
        enemiesContainer = null;
        collectiblesContainer = null;
        effectsContainer = null;
        projectilesContainer = null;
        gridParent = null;
        dynamicParent = null;
        
        Debug.Log("[LevelLoader] ClearAllTiles completed - FULL CLEANUP DONE");
    }
    
    /// <summary>
    /// Verify that all layers are completely clean before loading new level
    /// </summary>
    private bool VerifyLayersClean()
    {
        if (layeredGrid == null) return false;
        
        // Sample check a few key positions to verify cleanup
        int sampleCount = Math.Min(10, layeredGrid.Width * layeredGrid.Height);
        int cleanCount = 0;
        
        for (int i = 0; i < sampleCount; i++)
        {
            int x = i % layeredGrid.Width;
            int y = i / layeredGrid.Width;
            
            var staticMask = layeredGrid.GetStaticTile(x, y);
            var destructibleMask = layeredGrid.GetDestructibleTile(x, y);
            var actor = layeredGrid.GetActorAt(x, y);
            var bomb = layeredGrid.GetBombAt(x, y);
            
            if (staticMask == LayeredGridService.LayerMask.None && 
                destructibleMask == LayeredGridService.LayerMask.None &&
                actor == null && bomb == null)
            {
                cleanCount++;
            }
        }
        
        bool isClean = (cleanCount == sampleCount);
        return (cleanCount == sampleCount);
    }
    
    // Container creation moved to ContainerService
    
    /// <summary>
    /// Verify that scene tile counts match expected counts from level data
    /// </summary>
    private bool VerifyTileCounts(HoudiniLevelData levelData)
    {
        if (levelData == null || layeredGrid == null)
        {
            Debug.LogError("[TILE_VERIFY] Cannot verify - levelData or layeredGrid is null!");
            return false;
        }
        
        // Get expected counts from level data
        var expectedCounts = levelData.GetExpectedTileCounts();
        
        // Get actual counts from scene
        var actualCounts = layeredGrid.CountCurrentTiles();
        
        bool allMatch = true;
        var mismatches = new List<string>();
        
        // Compare counts for important tile types
        var criticalTileTypes = new[] { 
            TileType.Wall, 
            TileType.Breakable, 
            TileType.Player, 
            TileType.Enemy, 
            TileType.EnemyShooter,
            TileType.Coin, 
            TileType.Health 
        };
        
        foreach (var tileType in criticalTileTypes)
        {
            int expected = expectedCounts.ContainsKey(tileType) ? expectedCounts[tileType] : 0;
            int actual = actualCounts.ContainsKey(tileType) ? actualCounts[tileType] : 0;
            
            if (expected != actual)
            {
                allMatch = false;
                mismatches.Add($"{tileType}: Expected {expected}, Got {actual}");
                Debug.LogError($"[TILE_VERIFY] MISMATCH - {tileType}: Expected {expected}, Got {actual}");
            }
        }
        
        if (!allMatch)
        {
            Debug.LogError($"[TILE_VERIFY] TILE COUNT MISMATCH DETECTED! Level: {levelData.levelName}");
            Debug.LogError($"[TILE_VERIFY] Mismatches: {string.Join(", ", mismatches)}");
        }
        
        return allMatch;
    }
    
        /// <summary>
    /// Atomic bomb placement using layered system
    /// Returns true if successfully placed, false if position occupied
    /// </summary>
    public bool TryPlaceBombAt(int x, int y, out GameObject placedBomb)
    {
        placedBomb = null;
        
        if (layeredGrid == null || !layeredGrid.IsValidPosition(x, y))
        {
            return false;
        }
        
        // Check if position already has a bomb
        if (layeredGrid.GetBombAt(x, y) != null)
        {
            Debug.LogWarning($"[LevelLoader] Bomb position ({x}, {y}) already occupied");
            return false;
        }
        
        // Check if position is walkable (no walls, breakables)
        if (!layeredGrid.IsWalkable(x, y))
        {
            Debug.LogWarning($"[LevelLoader] Cannot place bomb at ({x}, {y}) - position not walkable");
            return false;
        }
        
        // Get bomb prefab
        if (!prefabMap.TryGetValue(TileType.Bomb, out var bombTilePrefab) || bombTilePrefab == null)
        {
            Debug.LogError($"[LevelLoader] No bomb prefab found in prefabMap!");
            return false;
        }
        
        // Create bomb
        Vector3 pos = layeredGrid.GridToWorld(x, y);
        Transform bombParent = GetContainerForTileType(TileType.Bomb);
        TileBase newBomb = Instantiate(bombTilePrefab, pos, Quaternion.identity, bombParent);

        // Setup bomb
        newBomb.SetVisual(spriteDatabase.GetSprite(TileType.Bomb));
        (newBomb as IInitializable)?.Init(x, y);
        
        // Place bomb in layered system
        bool placementSuccess = layeredGrid.PlaceBomb(newBomb.gameObject, x, y);
        if (placementSuccess)
        {
            placedBomb = newBomb.gameObject;
            Debug.Log($"[LevelLoader] Successfully placed bomb at ({x},{y}) using layered system");
            return true;
        }
        else
        {
            // Clean up if placement failed
            Destroy(newBomb.gameObject);
            return false;
        }
    }
    
    /// <summary>
    /// Legacy method for backward compatibility - now uses atomic operation
    /// </summary>
    public void PlaceBombAt(int x, int y)
    {
        TryPlaceBombAt(x, y, out _);
    }
    
    // ML-Agent support methods
    public List<GameObject> GetEnemies() => new List<GameObject>(enemies);
    public List<GameObject> GetCollectibles() => new List<GameObject>(collectibles);
    public GameObject GetExitObject() => exitObject;
    public Vector2Int GetPlayerSpawnPosition() => new Vector2Int(playerStartX, playerStartY);
    
    // HoudiniLevelData access methods
    public HoudiniLevelData GetCurrentLevelData() => currentLevelData;
    public int GetEnemyCount() => currentLevelData?.enemyPositions?.Count ?? 0;
    public int GetCollectibleCount() => (currentLevelData?.coinPositions?.Count ?? 0) + (currentLevelData?.healthPositions?.Count ?? 0);
    public Vector2Int GetExitPosition() => currentLevelData?.exitPosition ?? Vector2Int.zero;
    
    /// <summary>
    /// DEPRECATED: Use RemoveTileAt() instead for centralized tile management
    /// </summary>
    [System.Obsolete("Use RemoveTileAt() for centralized tile management")]
    public void RemoveEnemy(GameObject enemy)
    {
        
        if (enemies.Contains(enemy))
        {
            // Get position and delegate to centralized method
            var enemyTile = enemy.GetComponent<EnemyTile>();
            var enemyShooterTile = enemy.GetComponent<EnemyShooterTile>();
            
            Vector2Int gridPos;
            if (enemyTile != null)
            {
                gridPos = new Vector2Int(enemyTile.X, enemyTile.Y);
            }
            else if (enemyShooterTile != null)
            {
                gridPos = new Vector2Int(enemyShooterTile.X, enemyShooterTile.Y);
            }
            else
            {
                gridPos = WorldToGrid(enemy.transform.position);
            }
            
            // Use centralized removal method
            RemoveTileAt(gridPos.x, gridPos.y);
        }
        else
        {
        }
    }
    
    /// <summary>
    /// DEPRECATED: Use RemoveTileAt() instead for centralized tile management
    /// </summary>
    [System.Obsolete("Use RemoveTileAt() for centralized tile management")]
    public void RemoveCollectible(GameObject collectible)
    {
        
        if (collectible == null)
        {
            return;
        }
        
        // Get position and delegate to centralized method
        Vector2Int gridPos = WorldToGrid(collectible.transform.position);
        RemoveTileAt(gridPos.x, gridPos.y);
    }
    
    /// <summary>
    /// LEGACY METHOD: Use DestroyTileAt() instead
    /// </summary>
    [System.Obsolete("Use DestroyTileAt() instead")]
    public void RemoveTileAt(int x, int y)
    {
        DestroyTileAt(x, y);
    }
    
    /// <summary>
    /// Clear tile at specified grid position using layered system
    /// Used for clean tile removal operations (e.g., player death, object destruction)
    /// NOTE: This only clears logical data, does not destroy GameObjects
    /// </summary>
    public void ClearTile(int x, int y)
    {
        if (layeredGrid != null && layeredGrid.IsValidPosition(x, y))
        {
            // Get objects from all layers and clear references
            GameObject actor = layeredGrid.GetActorAt(x, y);
            GameObject bomb = layeredGrid.GetBombAt(x, y);
            GameObject item = layeredGrid.GetItemAt(x, y);
            GameObject effect = layeredGrid.GetEffectAt(x, y);
            
            // Remove from layers (this doesn't destroy GameObjects)
            if (actor != null) layeredGrid.RemoveActor(actor, x, y);
            if (bomb != null) layeredGrid.RemoveBomb(bomb, x, y);
            if (item != null) layeredGrid.RemoveItem(item, x, y);
            if (effect != null) layeredGrid.RemoveEffect(effect, x, y);
            
        }
        else
        {
        }
    }
    
    /// <summary>
    /// Clean up null references in containers - removes destroyed GameObjects from hierarchy
    /// </summary>
    public void CleanupNullReferences()
    {
        // Debug.Log("[LevelLoader] CleanupNullReferences - Starting cleanup");
        
        int cleanedObjects = 0;
        
        // Clean collectibles container
        if (collectiblesContainer != null)
        {
            for (int i = collectiblesContainer.childCount - 1; i >= 0; i--)
            {
                Transform child = collectiblesContainer.GetChild(i);
                if (child == null || child.gameObject == null)
                {
                    // Debug.Log($"[LevelLoader] Found null child at index {i} in collectibles container");
                    cleanedObjects++;
                }
            }
        }
        
        // Clean enemies container
        if (enemiesContainer != null)
        {
            for (int i = enemiesContainer.childCount - 1; i >= 0; i--)
            {
                Transform child = enemiesContainer.GetChild(i);
                if (child == null || child.gameObject == null)
                {
                    // Debug.Log($"[LevelLoader] Found null child at index {i} in enemies container");
                    cleanedObjects++;
                }
            }
        }
        
        // Clean other containers as needed
        Transform[] containers = { effectsContainer, projectilesContainer };
        string[] containerNames = { "effects", "projectiles" };
        
        for (int c = 0; c < containers.Length; c++)
        {
            if (containers[c] != null)
            {
                for (int i = containers[c].childCount - 1; i >= 0; i--)
                {
                    Transform child = containers[c].GetChild(i);
                    if (child == null || child.gameObject == null)
                    {
                        // Debug.Log($"[LevelLoader] Found null child at index {i} in {containerNames[c]} container");
                        cleanedObjects++;
                    }
                }
            }
        }
        
        // Debug.Log($"[LevelLoader] CleanupNullReferences completed - cleaned {cleanedObjects} null references");
    }
    
    public Vector2Int WorldToGrid(Vector3 worldPosition)
    {
        return new Vector2Int(
            Mathf.FloorToInt(worldPosition.x / tileSize + 0.5f),
            Height - 1 - Mathf.FloorToInt(worldPosition.y / tileSize + 0.5f)
        );
    }
    
    public Vector3 GridToWorld(Vector2Int gridPosition)
    {
        return new Vector3(
            gridPosition.x * tileSize,
            (Height - gridPosition.y - 1) * tileSize,
            0
        );
    }

    //=========================================================================
    // LEVEL LOADING CORE FUNCTIONALITY
    //=========================================================================
    
    // Multi-level sequence management has been moved to LevelSequencer.cs
    // LevelLoader now focuses solely on level generation and loading
    
    /// <summary>
    /// CENTRALIZED TILE CREATION - Create any tile type at specified position
    /// This is the main method for creating tiles in the game
    /// </summary>
    public bool CreateTileAt(int x, int y, TileType type)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
        {
            Debug.LogWarning($"[LevelLoader] CreateTileAt position out of bounds: ({x}, {y})");
            return false;
        }
        
        // Check if position is occupied using layered system
        if (layeredGrid != null)
        {
            var objects = layeredGrid.GetAllObjectsAt(x, y);
            if (objects.Count > 0)
            {
                Debug.LogWarning($"[LevelLoader] CreateTileAt position ({x}, {y}) is already occupied by: {objects[0].name}");
                return false;
            }
        }
        
        if (prefabMap.TryGetValue(type, out var tileBasePrefab))
        {
            if (tileBasePrefab != null)
            {
                Vector3 pos = new Vector3(x * tileSize, (Height - y - 1) * tileSize, 0);
                Transform parentContainer = GetContainerForTileType(type);
                TileBase newTile = Instantiate(tileBasePrefab, pos, Quaternion.identity, parentContainer);
                
                // Debug.Log($"[LevelLoader] CreateTileAt - Created {type} at ({x}, {y}): {newTile.name}");
                
                // Setup tile visual
                if (spriteDatabase != null)
                {
                    newTile.SetVisual(spriteDatabase.GetSprite(type));
                }
                
                // Initialize tile with position
                (newTile as IInitializable)?.Init(x, y);
                
                // Note: Layered system handles placement automatically via PlaceTileInLayer
                
                // ML-Agent and special tracking
                UpdateMLAgentTracking(type, newTile.gameObject);
                
                // Debug.Log($"[LevelLoader] CreateTileAt - Successfully created and registered {type} at ({x}, {y})");
                return true;
            }
        }
        
        Debug.LogWarning($"[LevelLoader] No prefab found for {type} at ({x}, {y})");
        return false;
    }
    
    /// <summary>
    /// CENTRALIZED TILE DESTRUCTION - Remove any tile type at specified position using layered system
    /// This is the main method for destroying tiles in the game
    /// </summary>
    public bool DestroyTileAt(int x, int y)
    {
        if (layeredGrid == null || !layeredGrid.IsValidPosition(x, y))
        {
            Debug.LogWarning($"[LevelLoader] DestroyTileAt position invalid: ({x}, {y})");
            return false;
        }
        
        // Check all layers for objects to destroy
        GameObject actor = layeredGrid.GetActorAt(x, y);
        GameObject bomb = layeredGrid.GetBombAt(x, y);
        GameObject item = layeredGrid.GetItemAt(x, y);
        GameObject effect = layeredGrid.GetEffectAt(x, y);
        
        GameObject tileObject = actor ?? bomb ?? item ?? effect;
        
        if (tileObject == null)
        {
            // Debug.Log($"[LevelLoader] DestroyTileAt - No tile to destroy at ({x}, {y})");
            return true; // Not an error, position is already empty
        }
        
        var tileBase = tileObject.GetComponent<TileBase>();
        TileType tileType = tileBase != null ? tileBase.TileType : TileType.Unknown;
        
        // Debug.Log($"[LevelLoader] DestroyTileAt - Destroying {tileType} at ({x}, {y}): {tileObject.name}");
        
        // Remove from layered system and tracking lists
        if (actor != null)
        {
            layeredGrid.RemoveActor(actor, x, y);
            
            if (enemies.Contains(actor))
            {
                enemies.Remove(actor);
                OnEnemyListChanged?.Invoke();
            }
        }
        
        if (bomb != null)
        {
            layeredGrid.RemoveBomb(bomb, x, y);
        }
        
        if (item != null)
        {
            layeredGrid.RemoveItem(item, x, y);
            
            if (collectibles.Contains(item))
            {
                collectibles.Remove(item);
                OnCollectibleListChanged?.Invoke();
            }
        }
        
        if (effect != null)
        {
            layeredGrid.RemoveEffect(effect, x, y);
        }
        
        // Handle special cases
        if (tileType == TileType.Gate && exitObject == tileObject)
        {
            exitObject = null;
        }
        
        // Destroy the visual GameObject
        Destroy(tileObject);
        
        // Debug.Log($"[LevelLoader] DestroyTileAt - Successfully destroyed {tileType} at ({x}, {y})");
        return true;
    }
    
    /// <summary>
    /// CENTRALIZED PLAYER SPAWNING - Create player at spawn position ONLY ONCE
    /// This is the ONLY method that should create/spawn players in the game
    /// </summary>
    public void CreatePlayerAtSpawn()
    {
        CreatePlayerUsingService();
    }

    /// <summary>
    /// Try to move an object in the layered grid system
    /// </summary>
    public bool TryMoveObject(GameObject obj, int fromX, int fromY, int toX, int toY)
    {
        if (layeredGrid == null || !layeredGrid.IsValidPosition(toX, toY))
        {
            return false;
        }
        
        // Check if destination is walkable
        if (!layeredGrid.IsWalkable(toX, toY, obj))
        {
            return false;
        }
        
        // Determine object type and move in appropriate layer
        var tileBase = obj.GetComponent<TileBase>();
        if (tileBase != null)
        {
            switch (tileBase.TileType)
            {
                case TileType.Player:
                case TileType.Enemy:
                case TileType.EnemyShooter:
                    bool moveSuccess = layeredGrid.MoveActor(obj, fromX, fromY, toX, toY);
                    if (moveSuccess)
                    {
                        // Update object transform (LayeredGridService doesn't handle Unity transforms)
                        obj.transform.position = layeredGrid.GridToWorld(toX, toY);
                    }
                    return moveSuccess;
                    
                default:
                    Debug.LogWarning($"[LevelLoader] Unknown object type for movement: {tileBase.TileType}");
                    return false;
            }
        }
        
        return false;
    }
    
    /// <summary>
    /// Update ML-Agent tracking lists
    /// </summary>
    private void UpdateMLAgentTracking(TileType type, GameObject gameObj)
    {
        switch (type)
        {
            case TileType.Enemy:
            case TileType.EnemyShooter:
                enemies.Add(gameObj);
                break;
                
            case TileType.Coin:
            case TileType.Health:
                collectibles.Add(gameObj);
                break;
                
            case TileType.Gate:
                exitObject = gameObj;
                break;
        }
    }
    
    /// <summary>
    /// Auto-discover prefabs from Resources folder and map by TileType
    /// </summary>
    private void AutoDiscoverPrefabs()
    {
        // Load TileBase prefabs from Resources/Prefabs
        TileBase[] allTilePrefabs = Resources.LoadAll<TileBase>("Prefabs");
        
        if (allTilePrefabs.Length == 0)
        {
            allTilePrefabs = Resources.LoadAll<TileBase>("");
        }
        
        foreach (var prefab in allTilePrefabs)
        {
            if (prefab != null)
            {
                TileType prefabType = prefab.TileType;
                
                if (prefabMap.ContainsKey(prefabType))
                {
                    Debug.LogWarning($"[LevelLoader] Duplicate prefab for {prefabType}: keeping {prefabMap[prefabType].name}, ignoring {prefab.name}");
                    continue;
                }
                
                prefabMap.Add(prefabType, prefab);
            }
        }
        
        // Report missing prefabs for essential tile types
        var allTileTypes = System.Enum.GetValues(typeof(TileType)).Cast<TileType>();
        foreach (var tileType in allTileTypes)
        {
            if (tileType != TileType.Unknown && tileType != TileType.Empty && !prefabMap.ContainsKey(tileType))
            {
                Debug.LogWarning($"[LevelLoader] Missing prefab for {tileType}");
            }
        }
    }
    
    #region SERVICE-BASED METHODS
    
    /// <summary>
    /// Clear all tiles using service layer
    /// </summary>
    private void ClearAllTilesUsingServices()
    {
        // Clear TurnManager registrations first to prevent null reference issues
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.ClearAllRegistersExceptPlayer();
        }
        
        // Clear player using PlayerService
        playerService?.DestroyCurrentPlayer();
        
        // Clear layered grid system - LayeredGridService now handles GameObject destruction internally
        if (layeredGrid != null)
        {
            layeredGrid.ClearAllLayers();
        }
        
        // Clear containers using ContainerService
        containerService?.ClearCurrentContainers();
        
        // Manual cleanup of any orphaned explosion objects not tracked by layered grid
        var orphanedExplosions = FindObjectsOfType<MovingExplosion>();
        var orphanedExplosionTiles = FindObjectsOfType<ExplosionTile>();
        
        foreach (var explosion in orphanedExplosions)
        {
            if (explosion != null)
            {
                Destroy(explosion.gameObject);
            }
        }
        
        foreach (var explosionTile in orphanedExplosionTiles)
        {
            if (explosionTile != null)
            {
                Destroy(explosionTile.gameObject);
            }
        }
        
        // Clear ML-Agent tracking lists
        enemies.Clear();
        collectibles.Clear();
        exitObject = null;
        playerObject = null;
        
        // Clear legacy container references
        wallsContainer = null;
        breakablesContainer = null;
        gatesContainer = null;
        enemiesContainer = null;
        collectiblesContainer = null;
        effectsContainer = null;
        projectilesContainer = null;
        gridParent = null;
        dynamicParent = null;
    }
    
    /// <summary>
    /// Create map visual using modern service approach
    /// </summary>
    private void CreateMapVisualUsingServices()
    {
        // Create visual objects from level data
        if (currentLevelData != null && currentLevelData.grid != null)
        {
            CreateVisualTiles();
        }
    }
    
    /// <summary>
    /// Internal method to create player using PlayerService
    /// </summary>
    private void CreatePlayerUsingService()
    {
        if (playerService == null)
        {
            Debug.LogError("[LevelLoader] PlayerService is null! Cannot create player.");
            return;
        }
        
        // Use LayeredGridService coordinate conversion for consistency
        Vector3 playerPos = layeredGrid.GridToWorld(playerStartX, playerStartY);
        Vector2Int gridPos = new Vector2Int(playerStartX, playerStartY);
        
        // Create player using service
        var playerController = playerService.CreatePlayerAtPosition(gridPos, playerPos);
        if (playerController != null)
        {
            playerObject = playerController.gameObject;
            
            // Notify TurnManager for ML-Agent registration
            if (TurnManager.Instance != null && PlayerAgentManager.Instance != null)
            {
                try 
                {
                    if (PlayerAgentManager.Instance.IsMLAgentActive())
                    {
                        TurnManager.Instance.Register(PlayerAgentManager.Instance);
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[PLAYER_INIT] Error ensuring TurnManager registration: {e.Message}");
                }
            }
        }
    }
    
    #endregion
}