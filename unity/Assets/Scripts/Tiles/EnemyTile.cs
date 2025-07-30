using UnityEngine;
public class EnemyTile : TileBehavior, IMovable
{
    public int X { get; set; }
    public int Y { get; set; }
    public TileType TileType => TileType.Enemy;
    
    void OnEnable() => TurnManager.OnTurnAdvanced += Act;
    void OnDisable() => TurnManager.OnTurnAdvanced -= Act;

    void Start()
    {
        Vector3 pos = transform.position;
        X = Mathf.RoundToInt(pos.x / LevelLoader.instance.tileSize);
        Y = LevelLoader.instance.height - 1 - Mathf.RoundToInt(pos.y / LevelLoader.instance.tileSize);
    }

    void Act()
    {
        if (Random.value < 0.5f) return;

        Vector2Int[] directions = new[] {
            Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
        };
        Vector2Int dir = directions[Random.Range(0, directions.Length)];

        if (!MovementHelper.TryMove(this, dir)) Debug.Log("Enemy couldn't move");

        transform.position = new Vector3(X * LevelLoader.instance.tileSize,
                                         (LevelLoader.instance.height - Y - 1) * LevelLoader.instance.tileSize, 0);
        OnMoved(X, Y);
    }

    public void OnMoved(int newX, int newY)
    {
        X = newX;
        Y = newY;
    }
}
