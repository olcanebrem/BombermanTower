using UnityEngine;

public class Projectile : MonoBehaviour, IMovable
{
    public int X { get; set; }
    public int Y { get; set; }
    public Vector2Int direction;
    public TileType TileType => TileType.Projectile;
    public static Projectile Spawn(int x, int y, Vector2Int direction)
    {
        Vector3 pos = new Vector3(x * LevelLoader.instance.tileSize, 
                                  (LevelLoader.instance.height - y - 1) * LevelLoader.instance.tileSize, 0);

        GameObject projectileGO = Instantiate(LevelLoader.instance.projectilePrefab, pos, Quaternion.identity, LevelLoader.instance.transform);

        Projectile proj = projectileGO.GetComponent<Projectile>();
        if (proj == null)
        {
            Debug.LogError("Projectile component not found!");
            return null;
        }

        proj.X = x;
        proj.Y = y;
        proj.direction = direction;
        return proj;
    }

    void OnEnable() => TurnManager.OnTurnAdvanced += OnTurn;
    void OnDisable() => TurnManager.OnTurnAdvanced -= OnTurn;

    void OnTurn()
    {
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
}
