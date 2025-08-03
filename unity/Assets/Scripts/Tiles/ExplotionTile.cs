using UnityEngine;
using System;
public class ExplosionTile : TileBase, ITurnBased, IInitializable
{
    public int X { get; set; }
    public int Y { get; set; }
    private int turnsToLive = 1; // Patlama efektinin kaç tur ekranda kalacağı

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
    
    // --- Arayüzlerin Uygulanması ---
    public bool HasActedThisTurn { get; set; }
    public void ResetTurn() => HasActedThisTurn = false;
    public void Init(int x, int y)
    {
        this.X = x;
        this.Y = y;
    }
    // --------------------------------

    public void ExecuteTurn()
    {
        if (HasActedThisTurn) return;

        OnTurn();
        
        HasActedThisTurn = true;
    }

    public void OnTurn()
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