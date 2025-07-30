using UnityEngine;

public class PlayerController : MonoBehaviour, IMovable
{
    public int X { get; set; }
    public int Y { get; set; }
    private bool hasActedThisTurn = false;
    public int explosionRange = 2;
    public float stepDuration = 0.1f;
    public TileType TileType => TileType.PlayerSpawn;

    void Start()
    {
        TurnManager.OnTurnAdvanced += ResetTurn;
    }

    void ResetTurn() => hasActedThisTurn = false;
    void OnDestroy() => TurnManager.OnTurnAdvanced -= ResetTurn;

    void Update()
    {
        if (hasActedThisTurn) return;

        if (Input.GetKey(KeyCode.W) && MovementHelper.TryMove(this, Vector2Int.up)) hasActedThisTurn = true;
        if (Input.GetKey(KeyCode.S) && MovementHelper.TryMove(this, Vector2Int.down)) hasActedThisTurn = true;
        if (Input.GetKey(KeyCode.A) && MovementHelper.TryMove(this, Vector2Int.left)) hasActedThisTurn = true;
        if (Input.GetKey(KeyCode.D) && MovementHelper.TryMove(this, Vector2Int.right)) hasActedThisTurn = true;
        if (Input.GetKeyDown(KeyCode.Space))
        {
            PlaceBomb();
            hasActedThisTurn = true;
        }
    }


    public void OnMoved(int newX, int newY)
    {
        X = newX;
        Y = newY;

        if (LevelLoader.instance.playerObject != null)
        {
            LevelLoader.instance.playerObject.transform.position = new Vector3(
                newX * LevelLoader.instance.tileSize,
                (LevelLoader.instance.height - newY - 1) * LevelLoader.instance.tileSize,
                0);
        }
    }

    public void Init(int x, int y)
    {
        X = x;
        Y = y;
    }

    void PlaceBomb()
    {
        char[,] map = LevelLoader.instance.levelMap;
        if (map[X, Y] == TileSymbols.TypeToSymbol(TileType.PlayerSpawn))
        {
            LevelLoader.instance.PlaceBombAt(X, Y, explosionRange, stepDuration);
        }
        hasActedThisTurn = true;
    }
}
