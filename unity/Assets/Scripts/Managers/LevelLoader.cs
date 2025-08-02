using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
[System.Serializable]
public struct TilePrefabEntry
{
    public TileType type;
    public TileBase prefab;
}
public class LevelLoader : MonoBehaviour
{
    public static LevelLoader instance;
    public TilePrefabEntry[] tilePrefabs;
    private Dictionary<TileType, TileBase> prefabMap;
    public int tileSize = 30;
    public Font asciiFont;
    public GameObject playerPrefab;
    

    public int width = 10;
    public int height = 30;

    public GameObject playerObject;
    public GameObject[,] tileObjects;

    public char[,] levelMap;

    void Awake()
{
    // --- BÖLÜM 1: KENDİNİ TANITMA ---
    // Bu metodun hangi GameObject üzerinde çalıştığını ve kaç tane prefabı olduğunu bize söyle.
    // Bu log, hiyerarşideki nesneye tıklanabilir bir link içerir.
    Debug.Log($"AWAKE ÇAĞRILDI: Ben '{gameObject.name}' (ID: {GetInstanceID()}). " +
              $"Inspector'daki prefab listemin boyutu: {tilePrefabs.Length}", gameObject);

    // --- BÖLÜM 2: SİNGLETON KONTROLÜ (EN ÖNEMLİ KISIM) ---
    // Eğer 'instance' daha önceden başka bir nesne tarafından doldurulmuşsa...
    if (instance != null)
    {
        // Bu bir hatadır! Bize durumu kırmızı renkte, kritik bir hata olarak bildir.
        Debug.LogError($"KRİTİK HATA: Sahnede zaten bir LevelLoader var! " +
                       $"Hayatta kalan patron: '{instance.gameObject.name}' (ID: {instance.GetInstanceID()}). " +
                       $"Ben ('{gameObject.name}') kendimi yok ediyorum!", gameObject);
        
        // Bu kopya nesneyi yok et ve bu metodun çalışmasını derhal durdur.
        Destroy(gameObject);
        return;
    }

    // --- BÖLÜM 3: PATRON OLMA ---
    // Eğer buraya kadar gelebildiysek, biz ilk ve tek örneğiz. Ofis bizimdir.
    Debug.Log($"PATRON OLDUM: Ben '{gameObject.name}'. Artık projenin tek LevelLoader'ıyım.", gameObject);
    instance = this;
    
    // İsteğe bağlı: Sahneler arası geçişte yok olmamak için.
    // DontDestroyOnLoad(gameObject);

    // --- BÖLÜM 4: SÖZLÜĞÜ GÜVENLE DOLDURMA ---
    // Artık tek patron olduğumuza göre, sözlüğümüzü güvenle doldurabiliriz.
    // Önceki önerideki gibi TileBase veya GameObject, ne kullanıyorsanız...
    prefabMap = new Dictionary<TileType, TileBase>(); 
    foreach (var entry in tilePrefabs)
    {
        if (entry.prefab != null && !prefabMap.ContainsKey(entry.type))
        {
            prefabMap.Add(entry.type, entry.prefab);
        }
    }
    Debug.Log($"Sözlük dolduruldu. Toplam {prefabMap.Count} eleman eklendi.", gameObject);
}

    void Start()
    {
        // Awake'in işini bitirdiğinden emin olduktan sonra haritayı oluştur.
        GenerateRandomLevel();
        CreateMapVisual();
    }
    void Update()
{
    // Her saniye bize prefab listesinin boyutunu ve bu nesnenin ID'sini söyle.
    if (Time.frameCount % 60 == 0) // Yaklaşık saniyede bir çalışır
    {
        Debug.Log($"Ben '{gameObject.name}' (ID: {GetInstanceID()}). " +
                  $"Prefab listemin boyutu: {tilePrefabs.Length}");
    }
}
    void GenerateRandomLevel()
    {
        levelMap = new char[width, height];

        // Hepsini boş yap
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                levelMap[x, y] = TileSymbols.TypeToDataSymbol(TileType.Empty);

        // Çevre duvarları koy
        for (int x = 0; x < width; x++)
        {
            levelMap[x, 0] = TileSymbols.TypeToDataSymbol(TileType.Wall);
            levelMap[x, height - 1] = TileSymbols.TypeToDataSymbol(TileType.Wall);
        }
        for (int y = 0; y < height; y++)
        {
            levelMap[0, y] = TileSymbols.TypeToDataSymbol(TileType.Wall);
            levelMap[width - 1, y] = TileSymbols.TypeToDataSymbol(TileType.Wall);
        }

        // Player spawn'u rastgele yerleştir
        int px = Random.Range(1, width - 1);
        int py = Random.Range(1, height - 1);
        levelMap[px, py] = TileSymbols.TypeToDataSymbol(TileType.PlayerSpawn);

        // Örnek olarak diğer bazı nesneleri rastgele yerleştir
        PlaceRandom(TileType.Coin, 10);
        PlaceRandom(TileType.Breakable, 5);
        PlaceRandom(TileType.Enemy, 4);
        PlaceRandom(TileType.EnemyShooter, 2);
        PlaceRandom(TileType.Bomb, 3);
        PlaceRandom(TileType.Health, 3);
        PlaceRandom(TileType.Gate, 1);
        PlaceRandom(TileType.Stairs, 1);
    }

