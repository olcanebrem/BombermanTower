using UnityEngine;

public class EnemyShooterTile : MonoBehaviour
{
    public int x, y; // konum
    private int turnCounter = 0;
    private int turnsElapsed = 2;
    void OnEnable() => TurnManager.OnTurnAdvanced += OnTurn;
    void OnDisable() => TurnManager.OnTurnAdvanced -= OnTurn;

    public void Init(int x, int y)
    {
        this.x = x;
        this.y = y;
    }

    private void OnTurn()
    {
        turnCounter++;

        if (turnCounter >= turnsElapsed)
        {
            Debug.Log($"Enemy shooter at ({this.x},{this.y}) is ready to shoot!");
            ShootRandomDirection();
        }
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
        Projectile.Spawn(x, y, dir);
        Debug.Log($"Enemy shooter at ({this.x},{this.y}) shoots in direction {dir}");
    }
}
