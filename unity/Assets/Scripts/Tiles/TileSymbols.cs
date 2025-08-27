using UnityEngine;

public enum TileType
{
    Wall,
    Empty,
    Breakable,
    Gate,
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
            case TileType.Wall:         return '|';
            case TileType.Empty:        return '-';
            case TileType.Breakable:    return 'B';
            case TileType.Gate:         return 'G';
            case TileType.PlayerSpawn:  return 'p';
            case TileType.Player:       return 'P';
            case TileType.Enemy:        return 'E';
            case TileType.EnemyShooter: return 'S'; // User requested EnemyShooter to show as 'S'
            case TileType.Bomb:         return 'O'; // User requested Bomb as 'O'
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
            // Updated mappings per user request
            case '|': return TileType.Wall;         // Wall changed from '#' to '|'
            case '#': return TileType.Wall;         // Legacy support
            case '-': return TileType.Empty;
            case 'B': return TileType.Breakable;
            case 'G': return TileType.Gate;         // Gate uses 'G' symbol
            case 'p': return TileType.PlayerSpawn;
            case 'O': return TileType.Bomb;         // Bomb uses 'O' symbol
            case '*': return TileType.Projectile;
            case 'X': return TileType.Explosion;    // Explosion stays 'X'
            
            // Houdini format mappings
            case '.': return TileType.Empty;        // EMPTY
            case 'P': return TileType.Player;       // PLAYER
            case 'E': return TileType.Enemy;        // ENEMY (both Enemy and EnemyShooter show as 'E')
            case 'S': return TileType.EnemyShooter; // ENEMY_SHOOTER (level data still uses 'S')
            case 'C': return TileType.Coin;         // COIN
            case 'H': return TileType.Health;       // HEALTH
            case '?': return TileType.Empty;        // Special floor type
            
            default:  return TileType.Empty;
        }
    }
}