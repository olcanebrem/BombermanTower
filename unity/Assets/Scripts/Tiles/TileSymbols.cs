using UnityEngine;

public enum TileType
{
    Wall,
    Empty,
    Breakable,
    Gate,
    Stairs,
    PlayerSpawn,
    Player,
    Enemy,
    EnemyShooter,
    Bomb,
    Projectile,
    Coin,
    Health,
    Explosion,
    Unknown
}
public static class TileSymbols
{
    // --- GÖRSEL KATMAN İÇİN ---
    // Bir Tipi, ekranda gösterilecek olan EMOJI'ye çevirir.
    public static string TypeToVisualSymbol(TileType type)
    {
        switch (type)
        {
            case TileType.Wall:         return "<sprite name=wall>";
            case TileType.Empty:        return "<sprite name=empty>";
            case TileType.Breakable:    return "<sprite name=breakable>";
            case TileType.Gate:         return "<sprite name=gate>";
            case TileType.Stairs:       return "<sprite name=stairs>";
            case TileType.PlayerSpawn:  return "<sprite name=playerspawn>";
            case TileType.Player:       return "<sprite name=player>";
            case TileType.Enemy:        return "<sprite name=enemy>";
            case TileType.EnemyShooter: return "<sprite name=enemyshooter>";
            case TileType.Bomb:         return "<sprite name=bomb>";
            case TileType.Projectile:   return "<sprite name=projectile>";
            case TileType.Coin:         return "<sprite name=coin>";
            case TileType.Health:       return "<sprite name=health>";
            case TileType.Explosion:    return "<sprite name=explosion>";
            default:                    return "<sprite name=unknown>";
        }
    }

    // --- VERİ KATMANI İÇİN ---
    // YENİ METOD: Bir Tipi, levelMap dizisinde saklanacak olan basit KARAKTERE çevirir.
    public static char TypeToDataSymbol(TileType type)
    {
        switch (type)
        {
            case TileType.Wall:         return '#';
            case TileType.Empty:        return '-';
            case TileType.Breakable:    return 'B';
            case TileType.Gate:         return 'G';
            case TileType.Stairs:       return 'S';
            case TileType.PlayerSpawn:  return 'p';
            case TileType.Player:       return 'P';
            case TileType.Enemy:        return 'E';
            case TileType.EnemyShooter: return 'F';
            case TileType.Bomb:         return 'x';
            case TileType.Projectile:   return '*';
            case TileType.Coin:         return 'C';
            case TileType.Health:       return 'H';
            case TileType.Explosion:    return 'X';
            default:                    return '?';
        }
    }

    // Bir KARAKTERİ, Tipe çevirir. Bu metodun adı artık daha anlamlı.
    public static TileType DataSymbolToType(char symbol)
    {
        switch (symbol)
        {
            case '#': return TileType.Wall;
            case '-': return TileType.Empty;
            case 'B': return TileType.Breakable;
            case 'G': return TileType.Gate;
            case 'S': return TileType.Stairs;
            case 'p': return TileType.PlayerSpawn;
            case 'P': return TileType.Player;
            case 'E': return TileType.Enemy;
            case 'F': return TileType.EnemyShooter;
            case 'x': return TileType.Bomb;
            case '*': return TileType.Projectile;
            case 'C': return TileType.Coin;
            case 'H': return TileType.Health;
            case 'X': return TileType.Explosion;
            default:  return TileType.Empty;
        }
    }
}