using UnityEngine;
public static class MovementHelper
{
    public static bool TryMove(IMovable mover, Vector2Int direction)
    {
        int newX = mover.X + direction.x;
        int newY = mover.Y + direction.y;

        var map = LevelLoader.instance.levelMap;
        int width = LevelLoader.instance.width;
        int height = LevelLoader.instance.height;

        // Harita sınırı kontrolü
        if (newX < 0 || newX >= width || newY < 0 || newY >= height)
            return false;

        TileType nextTile = TileSymbols.SymbolToType(map[newX, newY]);

        // Geçilemez nesne kontrolü
        if (nextTile == TileType.Wall || nextTile == TileType.Breakable || nextTile == TileType.Gate)
            return false;

        // Haritayı güncelle
        map[mover.X, mover.Y] = TileSymbols.TypeToSymbol(TileType.Empty);
        map[newX, newY] = TileSymbols.TypeToSymbol(mover.TileType);

        // Konum bildir (pozisyon burada güncellenmeli)
        mover.OnMoved(newX, newY);

        return true;
    }
}
