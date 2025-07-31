public enum TileType
{
    Wall,
    PlayerSpawn,
    Breakable,
    Empty,
    Gate,
    Coin,
    Stairs,
    Enemy,
    EnemyShooter,
    Bomb,
    Projectile,
    Health
}

public static class TileSymbols
{
    public static char TypeToSymbol(TileType type)
    {
        switch (type)
        {
            case TileType.Wall:         return '█';
            case TileType.Empty:        return ' ';
            case TileType.Breakable:    return '▒';
            case TileType.Gate:         return '∩';
            case TileType.Stairs:       return '≡';
            case TileType.PlayerSpawn:  return '☺';
            case TileType.Enemy:        return '☠';
            case TileType.EnemyShooter: return 'Ψ';
            case TileType.Bomb:         return '◎';
            case TileType.Projectile:   return '·';
            case TileType.Coin:         return '¤';
            case TileType.Health:       return '♥';
            default:                    return '?'; 
        }
    }

    public static TileType SymbolToType(char symbol)
    {
        switch (symbol)
        {
            case '█': return TileType.Wall;
            case ' ': return TileType.Empty;
            case '▒': return TileType.Breakable;
            case '∩': return TileType.Gate;
            case '≡': return TileType.Stairs;
            case '☺': return TileType.PlayerSpawn;
            case '☠': return TileType.Enemy;
            case 'Ψ': return TileType.EnemyShooter;
            case '◎': return TileType.Bomb;
            case '·': return TileType.Projectile;
            case '¤': return TileType.Coin;
            case '♥': return TileType.Health;
            default:  return TileType.Empty;
        }
    }
}