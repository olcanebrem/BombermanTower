using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.IO;
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
    private HoudiniLevelImporter levelImporter;
    
    // --- Level File Management ---
    [SerializeField] private List<LevelFileEntry> availableLevels = new List<LevelFileEntry>();
    [SerializeField] private int selectedLevelIndex = 0;
    [SerializeField] private string levelsDirectoryPath = "Assets/Levels";
    
    // --- Current Level Data ---
    private HoudiniLevelData currentLevelData; 
    
    public int Width { get; private set; }
    public int Height { get; private set; }
    
    public char[,] levelMap;
    public GameObject[,] tileObjects;
    private GameObject playerObject; // Runtime player instance reference
    private int playerStartX, playerStartY;
    
    // ML-Agent support - object tracking
    private List<GameObject> enemies = new List<GameObject>();
    private List<GameObject> collectibles = new List<GameObject>();
    private GameObject exitObject;
    
    // Events for cache invalidation
    public event System.Action OnEnemyListChanged;
    public event System.Action OnCollectibleListChanged;
    public void DebugPrintMap()
    {
        // TurnManager'dan o anki tur sayısını alarak log'u daha bilgilendirici yapalım.
        int currentTurn = (TurnManager.Instance != null) ? TurnManager.Instance.TurnCount : 0;
        
        // Konsolda daha kolay bulmak için bir başlık oluşturalım.
        string mapOutput = $"--- MANTIKSAL HARİTA DURUMU: TUR {currentTurn} ---\n"; // '\n' yeni bir satır başlatır.

        // Haritayı satır satır dolaşarak string'i oluşturalım.
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                // Her bir karakteri string'e ekle.
                mapOutput += levelMap[x, y];
            }
            // Her satırın sonunda bir alt satıra geç.
            mapOutput += "\n";
        }

        // Oluşturulan harita string'ini, konsolda öne çıkması için bir uyarı olarak yazdır.
        Debug.LogWarning(mapOutput);
    }
    void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning("[LevelLoader] Multiple LevelLoader instances detected. Destroying duplicate component only.");
            Destroy(this);
            return;
        }
        instance = this;

        // Component references - automatically find or create HoudiniLevelImporter
        levelImporter = GetComponent<HoudiniLevelImporter>();
        if (levelImporter == null)
        {
            levelImporter = gameObject.AddComponent<HoudiniLevelImporter>();
            Debug.Log("[LevelLoader] HoudiniLevelImporter component automatically added");
        }

        // Prefab sözlüğünü doldur
        prefabMap = new Dictionary<TileType, TileBase>();
        if (tilePrefabs != null)
        {
            foreach (var entry in tilePrefabs)
            {
                if (entry.prefab != null && !prefabMap.ContainsKey(entry.type))
                {
                    prefabMap.Add(entry.type, entry.prefab);
                }
            }
            Debug.Log($"[LevelLoader] Loaded {prefabMap.Count} tile prefab mappings");
        }
        else
        {
            Debug.LogWarning("[LevelLoader] tilePrefabs array is null! Please assign tile prefabs in Inspector.");
        }
        // Sprite database'i initialize et
        if (spriteDatabase != null)
        {
            spriteDatabase.Initialize(); // Veri tabanını hazırla
            Debug.Log("[LevelLoader] SpriteDatabase initialized");
        }
        else
        {
            Debug.LogWarning("[LevelLoader] spriteDatabase is null! Please assign SpriteDatabase in Inspector.");
        }
    }

    void Start()
    {
        ScanForLevelFiles();
        LoadSelectedLevel();
        CreateMapVisual();
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
        
        Debug.Log($"[LevelLoader] Found {availableLevels.Count} level files");
    }
    
    #if UNITY_EDITOR
    private void ScanLevelsWithAssetDatabase()
    {
        // Assets/Levels dizinindeki tüm .txt dosyalarını bul
        string[] guids = AssetDatabase.FindAssets("LEVEL_ t:TextAsset", new[] { levelsDirectoryPath });
        
        Debug.Log($"[LevelLoader] Scanning directory: {levelsDirectoryPath}");
        Debug.Log($"[LevelLoader] Found {guids.Length} files with 'LEVEL_' in name");
        
        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            string fileName = Path.GetFileNameWithoutExtension(assetPath);
            
            Debug.Log($"[LevelLoader] Checking file: {assetPath}");
            
            // LEVEL_XXXX pattern kontrolü
            if (IsValidLevelFileName(fileName))
            {
                // .ini dosyalarını TextAsset olarak yükle
                TextAsset textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
                if (textAsset != null)
                {
                    var levelEntry = ParseLevelFileName(fileName, assetPath, textAsset);
                    availableLevels.Add(levelEntry);
                    Debug.Log($"[LevelLoader] Successfully loaded level: {fileName}");
                }
                else
                {
                    Debug.LogWarning($"[LevelLoader] Could not load as TextAsset: {assetPath}");
                }
            }
            else
            {
                Debug.Log($"[LevelLoader] File name doesn't match pattern: {fileName}");
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
            Debug.LogWarning($"[LevelLoader] Failed to parse level file name '{fileName}': {e.Message}");
        }
        
        return entry;
    }
    
    /// <summary>
    /// Seçilen level'ı yükler (HoudiniLevelImporter kullanarak)
    /// </summary>
    public void LoadSelectedLevel()
    {
        if (availableLevels.Count == 0)
        {
            Debug.LogError("[LevelLoader] No level files found!");
            return;
        }
        
        // Index sınırlarını kontrol et
        selectedLevelIndex = Mathf.Clamp(selectedLevelIndex, 0, availableLevels.Count - 1);
        
        var selectedLevel = availableLevels[selectedLevelIndex];
        
        Debug.Log($"[LevelLoader] Loading level: {selectedLevel.fileName}");
        
        if (levelImporter == null)
        {
            Debug.LogError("[LevelLoader] HoudiniLevelImporter component not found!");
            return;
        }
        
        // HoudiniLevelImporter'dan level data al
        currentLevelData = levelImporter.LoadLevelData(selectedLevel.textAsset);
        if (currentLevelData != null)
        {
            LoadLevelFromHoudiniData(currentLevelData);
        }
        else
        {
            Debug.LogError($"[LevelLoader] Failed to parse level data: {selectedLevel.fileName}");
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
            Debug.Log($"[LevelLoader] Selected level: {availableLevels[index].fileName}");
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
        Debug.Log("[LevelLoader] Already at the last level");
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
        Debug.Log("[LevelLoader] Already at the first level");
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
        Debug.LogWarning($"[LevelLoader] Level {levelNumber} not found!");
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
        Debug.LogWarning($"[LevelLoader] Level {levelNumber} v{version} not found!");
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
            Debug.Log("[LevelLoader] Reset to first level");
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
            Debug.Log("[LevelLoader] Selected last level");
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

        // Set dimensions from Houdini data
        Width = levelData.gridWidth;
        Height = levelData.gridHeight;
        
        Debug.Log($"[LevelLoader] HoudiniData dimensions: {levelData.gridWidth}x{levelData.gridHeight}");
        Debug.Log($"[LevelLoader] HoudiniData grid array dimensions: {levelData.grid?.GetLength(0)}x{levelData.grid?.GetLength(1)}");
        
        // Copy grid data
        levelMap = new char[Width, Height];
        if (levelData.grid != null)
        {
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    if (x < levelData.grid.GetLength(0) && y < levelData.grid.GetLength(1))
                    {
                        levelMap[x, y] = levelData.grid[x, y];
                    }
                    else
                    {
                        levelMap[x, y] = TileSymbols.TypeToDataSymbol(TileType.Empty);
                        Debug.LogWarning($"[LevelLoader] Out of bounds access at ({x},{y}) - grid size is {levelData.grid.GetLength(0)}x{levelData.grid.GetLength(1)}");
                    }
                }
            }
        }
        
        // Set player spawn position
        playerStartX = levelData.playerSpawn.x;
        playerStartY = levelData.playerSpawn.y;
        
        // Count non-empty cells
        int nonEmptyCells = 0;
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                if (levelMap[x, y] != '.' && levelMap[x, y] != ' ')
                    nonEmptyCells++;
            }
        }
        
        // Combined debug info in single message
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine($"\n=== LEVEL LOADER DEBUG INFO ===");
        sb.AppendLine($"Level: {levelData.levelName} (v{levelData.version})");
        sb.AppendLine($"Dimensions: {Width}x{Height}");
        sb.AppendLine($"Player spawn: ({playerStartX}, {playerStartY})");
        sb.AppendLine($"Enemies: {levelData.enemyPositions.Count}");
        sb.AppendLine($"Non-empty cells: {nonEmptyCells}/{Width * Height}");
        sb.AppendLine($"Grid samples:");
        sb.AppendLine($"  Top-left: '{levelMap[0,0]}' | Top-right: '{levelMap[Width-1,0]}'");
        sb.AppendLine($"  Bottom-left: '{levelMap[0,Height-1]}' | Bottom-right: '{levelMap[Width-1,Height-1]}'");
        sb.AppendLine("==============================");
        
        Debug.Log(sb.ToString());
    }
    
    
    // CreateMapVisual metodunuz neredeyse hiç değişmeden çalışmaya devam edecek!
    // Sadece oyuncu oluşturma mantığını en sona taşıdık.
    void CreateMapVisual()
    {
        // 1. Referans haritalarımızı oluştur.
        tileObjects = new GameObject[Width, Height];

        // 2. JENERİK TILE'LARI OLUŞTURMA DÖNGÜSÜ
        // Bu döngü, oyuncu DIŞINDAKİ her şeyi oluşturur.
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                char symbolChar = levelMap[x, y];
                TileType type = TileSymbols.DataSymbolToType(symbolChar);

                // Boş kareleri ve oyuncunun başlangıç noktasını atla.
                // Oyuncu, bu döngü bittikten sonra özel olarak oluşturulacak.
                if (type == TileType.Empty || type == TileType.PlayerSpawn)
                {
                    continue;
                }

                // Prefab sözlüğünde bu tip için bir girdi var mı diye bak.
                if (prefabMap.TryGetValue(type, out var tileBasePrefab))
                {
                    if (tileBasePrefab != null)
                    {
                        Vector3 pos = new Vector3(x * tileSize, (Height - y - 1) * tileSize, 0);
                        TileBase newTile = Instantiate(tileBasePrefab, pos, Quaternion.identity, transform);
                        
                        // Yeni oluşturulan tile'ı kur.
                        if (spriteDatabase != null)
                        {
                            newTile.SetVisual(spriteDatabase.GetSprite(type));
                        }
                        (newTile as IInitializable)?.Init(x, y);
                        
                        // ML-Agent tracking - Add to appropriate lists
                        if (type == TileType.Enemy || type == TileType.EnemyShooter)
                        {
                            enemies.Add(newTile.gameObject);
                        }
                        else if (type == TileType.Coin || type == TileType.Health)
                        {
                            collectibles.Add(newTile.gameObject);
                        }
                        else if (type == TileType.Stairs)
                        {
                            exitObject = newTile.gameObject;
                        }
                        
                        // Tile array'ine ekle
                        tileObjects[x, y] = newTile.gameObject;
                    }
                    else
                    {
                        Debug.LogWarning($"[LevelLoader] TileBase prefab for {type} is null at position ({x}, {y})");
                    }
                }
                else
                {
                    Debug.LogWarning($"[LevelLoader] No prefab mapping found for TileType: {type} at position ({x}, {y})");
                }
            }
        }

        // --- 3. OYUNCUYU OLUŞTURMA BLOĞU ---
        // Tüm jenerik tile'lar oluşturulduktan sonra, oyuncuyu özel olarak ele al.

        // a) Oyuncunun başlangıç pozisyonunu hesapla.
        // Bu 'playerStartX' ve 'playerStartY' değişkenleri LoadLevelFromFile'da doldurulmuştu.
        Vector3 playerPos = new Vector3(playerStartX * tileSize, (Height - playerStartY - 1) * tileSize, 0);
        
        // b) Oyuncuyu, hesaplanan bu pozisyonda oluştur.
        // Singleton pattern: Varsa mevcut player'ı kullan
        if (PlayerController.Instance != null)
        {
            playerObject = PlayerController.Instance.gameObject;
            playerObject.transform.position = playerPos;
            Debug.Log("[LevelLoader] Using existing Singleton Player instance");
        }
        else if (playerObject == null)
        {
            if (playerPrefab != null)
            {
                playerObject = Instantiate(playerPrefab, playerPos, Quaternion.identity, transform);
                Debug.Log("[LevelLoader] New Singleton Player created");
            }
            else
            {
                Debug.LogError("[LevelLoader] playerPrefab is null! Please assign Player Prefab in Inspector.");
                // Create empty GameObject as fallback
                playerObject = new GameObject("Player (Missing Prefab)");
                playerObject.transform.position = playerPos;
                playerObject.transform.SetParent(transform);
            }
        }
        else
        {
            // Mevcut player'ı yeni pozisyona taşı
            playerObject.transform.position = playerPos;
            Debug.Log("[LevelLoader] Existing player repositioned");
        }
        
        // c) Gerekli bileşen referanslarını SADECE BİR KERE al.
        var playerController = playerObject.GetComponent<PlayerController>();
        var playerTileBase = playerObject.GetComponent<TileBase>();

        // d) Oyuncunun görselini ayarla.
        if (playerTileBase != null)
        {
            playerTileBase.SetVisual(spriteDatabase.GetSprite(TileType.Player));
        }

        // e) Oyuncunun mantığını kur ve diğer yöneticilere kaydettir.
        if (playerController != null)
        {
            playerController.Init(playerStartX, playerStartY);
            GameManager.Instance.RegisterPlayer(playerController);

            // Oyuncunun mantıksal haritasına kaydet.
            levelMap[playerStartX, playerStartY] = TileSymbols.TypeToDataSymbol(TileType.Player);
            tileObjects[playerStartX, playerStartY] = playerObject;
        }
        
        // f) Oyuncunun referansını, nesne haritasındaki doğru yere koy.
        tileObjects[playerStartX, playerStartY] = playerObject;
    }
        public void PlaceBombAt(int x, int y)
    {
        // GÜVENLİK KİLİDİ: Eğer bu kare bir şekilde doluysa, hiçbir şey yapma.
        if (TileSymbols.DataSymbolToType(levelMap[x, y]) != TileType.Empty)
        {
            Debug.LogWarning($"({x},{y}) dolu olduğu için bomba konulamadı. İçerik: {TileSymbols.DataSymbolToType(levelMap[x,y])}");
            return;
        }

        // Sözlükten Bomb prefabını al
        if (prefabMap.TryGetValue(TileType.Bomb, out var bombTilePrefab))
        {
            Vector3 pos = new Vector3(x * tileSize, (Height - y - 1) * tileSize, 0);
            TileBase newBomb = Instantiate(bombTilePrefab, pos, Quaternion.identity, transform);

            // Bombayı kur
            newBomb.SetVisual(spriteDatabase.GetSprite(TileType.Bomb));
            (newBomb as IInitializable)?.Init(x, y);
            
            // Haritaları GÜNCELLE
            tileObjects[x, y] = newBomb.gameObject; // Nesne haritasını güncelle
            levelMap[x, y] = TileSymbols.TypeToDataSymbol(TileType.Bomb); // Mantıksal haritayı güncelle
        }
        /// <summary>
        /// Mantıksal haritanın (levelMap) o anki durumunu konsola okunaklı bir şekilde yazdırır.
        /// Hata ayıklama için çok faydalıdır.
        /// </summary>
        /// <param name="turnNumber">Log'da gösterilecek olan mevcut tur sayısı.</param>
        
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
    
    public void RemoveEnemy(GameObject enemy)
    {
        if (enemies.Contains(enemy))
        {
            enemies.Remove(enemy);
            // Remove from grid tracking too
            Vector2Int gridPos = WorldToGrid(enemy.transform.position);
            if (tileObjects[gridPos.x, gridPos.y] == enemy)
            {
                tileObjects[gridPos.x, gridPos.y] = null;
                levelMap[gridPos.x, gridPos.y] = TileSymbols.TypeToDataSymbol(TileType.Empty);
            }
            
            // Notify listeners that enemy list changed
            OnEnemyListChanged?.Invoke();
        }
    }
    
    public void RemoveCollectible(GameObject collectible)
    {
        if (collectibles.Contains(collectible))
        {
            collectibles.Remove(collectible);
            // Remove from grid tracking too
            Vector2Int gridPos = WorldToGrid(collectible.transform.position);
            if (tileObjects[gridPos.x, gridPos.y] == collectible)
            {
                tileObjects[gridPos.x, gridPos.y] = null;
                levelMap[gridPos.x, gridPos.y] = TileSymbols.TypeToDataSymbol(TileType.Empty);
            }
            
            // Notify listeners that collectible list changed
            OnCollectibleListChanged?.Invoke();
        }
    }
    
    /// <summary>
    /// Clear tile at specified grid position - removes from both levelMap and tileObjects
    /// Used for clean tile removal operations (e.g., player death, object destruction)
    /// </summary>
    public void ClearTile(int x, int y)
    {
        if (x >= 0 && x < Width && y >= 0 && y < Height)
        {
            // Clear the levelMap data
            levelMap[x, y] = TileSymbols.TypeToDataSymbol(TileType.Empty);
            
            // Clear the object reference
            tileObjects[x, y] = null;
            
            Debug.Log($"[LevelLoader] Cleared tile at ({x}, {y})");
        }
        else
        {
            Debug.LogWarning($"[LevelLoader] ClearTile called with invalid coordinates ({x}, {y})");
        }
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
   
}