    void PlaceRandom(TileType type, int count)
    {
        int placed = 0;
        while (placed < count)
        {
            int x = Random.Range(1, width - 1);
            int y = Random.Range(1, height - 1);
            if (levelMap[x, y] == TileSymbols.TypeToDataSymbol(TileType.Empty))
            {
                levelMap[x, y] = TileSymbols.TypeToDataSymbol(type);
                placed++;
            }
        }
    }

    void CreateMapVisual()
    {
        tileObjects = new GameObject[width, height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // --- 1. ADIM: ÇEVİRİM ---
                // Haritadan okunan basit karakter (örn: 'P')
                char symbolChar = levelMap[x, y];
                // Karakteri TİPE çevir. Bu, tüm mantığın temelidir.
                TileType type = TileSymbols.DataSymbolToType(symbolChar);

                // Boş kareler bir hata değildir, bu yüzden hiçbir şey yapmadan devam et.
                if (type == TileType.Empty)
                {
                    continue;
                }

                // --- 2. ADIM: VERİ HAZIRLAMA ---
                Vector3 pos = new Vector3(x * tileSize, (height - y - 1) * tileSize, 0);
                // Tipi EMOJİ'ye çevir.
                string emojiSymbol = TileSymbols.TypeToVisualSymbol(type);

                // --- 3. ADIM: OLUŞTURMA ---

                // Oyuncuyu tipi üzerinden kontrol etmek daha güvenlidir.
                if (type == TileType.PlayerSpawn)
                {
                    playerObject = Instantiate(playerPrefab, pos, Quaternion.identity, transform);
                    playerObject.GetComponent<PlayerController>()?.Init(x, y);
                    tileObjects[x, y] = playerObject;
                    continue; // Oyuncu yerleştirildi, bu döngü adımı bitti.
                }

                // Diğer tüm tipler için prefab bulmayı dene.
                if (prefabMap.TryGetValue(type, out var tileBasePrefab))
                {
                    TileBase newTile = Instantiate(tileBasePrefab, pos, Quaternion.identity, transform);

                    // Görseli, hazırladığımız emoji string'i ile ayarla.
                    newTile.SetVisual(emojiSymbol);

                    // Init metodunu güvenli bir şekilde çağır.
                    (newTile as IInitializable)?.Init(x, y);
                    
                    // Diziyi güncelle.
                    tileObjects[x, y] = newTile.gameObject;
                }
                else // Prefab sözlükte bulunamadıysa, bu bir hatadır.
                {
                    // Hata mesajını daha bilgilendirici hale getirelim.
                    Debug.LogError($"Prefab bulunamadı! Harita Karakteri: '{symbolChar}', Anlaşılan Tip: {type}");
                }
            }
        }
    }

    GameObject CreateAsciiTile(char symbol, Vector3 position)
    {
        GameObject tileGO = new GameObject($"Tile_{position.x}_{position.y}");
        tileGO.transform.SetParent(this.transform);
        tileGO.transform.position = position;

        RectTransform rt = tileGO.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(tileSize, tileSize);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.zero;
        rt.pivot = Vector2.zero;

        Text text = tileGO.AddComponent<Text>();
        text.text = symbol.ToString();
        text.fontSize = tileSize;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.font = asciiFont;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        return tileGO;
    }
    
}
