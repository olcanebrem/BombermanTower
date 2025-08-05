using UnityEngine;
using System.Collections.Generic;
using System.Linq; // .Max() gibi metodlar için
[System.Serializable]
public struct TilePrefabEntry
{
    public TileType type;
    public TileBase prefab;
}
public class LevelLoader : MonoBehaviour
{
    // --- Singleton ve Temel Ayarlar ---
    public static LevelLoader instance;
    public int tileSize = 30;

    // --- Prefab Yönetimi ---
    public TilePrefabEntry[] tilePrefabs;
    private Dictionary<TileType, TileBase> prefabMap;
    public GameObject playerPrefab; // Oyuncu için özel prefab alanı
    public SpriteDatabase spriteDatabase;
    // --- Seviye Verisi ---
    // Inspector'dan sürükleyeceğimiz .txt dosyası
    public TextAsset levelFile; 
    
    public int width { get; private set; }
    public int height { get; private set; }
    
    public char[,] levelMap;
    public GameObject[,] tileObjects;
    public GameObject playerObject;
    private int playerStartX, playerStartY;

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
        LoadLevelFromFile();
        CreateMapVisual();
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
        height = lines.Length;
        width = 0;
        foreach (string line in lines)
        {
            // En uzun satırı genişlik olarak kabul et (düzgün format için)
            if (line.Length > width)
            {
                width = line.Length;
            }
        }

        // 3. levelMap dizisini oluştur
        levelMap = new char[width, height];

        // 4. levelMap'i doldur ve oyuncu pozisyonunu bul
        for (int y = 0; y < height; y++)
        {
            string line = lines[y].TrimEnd(); // Satır sonundaki olası boşlukları temizle
            for (int x = 0; x < width; x++)
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
    public void DebugPrintMap()
    {
        Debug.Log("--- Haritanın Mevcut Durumu ---");
        string mapString = "";
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                mapString += levelMap[x, y];
            }
            mapString += "\n"; // Her satırdan sonra yeni bir satıra geç
        }
        Debug.Log(mapString);
    }
    // CreateMapVisual metodunuz neredeyse hiç değişmeden çalışmaya devam edecek!
    // Sadece oyuncu oluşturma mantığını en sona taşıdık.
void CreateMapVisual()
{
    // 1. Referans haritalarımızı oluştur.
    tileObjects = new GameObject[width, height];

    // 2. JENERİK TILE'LARI OLUŞTURMA DÖNGÜSÜ
    // Bu döngü, oyuncu DIŞINDAKİ her şeyi oluşturur.
    for (int y = 0; y < height; y++)
    {
        for (int x = 0; x < width; x++)
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
                Vector3 pos = new Vector3(x * tileSize, (height - y - 1) * tileSize, 0);
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
    Vector3 playerPos = new Vector3(playerStartX * tileSize, (height - playerStartY - 1) * tileSize, 0);
    
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
        Vector3 pos = new Vector3(x * tileSize, (height - y - 1) * tileSize, 0);
        TileBase newBomb = Instantiate(bombTilePrefab, pos, Quaternion.identity, transform);

        // Bombayı kur
        newBomb.SetVisual(spriteDatabase.GetSprite(TileType.Bomb));
        (newBomb as IInitializable)?.Init(x, y);
        
        // Haritaları GÜNCELLE
        tileObjects[x, y] = newBomb.gameObject; // Nesne haritasını güncelle
        levelMap[x, y] = TileSymbols.TypeToDataSymbol(TileType.Bomb); // Mantıksal haritayı güncelle
    }
}
}