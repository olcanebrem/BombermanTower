using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public int playerX, playerY;
    private bool hasActedThisTurn = false;
    public int explosionRange = 2; // Patlama menzili, bombanın yayılma uzunluğu
    public float stepDuration = 0.1f; // Playerın bir tile hareketi süresi, tur süresi olarak kullanılacak

    void Start()
    {
        FindPlayerPosition();
        TurnManager.OnTurnAdvanced += ResetTurn;
    }

    void ResetTurn()
    {
        hasActedThisTurn = false;
    }

    void OnDestroy()
    {
        TurnManager.OnTurnAdvanced -= ResetTurn;
    }


    void Update()
    {
        if (hasActedThisTurn) return;
        if (Input.GetKey(KeyCode.W) && !hasActedThisTurn) { TryMove(0, -1); }
        if (Input.GetKey(KeyCode.S) && !hasActedThisTurn) { TryMove(0, 1); }
        if (Input.GetKey(KeyCode.A) && !hasActedThisTurn) { TryMove(-1, 0); }
        if (Input.GetKey(KeyCode.D) && !hasActedThisTurn) { TryMove(1, 0); }
        if (Input.GetKeyDown(KeyCode.Space) && !hasActedThisTurn) { PlaceBomb(); }
    }

    void FindPlayerPosition()
    {
        char[,] map = LevelLoader.instance.levelMap;

        for (int y = 0; y < LevelLoader.instance.height; y++)
        {
            for (int x = 0; x < LevelLoader.instance.width; x++)
            {
                if (map[x, y] == TileSymbols.TypeToSymbol(TileType.PlayerSpawn))
                {
                    playerX = x;
                    playerY = y;
                    return;
                }
            }
        }
        Debug.LogError("Player (P) not found on the map!");
    }

    void TryMove(int dx, int dy)
    {
        int newX = playerX + dx;
        int newY = playerY + dy;

        if (newX < 0 || newX >= LevelLoader.instance.width || newY < 0 || newY >= LevelLoader.instance.height)
            return;

        char[,] map = LevelLoader.instance.levelMap;
        char targetChar = map[newX, newY];

        if ("#B╔╗╚╝║═█▓☠".Contains(targetChar))
            return;

        map[playerX, playerY] = TileSymbols.TypeToSymbol(TileType.Empty);
        map[newX, newY] = TileSymbols.TypeToSymbol(TileType.PlayerSpawn);

        playerX = newX;
        playerY = newY;

        if (LevelLoader.instance.playerObject != null)
        {
            LevelLoader.instance.playerObject.transform.position = new Vector3(
                newX * LevelLoader.instance.tileSize,
                (LevelLoader.instance.height - newY - 1) * LevelLoader.instance.tileSize,
                0);
        }

        hasActedThisTurn = true;
    }

    void PlaceBomb()
    {
        char[,] map = LevelLoader.instance.levelMap;
        if (map[playerX, playerY] == TileSymbols.TypeToSymbol(TileType.PlayerSpawn))
        {
            LevelLoader.instance.PlaceBombAt(playerX, playerY, explosionRange, stepDuration);
        }
        hasActedThisTurn = true;
    }
}
