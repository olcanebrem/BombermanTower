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
    
    // --- Level File Management ---
    [SerializeField] private List<LevelFileEntry> availableLevels = new List<LevelFileEntry>();
    [SerializeField] private int selectedLevelIndex = 0;
    [SerializeField] private string levelsDirectoryPath = "Assets/Levels";
    
    // --- Seviye Verisi ---
    public TextAsset levelFile; 
    
    public int Width { get; private set; }
    public int Height { get; private set; }
    
    public char[,] levelMap;
    public GameObject[,] tileObjects;
    public GameObject playerObject;
    private int playerStartX, playerStartY;
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
            Destroy(gameObject);
            return;
        }
        instance = this;

        // Prefab sözlüğünü doldur
        prefabMap = new Dictionary<TileType, TileBase>();
        foreach (var entry in tilePrefabs)
        {
            if (entry.prefab != null && !prefabMap.ContainsKey(entry.type))
            {
                prefabMap.Add(entry.type, entry.prefab);
            }
        }
        spriteDatabase.Initialize(); // Veri tabanını hazırla
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
        // Assets/Levels dizinindeki tüm .ini dosyalarını bul
        string[] guids = AssetDatabase.FindAssets("*.ini", new[] { levelsDirectoryPath });
        
        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            string fileName = Path.GetFileNameWithoutExtension(assetPath);
            
            // LEVEL_XXXX pattern kontrolü
            if (IsValidLevelFileName(fileName))
            {
                TextAsset textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
                if (textAsset != null)
                {
                    var levelEntry = ParseLevelFileName(fileName, assetPath, textAsset);
                    availableLevels.Add(levelEntry);
                }
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
    /// Seçilen level'ı yükler
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
        levelFile = selectedLevel.textAsset;
        
        Debug.Log($"[LevelLoader] Loading level: {selectedLevel.fileName}");
        LoadLevelFromFile();
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
    void LoadLevelFromFile()
    {
        if (levelFile == null)
        {
            Debug.LogError("LevelLoader'a bir levelFile atanmamış!");
            return;
        }

        // 1. Dosyayı satırlara böl
        string[] lines = levelFile.text.Split('\n');

        // 2. Boyutları belirle
        Height = lines.Length;
        Width = 0;
        foreach (string line in lines)
        {
            // En uzun satırı genişlik olarak kabul et (düzgün format için)
            if (line.Length > Width)
            {
                Width = line.Length;
            }
        }

        // 3. levelMap dizisini oluştur
        levelMap = new char[Width, Height];

        // 4. levelMap'i doldur ve oyuncu pozisyonunu bul
        for (int y = 0; y < Height; y++)
        {
            string line = lines[y].TrimEnd(); // Satır sonundaki olası boşlukları temizle
            for (int x = 0; x < Width; x++)
            {
                if (x < line.Length)
                {
                    char symbol = line[x];
                    levelMap[x, y] = symbol;

                    // Oyuncu başlangıç noktasını bul ve kaydet
                    if (TileSymbols.DataSymbolToType(symbol) == TileType.PlayerSpawn)
                    {
                        playerStartX = x;
                        playerStartY = y;
                        // Oyuncunun yerini haritada boşluk olarak bırakalım ki üzerine başka bir şey çizilmesin
                        levelMap[x, y] = TileSymbols.TypeToDataSymbol(TileType.Empty);
                    }
                }
                else
                {
                    // Eğer satır daha kısaysa, geri kalanını boşlukla doldur
                    levelMap[x, y] = TileSymbols.TypeToDataSymbol(TileType.Empty);
                }
            }
        }
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
                    Vector3 pos = new Vector3(x * tileSize, (Height - y - 1) * tileSize, 0);
                    TileBase newTile = Instantiate(tileBasePrefab, pos, Quaternion.identity, transform);
                    
                    // Yeni oluşturulan tile'ı kur.
                    newTile.SetVisual(spriteDatabase.GetSprite(type));
                    (newTile as IInitializable)?.Init(x, y);
                    
                    // Referans haritasına ekle.
                    tileObjects[x, y] = newTile.gameObject;
                }
                else
                {
                    Debug.LogWarning($"Prefab bulunamadı, bu tip için: {type}");
                }
            }
        }

        // --- 3. OYUNCUYU OLUŞTURMA BLOĞU ---
        // Tüm jenerik tile'lar oluşturulduktan sonra, oyuncuyu özel olarak ele al.

        // a) Oyuncunun başlangıç pozisyonunu hesapla.
        // Bu 'playerStartX' ve 'playerStartY' değişkenleri LoadLevelFromFile'da doldurulmuştu.
        Vector3 playerPos = new Vector3(playerStartX * tileSize, (Height - playerStartY - 1) * tileSize, 0);
        
        // b) Oyuncuyu, hesaplanan bu pozisyonda oluştur.
        playerObject = Instantiate(playerPrefab, playerPos, Quaternion.identity, transform);
        
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
   
}