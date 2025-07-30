using UnityEngine;

public class EnemyShooterTile : MonoBehaviour, IMovable, ITurnBased
{
    private int turnCounter = 0;
    private int turnsElapsed = 4;
    public TileType TileType => TileType.EnemyShooter;
    void OnEnable()
    {
        if (TurnManager.Instance != null) TurnManager.Instance.Register(this);
        TurnManager.OnTurnAdvanced += OnTurn;
    }

    void OnDisable()
    {
        if (TurnManager.Instance != null) TurnManager.Instance.Unregister(this);
        TurnManager.OnTurnAdvanced -= OnTurn;
    }
    public bool HasActedThisTurn { get; set; }
    public void ResetTurn() => HasActedThisTurn = false;

    public int X { get; set; }
    public int Y { get; set; }

    public void Init(int x, int y)
    {
        this.X = x;
        this.Y = y;
    }

    private void OnTurn()
    {
        if (HasActedThisTurn) return;
        // Ateş etme kontrolü
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
            if (Random.value > 0.5f)
            {
                int dx = Random.Range(-1, 2);
                int dy = Random.Range(-1, 2);

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
    public void OnMoved(int newX, int newY)
    {
        X = newX;
        Y = newY;
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
        Projectile.Spawn(X, Y, dir);
    }
}
