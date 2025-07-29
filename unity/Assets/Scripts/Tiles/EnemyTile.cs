using UnityEngine;

public class EnemyTile : TileBehavior
{
    private int x, y;

    void Start()
    {
        // Bu objenin bulunduğu tile'ı bul
        Vector3 pos = transform.position;
        x = Mathf.RoundToInt(pos.x / LevelLoader.instance.tileSize);
        y = LevelLoader.instance.height - 1 - Mathf.RoundToInt(pos.y / LevelLoader.instance.tileSize);

        TurnManager.OnTurnAdvanced += Act;
    }

    void OnDestroy() => TurnManager.OnTurnAdvanced -= Act;

    void Act()
    {
        // %50 ihtimal idle kal, %50 ihtimal hareket et
        if (Random.value < 0.5f) return;

        int dx = Random.Range(-1, 2);
        int dy = Random.Range(-1, 2);

        int newX = x + dx;
        int newY = y + dy;

        if (!IsValidMove(newX, newY)) return;

        // Harita güncelle
        var map = LevelLoader.instance.levelMap;
        map[x, y] = TileSymbols.TypeToSymbol(TileType.Empty);
        map[newX, newY] = TileSymbols.TypeToSymbol(TileType.Enemy);

        // Görsel taşı
        transform.position = new Vector3(newX * LevelLoader.instance.tileSize, (LevelLoader.instance.height - newY - 1) * LevelLoader.instance.tileSize, 0);

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
}
