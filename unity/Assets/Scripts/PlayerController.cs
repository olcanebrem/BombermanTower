using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public int playerX, playerY;
    private float moveCooldown = 0.1f; // Time between moves in seconds
    private float currentCooldown = 0f;
    private Vector2 moveInput;

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
        // Reset move input
        moveInput = Vector2.zero;
        
        // Get input with key holding
        if (Input.GetKey(KeyCode.W)) moveInput.y = -1;
        if (Input.GetKey(KeyCode.S)) moveInput.y = 1;
        if (Input.GetKey(KeyCode.A)) moveInput.x = -1;
        if (Input.GetKey(KeyCode.D)) moveInput.x = 1;

        // Normalize diagonal movement
        if (moveInput.magnitude > 1)
            moveInput.Normalize();

        // Handle movement with cooldown
        if (currentCooldown <= 0 && moveInput != Vector2.zero)
        {
            TryMove((int)moveInput.x, (int)moveInput.y);
            currentCooldown = moveCooldown;
        }
        
        // Update cooldown timer
        if (currentCooldown > 0)
            currentCooldown -= Time.deltaTime;
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
