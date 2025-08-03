using UnityEngine;
using System;
public class EnemyShooterTile : TileBase, IMovable, ITurnBased, IInitializable
{
    public int X { get; set; }
    public int Y { get; set; }
    private int turnCounter = 0;
    private int turnsElapsed = 4;
    public TileType TileType => TileType.EnemyShooter;
    public GameObject projectilePrefab;
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
    
    public void ExecuteTurn()
    {

        if (HasActedThisTurn)
        {
            return;
        }

        // Düşmanın AI mantığı burada çalışır.
         turnCounter++;

        if (turnCounter >= turnsElapsed)
        {
            ShootRandomDirection();
            turnCounter = 0;
            HasActedThisTurn = true; // Eylem yapıldı, tur bitti.
        }
        else // Ateş etmediyse, hareket etmeyi düşünebilir
        {
            // %50 ihtimalle hareket etmeye karar ver
            if (UnityEngine.Random.value > 0.5f)
            {
                int dx = UnityEngine.Random.Range(-1, 2);
                int dy = UnityEngine.Random.Range(-1, 2);

                // Hareketi denemek için merkezi MovementHelper'ı çağır.
                // MovementHelper'ın kendisi tüm kontrolleri yapacak.
                Vector2Int moveDirection = new Vector2Int(dx, dy);

                bool didMove = MovementHelper.TryMove(this, moveDirection);

                // Sadece GERÇEKTEN hareket ettiyse eylem yapmış sayılır.
                if (didMove)
                {
                    HasActedThisTurn = true; // Eylem yapıldı, tur bitti.
                }
            }
        }
        
    }
    public void Init(int x, int y) { this.X = x; this.Y = y; }
    public void OnMoved(int newX, int newY) { this.X = newX; this.Y = newY; }

    void ShootRandomDirection()
    {
        Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        Vector2Int dir = directions[UnityEngine.Random.Range(0, directions.Length)];

        // Yeni Spawn metodunu çağırırken, kendi prefabımızı ona veriyoruz.
        Projectile.Spawn(this.projectilePrefab, X, Y, dir);
    }

    public void ResetTurn() => HasActedThisTurn = false;
}
