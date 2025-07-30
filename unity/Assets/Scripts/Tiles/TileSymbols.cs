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
            case TileType.Wall: return '#';
            case TileType.PlayerSpawn: return 'P';
            case TileType.Breakable: return 'B';
            case TileType.Empty: return ' ';
            case TileType.Gate: return 'G';
            case TileType.Coin: return 'C';
            case TileType.Stairs: return 'S';
            case TileType.Enemy: return 'E';
            case TileType.EnemyShooter: return 'F';
            case TileType.Bomb: return 'X';
            case TileType.Projectile: return '*';
            case TileType.Health: return 'H';
            default: return '?';
        }
    }

    public static TileType SymbolToType(char symbol)
    {
        switch (symbol)
        {
            case '#': return TileType.Wall;
            case 'P': return TileType.PlayerSpawn;
            case 'B': return TileType.Breakable;
            case ' ': return TileType.Empty;
            case 'G': return TileType.Gate;
            case 'C': return TileType.Coin;
            case 'S': return TileType.Stairs;
            case 'E': return TileType.Enemy;
            case 'F': return TileType.EnemyShooter;
            case 'X': return TileType.Bomb;
            case '*': return TileType.Projectile;
            case 'H': return TileType.Health;
            default: return TileType.Empty;
        }
    }
}
