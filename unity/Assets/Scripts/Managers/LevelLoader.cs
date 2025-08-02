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
    public Font asciiFont; // Manuel tile oluşturma için

    // --- Prefab Yönetimi ---
    public TilePrefabEntry[] tilePrefabs;
    private Dictionary<TileType, TileBase> prefabMap;
    public GameObject playerPrefab; // Oyuncu için özel prefab alanı

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

    // CreateMapVisual metodunuz neredeyse hiç değişmeden çalışmaya devam edecek!
    // Sadece oyuncu oluşturma mantığını en sona taşıdık.
    void CreateMapVisual()
    {
        tileObjects = new GameObject[width, height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                char symbolChar = levelMap[x, y];
                TileType type = TileSymbols.DataSymbolToType(symbolChar);

                if (type == TileType.Empty) continue;

                if (prefabMap.TryGetValue(type, out var tileBasePrefab))
                {
                    Vector3 pos = new Vector3(x * tileSize, (height - y - 1) * tileSize, 0);
                    TileBase newTile = Instantiate(tileBasePrefab, pos, Quaternion.identity, transform);
                    newTile.SetVisual(TileSymbols.TypeToVisualSymbol(type));
                    (newTile as IInitializable)?.Init(x, y);
                    tileObjects[x, y] = newTile.gameObject;
                }
                else
                {
                    Debug.LogWarning($"Prefab bulunamadı, bu tip için: {type}");
                }
                    // --- YENİ OYUNCU OLUŞTURMA BLOĞU ---
                    // Tüm harita bittikten sonra, oyuncuyu özel koordinatlarına yerleştir.
                    Vector3 playerPos = new Vector3(playerStartX * tileSize, (height - playerStartY - 1) * tileSize, 0);
                    playerObject = Instantiate(playerPrefab, playerPos, Quaternion.identity, transform);
                    
                    // 1. Oyuncunun tipini al (PlayerSpawn).
                    TileType playerType = TileType.PlayerSpawn;
                    // 2. Bu tipe karşılık gelen görseli (sprite etiketini) al.
                    string playerVisual = TileSymbols.TypeToVisualSymbol(playerType);
                    // 3. Oyuncu nesnesinin üzerindeki TileBase bileşenini bul ve görselini ayarla.
                    playerObject.GetComponent<TileBase>()?.SetVisual(playerVisual);
                    // ---------------------------------------------------------

                    // Init metodunu çağır.
                    playerObject.GetComponent<PlayerController>()?.Init(playerStartX, playerStartY);
                    
                    // Oyuncunun GameObject'ini de tileObjects dizisine ekleyelim ki referans tam olsun.
                    tileObjects[playerStartX, playerStartY] = playerObject;
                
            }
        }
    }
}