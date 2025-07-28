using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public int playerX, playerY;

    void Start()
    {
        FindPlayerPosition();
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

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.W)) TryMove(0, -1);
        if (Input.GetKeyDown(KeyCode.S)) TryMove(0, 1);
        if (Input.GetKeyDown(KeyCode.A)) TryMove(-1, 0);
        if (Input.GetKeyDown(KeyCode.D)) TryMove(1, 0);
    }

    void TryMove(int dx, int dy)
    {
        int newX = playerX + dx;
        int newY = playerY + dy;

        if (newX < 0 || newX >= LevelLoader.instance.width || newY < 0 || newY >= LevelLoader.instance.height)
            return;

        char[,] map = LevelLoader.instance.levelMap;
        char targetChar = map[newX, newY];

        // Engel olan karakterler
        if ("#B╔╗╚╝║═█▓☠".Contains(targetChar))
            return;

        // Eski pozisyonu boşalt
        map[playerX, playerY] = TileSymbols.TypeToSymbol(TileType.Empty);

        // Yeni pozisyona player koy
        map[newX, newY] = TileSymbols.TypeToSymbol(TileType.PlayerSpawn);

        playerX = newX;
        playerY = newY;

        // Player GameObject pozisyonunu güncelle
        if (LevelLoader.instance.playerObject != null)
        {
            LevelLoader.instance.playerObject.transform.position = new Vector3(
                newX * LevelLoader.instance.tileSize,
                (LevelLoader.instance.height - newY - 1) * LevelLoader.instance.tileSize,
                0);
        }
    }
}
