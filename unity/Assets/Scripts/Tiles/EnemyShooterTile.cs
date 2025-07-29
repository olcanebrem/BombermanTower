using UnityEngine;

public class EnemyShooterTile : MonoBehaviour
{
    private int x, y;
    private int turnCounter = 0;
    private int turnsElapsed = 4;
    void OnEnable() => TurnManager.OnTurnAdvanced += OnTurn;
    void OnDisable() => TurnManager.OnTurnAdvanced -= OnTurn;

    public void Init(int x, int y)
    {
        this.x = x;
        this.y = y;
    }

    private void OnTurn()
    {
        // Hareket et
        TryMove();

        // Ateş etme kontrolü
        turnCounter++;

        if (turnCounter >= turnsElapsed)
        {
            ShootRandomDirection();
            turnCounter = 0;
        }
    }

    void TryMove()
    {
        // %50 ihtimal hareket et, %50 ihtimal bekle
        if (Random.value < 0.5f) return;

        int dx = Random.Range(-1, 2);
        int dy = Random.Range(-1, 2);

        int newX = x + dx;
        int newY = y + dy;

        if (!IsValidMove(newX, newY)) return;

        // Harita güncelle
        var map = LevelLoader.instance.levelMap;
        map[x, y] = TileSymbols.TypeToSymbol(TileType.Empty);
        map[newX, newY] = TileSymbols.TypeToSymbol(TileType.EnemyShooter);

        // Görsel taşı
        transform.position = new Vector3(
            newX * LevelLoader.instance.tileSize, 
            (LevelLoader.instance.height - newY - 1) * LevelLoader.instance.tileSize, 
            0);

        // Pozisyon güncelle
        x = newX;
        y = newY;
    }

    bool IsValidMove(int nx, int ny)
    {
        if (nx < 0 || ny < 0 || nx >= LevelLoader.instance.width || ny >= LevelLoader.instance.height)
            return false;
        var c = LevelLoader.instance.levelMap[nx, ny];
        return TileSymbols.SymbolToType(c) == TileType.Empty;
    }

    void ShootRandomDirection()
    {
        // Rastgele yön seç
        Vector2Int[] directions = new Vector2Int[] {
            Vector2Int.up,
            Vector2Int.down,
            Vector2Int.left,
            Vector2Int.right
        };

        Vector2Int dir = directions[Random.Range(0, directions.Length)];

        // Spawn projectile'u kendi (x,y) pozisyonundan oluştur
        Projectile.Spawn(x, y, dir);
    }
}
