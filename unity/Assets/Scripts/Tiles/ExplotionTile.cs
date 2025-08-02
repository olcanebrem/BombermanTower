using UnityEngine;
public class ExplosionTile : TileBase, ITurnBased, IInitializable
{
    public int X { get; set; }
    public int Y { get; set; }
    private int turnsToLive = 1; // Patlama efektinin kaç tur ekranda kalacağı

    // --- Arayüzlerin Uygulanması ---
    public bool HasActedThisTurn { get; set; }
    public void ResetTurn() => HasActedThisTurn = false;
    public void Init(int x, int y)
    {
        this.X = x;
        this.Y = y;
    }
    // --------------------------------

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

    void OnTurn()
    {
        if (HasActedThisTurn) return;

        turnsToLive--;

        if (turnsToLive < 0)
        {
            // Süremiz doldu. Önce mantıksal haritayı temizle, sonra kendini yok et.
            LevelLoader.instance.levelMap[X, Y] = TileSymbols.TypeToDataSymbol(TileType.Empty);
            Destroy(gameObject);
        }

        HasActedThisTurn = true;
    }
}