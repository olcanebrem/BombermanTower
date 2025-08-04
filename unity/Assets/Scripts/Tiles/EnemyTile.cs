using UnityEngine;
using System;
public class EnemyTile : TileBase, IMovable, ITurnBased, IInitializable, IDamageable
{
    public int X { get; set; }
    public int Y { get; set; }
    
    public TileType TileType => TileType.Enemy;
    public bool HasActedThisTurn { get; set; }
    
    void OnEnable()
    {
        // Kendini TurnManager'ın listesine kaydettirir.
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.Register(this);
        }
    }

    void OnDisable()
    {
        // Kendini TurnManager'ın listesinden siler.
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.Unregister(this);
        }
    }
    
    public int CurrentHealth { get; private set; }
    public int MaxHealth { get; private set; }
    public event Action OnHealthChanged;

    public void TakeDamage(int damageAmount)
    {
        CurrentHealth -= damageAmount;
        OnHealthChanged?.Invoke();
    }
    public void ExecuteTurn()
    {
        if (HasActedThisTurn)
        {
            return;
        }

        OnTurn();
        
        HasActedThisTurn = true;
    }
    
    void Start()
    {
        Vector3 pos = transform.position;
        X = Mathf.RoundToInt(pos.x / LevelLoader.instance.tileSize);
        Y = LevelLoader.instance.height - 1 - Mathf.RoundToInt(pos.y / LevelLoader.instance.tileSize);
    }

    void OnTurn()
    {
        if (HasActedThisTurn || UnityEngine.Random.value < 0.5f) return;

        Vector2Int[] directions = new[] {
            Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
        };
        Vector2Int dir = directions[UnityEngine.Random.Range(0, directions.Length)];

        if (!MovementHelper.TryMove(this, dir)) Debug.Log("Enemy couldn't move");

        transform.position = new Vector3(X * LevelLoader.instance.tileSize,
                                         (LevelLoader.instance.height - Y - 1) * LevelLoader.instance.tileSize, 0);
        OnMoved(X, Y);
        HasActedThisTurn = true;
    }

    public void OnMoved(int newX, int newY)
    {
        X = newX;
        Y = newY;
    }

    public void ResetTurn() => HasActedThisTurn = false;

    public void Init(int x, int y)
    {
        X = x;
        Y = y;
    }
}
