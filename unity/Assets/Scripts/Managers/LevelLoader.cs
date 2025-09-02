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
    
    // --- Component References ---
    private HoudiniLevelParser levelParser;
    
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
    
    // --- Runtime Container References ---
    [Header("Level Content Containers")]
    public Transform levelContentParent; // [LEVEL CONTENT] root container
    
    // Current level-specific containers
    private Transform currentLevelContainer; // Current level's root container
    private Transform currentStaticContainer; // Static tiles (walls, gates)
    private Transform currentDestructibleContainer; // Destructible tiles (breakables)  
    private Transform currentDynamicContainer; // Dynamic objects (enemies, items)
    
    // Legacy containers (kept for compatibility)
    public Transform gridParent;         // Grid tiles container  
    public Transform dynamicParent;      // Dynamic objects container
    public Transform wallsContainer;     // Walls container
    public Transform breakablesContainer; // Breakables container
    public Transform gatesContainer;     // Gates container
    public Transform enemiesContainer;   // Enemies container
    public Transform collectiblesContainer; // Collectibles container
    public Transform effectsContainer;   // Effects container
    public Transform projectilesContainer; // Projectiles container
    
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
        // Debug.LogWarning(mapOutput);
    }
    void Awake()
    {
        if (instance != null && instance != this)
        {
            // Debug.LogWarning("[LevelLoader] Multiple LevelLoader instances detected. Destroying duplicate component only.");
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
        // Debug.Log($"[LevelLoader] Singleton instance set to: {this.gameObject.name}");

        // Component references - find HoudiniLevelParser (Singleton or scene)
        levelParser = HoudiniLevelParser.Instance;
        if (levelParser == null)
        {
            levelParser = FindObjectOfType<HoudiniLevelParser>();
            if (levelParser == null)
            {
                levelParser = gameObject.AddComponent<HoudiniLevelParser>();
                // Debug.Log("[LevelLoader] HoudiniLevelParser component automatically added to LevelLoader");
            }
            else
            {
                // Debug.Log("[LevelLoader] HoudiniLevelParser found in scene");
            }
        }
        else
        {
            // Debug.Log("[LevelLoader] HoudiniLevelParser found via Singleton");
        }
        
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
    
    /// <summary>
    /// Helper method to get full hierarchy path for debugging
    /// </summary>
    private string GetFullPath(Transform transform)
    {
        if (transform == null) return "NULL";
        
        string path = transform.name;
        Transform parent = transform.parent;
        
        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }
        
        return path;
    }
    
    /// <summary>
    /// Helper to create container if it doesn't exist
    /// </summary>
    private void CreateTileContainer(ref Transform container, string containerName, Transform parent)
    {
        if (container == null)
        {
            GameObject containerGO = new GameObject(containerName + " Container");
            containerGO.transform.SetParent(parent);
            container = containerGO.transform;
        }
    }
    
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
    
    #if UNITY_EDITOR
    [ContextMenu("Refresh Level Files")]
    public void RefreshLevelFilesInEditor()
    {
        ScanForLevelFiles();
        EditorUtility.SetDirty(this);
    }
    #endif
    
    /// <summary>
    /// Levels dizinini tarar ve .ini dosyalarını bulur
    /// </summary>
    public void ScanForLevelFiles()
    {
        availableLevels.Clear();
        
        #if UNITY_EDITOR
        // Editor modunda AssetDatabase kullan
        ScanLevelsWithAssetDatabase();
        #else
        // Build'de Resources veya StreamingAssets kullan
        ScanLevelsFromResources();
        #endif
        
        // Level'ları sırala (level numarasına göre, sonra versiyona göre)
        availableLevels.Sort((a, b) => {
            int levelCompare = a.levelNumber.CompareTo(b.levelNumber);
            return levelCompare != 0 ? levelCompare : string.Compare(a.version, b.version, System.StringComparison.Ordinal);
        });
        
        // Debug.Log($"[LevelLoader] Found {availableLevels.Count} level files");
    }
    
    #if UNITY_EDITOR
    private void ScanLevelsWithAssetDatabase()
    {
        // Assets/Levels dizinindeki tüm .txt dosyalarını bul
        string[] guids = AssetDatabase.FindAssets("LEVEL_ t:TextAsset", new[] { levelsDirectoryPath });
        
        // Debug.Log($"[LevelLoader] Scanning directory: {levelsDirectoryPath}");
        // Debug.Log($"[LevelLoader] Found {guids.Length} files with 'LEVEL_' in name");
        
        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            string fileName = Path.GetFileNameWithoutExtension(assetPath);
            
            // Debug.Log($"[LevelLoader] Checking file: {assetPath}");
            
            // LEVEL_XXXX pattern kontrolü
            if (IsValidLevelFileName(fileName))
            {
                // .ini dosyalarını TextAsset olarak yükle
                TextAsset textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
                if (textAsset != null)
                {
                    var levelEntry = ParseLevelFileName(fileName, assetPath, textAsset);
                    availableLevels.Add(levelEntry);
                    // Debug.Log($"[LevelLoader] Successfully loaded level: {fileName}");
                }
                else
                {
                    // Debug.LogWarning($"[LevelLoader] Could not load as TextAsset: {assetPath}");
                }
            }
            else
            {
                // Debug.Log($"[LevelLoader] File name doesn't match pattern: {fileName}");
            }
        }
    }
    #endif
    
    private void ScanLevelsFromResources()
    {
        // Runtime için Resources klasöründen yükleme
        // Bu implementasyon gerekirse daha sonra genişletilebilir
        TextAsset[] levelAssets = Resources.LoadAll<TextAsset>("Levels");
        
        foreach (TextAsset asset in levelAssets)
        {
            if (IsValidLevelFileName(asset.name))
            {
                string assetPath = "Resources/Levels/" + asset.name;
                var levelEntry = ParseLevelFileName(asset.name, assetPath, asset);
                availableLevels.Add(levelEntry);
            }
        }
    }
    
    /// <summary>
    /// Dosya adının LEVEL_XXXX formatında olup olmadığını kontrol eder
    /// </summary>
    private bool IsValidLevelFileName(string fileName)
    {
        // LEVEL_0001_v1.0.0_v4.3 gibi formatları kabul et
        return fileName.StartsWith("LEVEL_") && fileName.Contains("_v");
    }
    
    /// <summary>
    /// Level dosya adını parse ederek bilgileri çıkarır
    /// </summary>
    private LevelFileEntry ParseLevelFileName(string fileName, string fullPath, TextAsset textAsset)
    {
        var entry = new LevelFileEntry
        {
            fileName = fileName,
            fullPath = fullPath,
            textAsset = textAsset,
            levelNumber = 1,
            version = "1.0.0"
        };
        
        try
        {
            // LEVEL_0001_v1.0.0_v4.3 formatını parse et
            string[] parts = fileName.Split('_');
            
            if (parts.Length >= 2)
            {
                // Level numarasını çıkar (LEVEL_0001 -> 1)
                string levelNumberStr = parts[1];
                if (int.TryParse(levelNumberStr, out int levelNumber))
                {
                    entry.levelNumber = levelNumber;
                }
            }
            
            if (parts.Length >= 3)
            {
                // Versiyon bilgisini çıkar (v1.0.0 -> 1.0.0)
                string versionStr = parts[2];
                if (versionStr.StartsWith("v"))
                {
                    entry.version = versionStr.Substring(1);
                }
            }
        }
        catch (System.Exception e)
        {
            // Debug.LogWarning($"[LevelLoader] Failed to parse level file name '{fileName}': {e.Message}");
        }
        
        return entry;
    }
    
    /// <summary>
    /// Seçilen level'ı yükler (HoudiniLevelImporter kullanarak)
    /// </summary>
    public void LoadSelectedLevel()
    {
        // Debug.Log($"[LevelLoader] LoadSelectedLevel called - Available levels: {availableLevels.Count}");
        
        if (availableLevels.Count == 0)
        {
            Debug.LogError("[LevelLoader] No level files found! Re-scanning...");
            ScanForLevelFiles();
            if (availableLevels.Count == 0)
            {
                Debug.LogError("[LevelLoader] Still no level files after re-scan!");
                return;
            }
        }
        
        // Index sınırlarını kontrol et
        selectedLevelIndex = Mathf.Clamp(selectedLevelIndex, 0, availableLevels.Count - 1);
        
        var selectedLevel = availableLevels[selectedLevelIndex];
        
        // Debug.Log($"[LevelLoader] Loading level: {selectedLevel.fileName}");
        
        // Use LevelImporter for organized level loading
        if (LevelImporter.Instance != null)
        {
            // Debug.Log("[LevelLoader] Using LevelImporter for data import");
            currentLevelData = LevelImporter.Instance.ImportLevel(selectedLevel.textAsset);
            
            if (currentLevelData == null)
            {
                Debug.LogError("[LevelLoader] LevelImporter returned null data!");
                return;
            }
            
            // Now handle level creation ourselves
            LoadFromLevelData(currentLevelData);
        }
        else
        {
            // Fallback to direct parsing if LevelImporter not available
            if (levelParser == null)
            {
                Debug.LogError("[LevelLoader] HoudiniLevelParser component not found!");
                return;
            }
            
            // Debug.LogWarning("[LevelLoader] LevelImporter not found - using fallback direct loading");
            currentLevelData = levelParser.ParseLevelData(selectedLevel.textAsset);
            if (currentLevelData != null)
            {
                LoadLevelFromHoudiniData(currentLevelData);
            }
            else
            {
                Debug.LogError($"[LevelLoader] Failed to parse level data: {selectedLevel.fileName}");
            }
        }
    }
    
    /// <summary>
    /// Level seçimini değiştirir
    /// </summary>
    public void SelectLevel(int index)
    {
        if (index >= 0 && index < availableLevels.Count)
        {
            selectedLevelIndex = index;
            // Debug.Log($"[LevelLoader] Selected level: {availableLevels[index].fileName}");
        }
    }
    
    /// <summary>
    /// Mevcut level listesini döndürür
    /// </summary>
    public List<LevelFileEntry> GetAvailableLevels()
    {
        return new List<LevelFileEntry>(availableLevels);
    }
    
    /// <summary>
    /// Seçilen level bilgisini döndürür
    /// </summary>
    public LevelFileEntry GetSelectedLevel()
    {
        if (selectedLevelIndex >= 0 && selectedLevelIndex < availableLevels.Count)
        {
            return availableLevels[selectedLevelIndex];
        }
        return new LevelFileEntry();
    }
    
    /// <summary>
    /// Sonraki level'a geçer (progression için)
    /// </summary>
    public bool SelectNextLevel()
    {
        if (selectedLevelIndex < availableLevels.Count - 1)
        {
            SelectLevel(selectedLevelIndex + 1);
            LoadSelectedLevel();
            return true;
        }
        // Debug.Log("[LevelLoader] Already at the last level");
        return false; // Son level'dayız
    }
    
    /// <summary>
    /// Önceki level'a geçer
    /// </summary>
    public bool SelectPreviousLevel()
    {
        if (selectedLevelIndex > 0)
        {
            SelectLevel(selectedLevelIndex - 1);
            LoadSelectedLevel();
            return true;
        }
        // Debug.Log("[LevelLoader] Already at the first level");
        return false; // İlk level'dayız
    }
    
    /// <summary>
    /// Belirli bir level numarasına göre level seçer
    /// </summary>
    public bool SelectLevelByNumber(int levelNumber)
    {
        int index = availableLevels.FindIndex(level => level.levelNumber == levelNumber);
        if (index >= 0)
        {
            SelectLevel(index);
            LoadSelectedLevel();
            return true;
        }
        // Debug.LogWarning($"[LevelLoader] Level {levelNumber} not found!");
        return false;
    }
    
    /// <summary>
    /// Belirli level numarası ve versiyona göre level seçer
    /// </summary>
    public bool SelectLevelByNumberAndVersion(int levelNumber, string version)
    {
        int index = availableLevels.FindIndex(level => 
            level.levelNumber == levelNumber && level.version == version);
        if (index >= 0)
        {
            SelectLevel(index);
            LoadSelectedLevel();
            return true;
        }
        // Debug.LogWarning($"[LevelLoader] Level {levelNumber} v{version} not found!");
        return false;
    }
    
    /// <summary>
    /// İlk level'a döner
    /// </summary>
    public void ResetToFirstLevel()
    {
        if (availableLevels.Count > 0)
        {
            SelectLevel(0);
            LoadSelectedLevel();
            // Debug.Log("[LevelLoader] Reset to first level");
        }
    }
    
    /// <summary>
    /// Son level'a geçer
    /// </summary>
    public void SelectLastLevel()
    {
        if (availableLevels.Count > 0)
        {
            SelectLevel(availableLevels.Count - 1);
            LoadSelectedLevel();
            // Debug.Log("[LevelLoader] Selected last level");
        }
    }
    
    /// <summary>
    /// Toplam level sayısını döndürür
    /// </summary>
    public int GetTotalLevelCount()
    {
        return availableLevels.Count;
    }
    
    /// <summary>
    /// Mevcut level indeksini döndürür (0-based)
    /// </summary>
    public int GetCurrentLevelIndex()
    {
        return selectedLevelIndex;
    }
    
    /// <summary>
    /// Mevcut level numarasını döndürür (1-based)
    /// </summary>
    public int GetCurrentLevelNumber()
    {
        var selectedLevel = GetSelectedLevel();
        return selectedLevel.levelNumber;
    }
    
    /// <summary>
    /// Level progression için - sonraki level var mı?
    /// </summary>
    public bool HasNextLevel()
    {
        return selectedLevelIndex < availableLevels.Count - 1;
    }
    
    /// <summary>
    /// Level progression için - önceki level var mı?
    /// </summary>
    public bool HasPreviousLevel()
    {
        return selectedLevelIndex > 0;
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
        
        // Debug.Log($"[LevelLoader] HoudiniData dimensions: {levelData.gridWidth}x{levelData.gridHeight}");
        // Debug.Log($"[LevelLoader] HoudiniData grid array dimensions: {levelData.grid?.GetLength(0)}x{levelData.grid?.GetLength(1)}");
        
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
    
    
    // CreateMapVisual metodunuz neredeyse hiç değişmeden çalışmaya devam edecek!
    // Sadece oyuncu oluşturma mantığını en sona taşıdık.
    void CreateMapVisual()
    {
        // Debug.Log($"[LevelLoader] *** CreateMapVisual CALLED *** - Dimensions: {Width}x{Height}");
        // Debug.Log($"[LevelLoader] Call Stack:\n{System.Environment.StackTrace}");
        
        // Clear previous visual objects
        // Debug.Log($"[LevelLoader] *** CreateMapVisual CALLED *** - Dimensions: {Width}x{Height}");

        // Create visual objects from level data
        if (currentLevelData != null && currentLevelData.grid != null)
        {
            CreateVisualTiles();
        }

        // Player creation with fresh instantiation
        CreatePlayerAtSpawn();
        
        // Debug.Log($"[LevelLoader] CreateMapVisual completed successfully");
        
        // Debug: Log TurnManager state after level creation
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
        Debug.Log($"[🎨 CREATE_VISUAL] Starting visual tiles creation for level: {currentLevelData?.levelName} ({Width}x{Height})");
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
                    
                    //Debug.Log($"[LevelLoader] Created {type} at ({x},{y}) in layered system");
                }
                else
                {
                    Debug.LogWarning($"[LevelLoader] No prefab found for {type} at ({x}, {y})");
                }
            }
        }
        
        Debug.Log($"[🎨 CREATE_VISUAL] ✅ Completed visual tiles creation for level: {currentLevelData?.levelName}");
        Debug.Log($"[🎨 CREATE_VISUAL] Created {createdBreakables} breakables, {createdEnemies} enemies, {createdCoins} coins, {createdHealth} health items");
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
        bool isClean = (cleanCount == sampleCount);
        return isClean;
    }
    
    /// <summary>
    /// Create level-specific container hierarchy: Level > Category > Objects
    /// </summary>
    private void CreateLevelSpecificContainers(HoudiniLevelData levelData)
    {
        string levelName = levelData.levelName ?? levelData.levelId ?? "UnknownLevel";
        
        // Create main level container
        GameObject levelContainerObj = new GameObject($"Level_{levelName}");
        currentLevelContainer = levelContainerObj.transform;
        currentLevelContainer.SetParent(levelContentParent);
        
        // Create category containers under level container
        GameObject staticContainerObj = new GameObject("Static");
        currentStaticContainer = staticContainerObj.transform;
        currentStaticContainer.SetParent(currentLevelContainer);
        
        GameObject destructibleContainerObj = new GameObject("Destructible");  
        currentDestructibleContainer = destructibleContainerObj.transform;
        currentDestructibleContainer.SetParent(currentLevelContainer);
        
        GameObject dynamicContainerObj = new GameObject("Dynamic");
        currentDynamicContainer = dynamicContainerObj.transform;
        currentDynamicContainer.SetParent(currentLevelContainer);
        
        // Create sub-categories under Static
        CreateTileContainer(ref wallsContainer, "Walls", currentStaticContainer);
        CreateTileContainer(ref gatesContainer, "Gates", currentStaticContainer);
        
        // Create sub-categories under Destructible
        CreateTileContainer(ref breakablesContainer, "Breakables", currentDestructibleContainer);
        
        // Create sub-categories under Dynamic  
        CreateTileContainer(ref enemiesContainer, "Enemies", currentDynamicContainer);
        CreateTileContainer(ref collectiblesContainer, "Collectibles", currentDynamicContainer);
        CreateTileContainer(ref effectsContainer, "Effects", currentDynamicContainer);
        CreateTileContainer(ref projectilesContainer, "Projectiles", currentDynamicContainer);
        
        // Update legacy references for compatibility
        gridParent = currentStaticContainer;
        dynamicParent = currentDynamicContainer;
    }
    
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
        Transform bombParent = dynamicParent ?? levelContentParent;
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
        // Debug.LogWarning("[LevelLoader] RemoveEnemy is deprecated. Use RemoveTileAt() instead.");
        
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
            // Debug.LogWarning($"[LevelLoader] RemoveEnemy - Enemy {enemy.name} not found in enemies list");
        }
    }
    
    /// <summary>
    /// DEPRECATED: Use RemoveTileAt() instead for centralized tile management
    /// </summary>
    [System.Obsolete("Use RemoveTileAt() for centralized tile management")]
    public void RemoveCollectible(GameObject collectible)
    {
        // Debug.LogWarning("[LevelLoader] RemoveCollectible is deprecated. Use RemoveTileAt() instead.");
        
        if (collectible == null)
        {
            // Debug.LogWarning("[LevelLoader] RemoveCollectible - collectible is null!");
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
        // Debug.LogWarning("[LevelLoader] RemoveTileAt is deprecated. Use DestroyTileAt() instead.");
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
            
            // Debug.Log($"[LevelLoader] Cleared tile at ({x}, {y}) from layered system");
        }
        else
        {
            // Debug.LogWarning($"[LevelLoader] ClearTile called with invalid coordinates ({x}, {y})");
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
        Debug.Log($"[🎮 PLAYER_SPAWN] === Starting player creation for level: {currentLevelData?.levelName} ===");
        
        // Early validation
        if (playerPrefab == null)
        {
            Debug.LogError("[LevelLoader] playerPrefab is null! Please assign Player Prefab in Inspector.");
            return;
        }

        // Debug: Show expected vs actual spawn data
        Debug.Log($"[🎮 PLAYER_SPAWN] Level data player spawn: ({currentLevelData?.playerSpawn.x}, {currentLevelData?.playerSpawn.y})");
        Debug.Log($"[🎮 PLAYER_SPAWN] LevelLoader playerStart: ({playerStartX}, {playerStartY})");
        Debug.Log($"[🎮 PLAYER_SPAWN] Grid dimensions: {Width}x{Height}, TileSize: {tileSize}");

        // Use LayeredGridService coordinate conversion for consistency
        Vector3 playerPos = layeredGrid.GridToWorld(playerStartX, playerStartY);
        Debug.Log($"[🎮 PLAYER_SPAWN] LayeredGrid world position: {playerPos}");
        
        // Show alternative calculations for comparison
        Vector3 directPos = new Vector3(playerStartX * tileSize, playerStartY * tileSize, 0);
        Vector3 originalPos = new Vector3(playerStartX * tileSize, (Height - playerStartY - 1) * tileSize, 0);
        Debug.Log($"[🎮 PLAYER_SPAWN] Direct calculation: {directPos}");
        Debug.Log($"[🎮 PLAYER_SPAWN] Y-flip calculation: {originalPos}");
        
        // Verify the spawn position has correct tile type
        if (currentLevelData?.grid != null && 
            playerStartX < currentLevelData.grid.GetLength(0) && 
            playerStartY < currentLevelData.grid.GetLength(1))
        {
            char spawnChar = currentLevelData.grid[playerStartX, playerStartY];
            TileType spawnTileType = TileSymbols.DataSymbolToType(spawnChar);
            Debug.Log($"[🎮 PLAYER_SPAWN] Tile at spawn position ({playerStartX},{playerStartY}): '{spawnChar}' → {spawnTileType}");
        }
        
        // Since ClearAllTiles now destroys everything, we should have clean slate
        var allExistingPlayers = FindObjectsOfType<PlayerController>();
        if (allExistingPlayers.Length > 0)
        {
            Debug.LogWarning($"[🎮 FRESH_SPAWN] Found {allExistingPlayers.Length} existing players after cleanup - this shouldn't happen!");
            // Clean any remaining players
            foreach (var player in allExistingPlayers)
            {
                Debug.LogWarning($"[🎮 FRESH_SPAWN] Destroying leftover player: '{player.name}'");
                Destroy(player.gameObject);
            }
        }
        
        // FRESH CREATION: Always create new player since ClearAllTiles destroyed everything
        Debug.Log("[🎮 FRESH_CREATE] Creating completely new player instance");
        Transform playerParent = GetContainerForTileType(TileType.Player);
        Debug.Log($"[🎮 FRESH_CREATE] Player parent container: {playerParent?.name}");
        playerObject = Instantiate(playerPrefab, playerPos, Quaternion.identity, playerParent);
        Debug.Log($"[🎮 FRESH_CREATE] New Player instance created: {playerObject.name} at {playerPos}");
        
        // Setup player components
        var playerController = playerObject.GetComponent<PlayerController>();
        var playerTileBase = playerObject.GetComponent<TileBase>();
        
        // Publish player spawned event
        if (playerController != null)
        {
            Vector2Int gridPos = new Vector2Int(playerStartX, playerStartY);
            GameEventBus.Instance?.Publish(new PlayerSpawned(playerController, gridPos, playerPos));
        }
        
        if (playerTileBase != null && spriteDatabase != null)
        {
            playerTileBase.SetVisual(spriteDatabase.GetSprite(TileType.Player));
        }
        
        if (playerController != null)
        {
            Debug.Log($"[🎮 PLAYER_INIT] Initializing PlayerController with grid coords: ({playerStartX}, {playerStartY})");
            playerController.Init(playerStartX, playerStartY);
            
            // Verify initialization worked
            Debug.Log($"[🎮 PLAYER_INIT] PlayerController after init - X: {playerController.X}, Y: {playerController.Y}");
            
            // Register with GameManager
            if (GameManager.Instance != null)
            {
                GameManager.Instance.RegisterPlayer(playerController);
                Debug.Log($"[🎮 PLAYER_INIT] Registered with GameManager");
            }
            
            // PlayerAgentManager registration now handled via event bus (PlayerSpawned event)
                
                // Notify TurnManager to refresh ML-Agent registration after new player creation
                if (TurnManager.Instance != null)
                {
                    // Simple approach: Just register PlayerAgentManager directly if it's not already registered
                    var turnManager = TurnManager.Instance;
                    var playerAgentManager = PlayerAgentManager.Instance;
                    
                    // Check if PlayerAgentManager is in TurnManager's registered objects
                    bool isAlreadyRegistered = false;
                    try 
                    {
                        // Force TurnManager to register PlayerAgentManager if ML is active
                        if (playerAgentManager.IsMLAgentActive())
                        {
                            turnManager.Register(playerAgentManager);
                            Debug.Log($"[🎮 PLAYER_INIT] Ensured PlayerAgentManager registration with TurnManager");
                        }
                        else
                        {
                            Debug.Log($"[🎮 PLAYER_INIT] PlayerAgentManager ML not active, skipping TurnManager registration");
                        }
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"[🎮 PLAYER_INIT] Error ensuring TurnManager registration: {e.Message}");
                    }
                }
            }
            
            // Place in layered system
            if (layeredGrid != null)
            {
                bool placementSuccess = layeredGrid.PlaceActor(playerObject, playerStartX, playerStartY);
                Debug.Log($"[🎮 PLAYER_INIT] LayeredGrid placement result: {placementSuccess}");
            }
            
            Debug.Log($"[🎮 SPAWN_COMPLETE] Player initialized at grid({playerStartX}, {playerStartY}) with health {playerController.CurrentHealth}/{playerController.MaxHealth}");
            Debug.Log($"[🎮 SPAWN_COMPLETE] Player world position: {playerObject.transform.position}");
        }
        
        // Final verification - ensure only one PlayerController exists in scene
        var finalPlayerCount = FindObjectsOfType<PlayerController>().Length;
        if (finalPlayerCount != 1)
        {
            Debug.LogWarning($"[🎮 SPAWN_WARNING] Expected 1 PlayerController but found {finalPlayerCount}!");
        }
    }
    
    /// <summary>
    /// Atomic movement operation using layered system
    /// Returns true if movement successful, false if destination occupied
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
    /// Otomatik olarak prefab'ları Resource klasöründen keşfeder ve TileType'larına göre eşler
    /// </summary>
    private void AutoDiscoverPrefabs()
    {
        // Debug.Log("[LevelLoader] === AUTO PREFAB DISCOVERY ===");
        
        // Resources/Prefabs klasöründen tüm TileBase prefab'larını yükle
        TileBase[] allTilePrefabs = Resources.LoadAll<TileBase>("Prefabs");
        
        if (allTilePrefabs.Length == 0)
        {
            // Debug.LogWarning("[LevelLoader] No prefabs found in Resources/Prefabs. Trying root Resources folder...");
            allTilePrefabs = Resources.LoadAll<TileBase>("");
        }
        
        // Debug.Log($"[LevelLoader] Found {allTilePrefabs.Length} TileBase prefabs in Resources");
        
        foreach (var prefab in allTilePrefabs)
        {
            if (prefab != null)
            {
                TileType prefabType = prefab.TileType;
                
                // Duplicate mapping kontrolü
                if (prefabMap.ContainsKey(prefabType))
                {
                    Debug.LogWarning($"[LevelLoader] Duplicate prefab for {prefabType}: keeping {prefabMap[prefabType].name}, ignoring {prefab.name}");
                    continue;
                }
                
                prefabMap.Add(prefabType, prefab);
                // Debug.Log($"[LevelLoader] Auto-discovered: {prefabType} -> {prefab.name}");
            }
        }
        
        // Eksik prefab'ları rapor et
        var allTileTypes = System.Enum.GetValues(typeof(TileType)).Cast<TileType>();
        foreach (var tileType in allTileTypes)
        {
            if (tileType != TileType.Unknown && tileType != TileType.Empty && !prefabMap.ContainsKey(tileType))
            {
                Debug.LogWarning($"[LevelLoader] Missing prefab for {tileType}");
            }
        }
        
        // Debug.Log("[LevelLoader] ================================");
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