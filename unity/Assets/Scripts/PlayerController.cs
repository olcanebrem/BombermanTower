using UnityEngine;

public class PlayerController : TileBase, IMovable, ITurnBased, IInitializable
{
    public int X { get; set; }
    public int Y { get; set; }
    public int explosionRange = 2;
    public float stepDuration = 0.1f;
    public bool HasActedThisTurn { get; set; }
    public TileType TileType => TileType.PlayerSpawn;

    void Start()
    {
        TurnManager.OnTurnAdvanced += ResetTurn;
    }

    void OnDestroy() { TurnManager.OnTurnAdvanced -= ResetTurn; }

    void Update()
    {
        if (HasActedThisTurn) return;

        if (Input.GetKey(KeyCode.W) && MovementHelper.TryMove(this, Vector2Int.up)) HasActedThisTurn = true;
        if (Input.GetKey(KeyCode.S) && MovementHelper.TryMove(this, Vector2Int.down)) HasActedThisTurn = true;
        if (Input.GetKey(KeyCode.A) && MovementHelper.TryMove(this, Vector2Int.left)) HasActedThisTurn = true;
        if (Input.GetKey(KeyCode.D) && MovementHelper.TryMove(this, Vector2Int.right)) HasActedThisTurn = true;
        if (Input.GetKeyDown(KeyCode.Space))
        {
            PlaceBomb();
            HasActedThisTurn = true;
        }
    }


    public void OnMoved(int newX, int newY)
    {
        X = newX;
        Y = newY;
    }

    public void Init(int x, int y)
    {
        X = x;
        Y = y;
    }

    
    public void ResetTurn() => HasActedThisTurn = false;
    public void PlaceBomb()
    {
        
    }
}
