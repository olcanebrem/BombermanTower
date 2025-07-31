using UnityEngine;

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

        proj.X = x;
        proj.Y = y;
        proj.direction = direction;
        return proj;
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
        TileType nextTileType = TileSymbols.SymbolToType(nextTileChar);

        // Engel varsa patla / yok ol
        if (nextTileType == TileType.Wall || nextTileType == TileType.Breakable || nextTileType == TileType.Gate)
        {
            Debug.Log($"Projectile hit wall at ({newX}, {newY})");
            Destroy(gameObject);
            return;
        }

        // Harita güncelle
        LevelLoader.instance.levelMap[X, Y] = TileSymbols.TypeToSymbol(TileType.Empty);
        LevelLoader.instance.levelMap[newX, newY] = '*'; // projectile karakteri

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
