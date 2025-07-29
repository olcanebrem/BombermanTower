using UnityEngine;

public class EnemyShooterTile : TileBehavior
{
    private int x, y;

    void Start()
    {
        Vector3 pos = transform.position;
        x = Mathf.RoundToInt(pos.x / LevelLoader.instance.tileSize);
        y = LevelLoader.instance.height - 1 - Mathf.RoundToInt(pos.y / LevelLoader.instance.tileSize);

        TurnManager.OnTurnAdvanced += Act;
    }

    void OnDestroy() => TurnManager.OnTurnAdvanced -= Act;

    void Act()
    {
        if (Random.value > 0.5f) return; // Rastgele ateş et

        Vector2Int[] directions = new Vector2Int[] {
            Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
        };

        Vector2Int dir = directions[Random.Range(0, directions.Length)];
        ShootInDirection(dir);
    }

    void ShootInDirection(Vector2Int dir)
    {
        int maxRange = 5; // Mermi menzili
        for (int i = 1; i <= maxRange; i++)
        {
            int nx = x + dir.x * i;
            int ny = y + dir.y * i;

            if (nx < 0 || ny < 0 || nx >= LevelLoader.instance.width || ny >= LevelLoader.instance.height)
                break;

            // Duvara veya başka bir engele çarparsa durur
            char target = LevelLoader.instance.levelMap[nx, ny];
            if ("#B║═".Contains(target)) break;

            LevelLoader.instance.levelMap[nx, ny] = '*'; // Ateş karakteri
        }
    }
}
