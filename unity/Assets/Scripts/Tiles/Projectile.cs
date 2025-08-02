using UnityEngine;
using System.Collections;

public class Projectile : TileBase, IMovable, ITurnBased, IInitializable
{
    public int X { get; set; }
    public int Y { get; set; }
    public Vector2Int direction;
    public TileType TileType => TileType.Projectile;
    
    public bool HasActedThisTurn { get; set; }
    public GameObject projectilePrefab;
    
    // Artık LevelLoader'ı değil, doğrudan kullanılacak prefabı parametre olarak alıyor.
    public static Projectile Spawn(GameObject prefabToSpawn, int x, int y, Vector2Int direction)
    {
        // LevelLoader'dan tileSize ve height gibi bilgileri hâlâ alabiliriz, bu kabul edilebilir.
        // Çünkü bu, dünyanın genel fiziksel özellikleridir.
        float tileSize = LevelLoader.instance.tileSize;
        int height = LevelLoader.instance.height;

        Vector3 pos = new Vector3(x * tileSize, (height - y - 1) * tileSize, 0);

        // LevelLoader'ın sözlüğüne erişmek yerine, bize verilen prefabı kullanıyoruz.
        GameObject projectileGO = Instantiate(prefabToSpawn, pos, Quaternion.identity, LevelLoader.instance.transform);

        Projectile proj = projectileGO.GetComponent<Projectile>();
        if (proj == null)
        {
            Debug.LogError("Verilen prefabda Projectile component'i bulunamadı!", prefabToSpawn);
            return null;
        }

        proj.Init(x, y); // Init'i burada çağırabiliriz.
        proj.direction = direction;
        return proj;
    }
    // Start metodu, nesne tamamen sahneye eklendikten sonra çalışır.
    void Start()
    {
        // Görseli ayarlama işini bir sonraki frame'e erteleyen bir Coroutine başlat.
        StartCoroutine(SetVisualDelayed());
    }

    private IEnumerator SetVisualDelayed()
    {
        // Bir sonraki frame'i bekle. Bu, TMP'nin Awake'ini çalıştırması için zaman tanır.
        yield return null; 

        // Artık TMP hazır, görseli güvenle ayarlayabiliriz.
        string visual = TileSymbols.TypeToVisualSymbol(this.TileType);
        SetVisual(visual);
        // --- 2. ROTASYONU AYARLA (YENİ KISIM) ---
        // Yön vektörüne göre doğru açıyı hesapla.
        float angle = 180f;

        if (direction == Vector2Int.up)
        {
            angle = 270f;
        }
        else if (direction == Vector2Int.down)
        {
            angle = 90f;
        }
        else if (direction == Vector2Int.right)
        {
            angle = 0f; // Veya 270f
        }
        else if (direction == Vector2Int.left)
        {
            angle = 180f;
        }

        // Hesaplanan açıyı GameObject'in rotasyonuna uygula.
        // Z ekseni etrafında döndürüyoruz.
        transform.rotation = Quaternion.Euler(0, 0, angle);
    }
    void OnEnable()
    {
        if (TurnManager.Instance != null) TurnManager.Instance.Register(this);
        TurnManager.OnTurnAdvanced += OnTurn;
    }

    void OnDisable()
    {
        if (TurnManager.Instance != null) TurnManager.Instance.Unregister(this);
        TurnManager.OnTurnAdvanced -= OnTurn;
    }
    void OnTurn()
    {
        if (HasActedThisTurn) return;
        Move();
    }
    void Move()
    {
        int newX = X + direction.x;
        int newY = Y + direction.y;

        // Harita sınırları kontrolü
        if (newX < 0 || newX >= LevelLoader.instance.width || newY < 0 || newY >= LevelLoader.instance.height)
        {
            Destroy(gameObject);
            return;
        }

        char nextTileChar = LevelLoader.instance.levelMap[newX, newY];
        TileType nextTileType = TileSymbols.DataSymbolToType(nextTileChar);

        // Engel varsa patla / yok ol
        if (nextTileType == TileType.Wall || nextTileType == TileType.Breakable || nextTileType == TileType.Gate)
        {
            Debug.Log($"Projectile hit wall at ({newX}, {newY})");
            Destroy(gameObject);
            return;
        }

        // Harita güncelle
        LevelLoader.instance.levelMap[X, Y] = TileSymbols.TypeToDataSymbol(TileType.Empty);
        LevelLoader.instance.levelMap[newX, newY] = TileSymbols.TypeToDataSymbol(TileType.Projectile);

        // Görsel pozisyon güncelle
        transform.position = new Vector3(newX * LevelLoader.instance.tileSize,
            (LevelLoader.instance.height - newY - 1) * LevelLoader.instance.tileSize, 0);

        OnMoved(newX, newY);
    }
    public void OnMoved(int newX, int newY)
    {
        X = newX;
        Y = newY;
    }
    public void Init(int x, int y)
    {
        this.X = x;
        this.Y = y;
    }
    public void ResetTurn() => HasActedThisTurn = false;
}
