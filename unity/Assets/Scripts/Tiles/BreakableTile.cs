using UnityEngine;
using System;
public class BreakableTile : TileBase, ITurnBased, IInitializable, IDamageable
{
    public int X { get; private set; }
    public int Y { get; private set; }
    public TileType TileType => TileType.Breakable;
    public bool HasActedThisTurn { get; set; }
    
    // --- Can Sistemi ---
    public int CurrentHealth { get; private set; }
    public int MaxHealth { get; private set; } = 1;
    // ------------------
    public event Action OnHealthChanged;
    void OnEnable() { if (TurnManager.Instance != null) TurnManager.Instance.Register(this); }
    void OnDisable() { if (TurnManager.Instance != null) TurnManager.Instance.Unregister(this); }
    public void Init(int x, int y) { this.X = x; this.Y = y; CurrentHealth = MaxHealth; }
    public void ResetTurn() => HasActedThisTurn = false;

    public IGameAction GetAction()
    {
        if (HasActedThisTurn) return null;
        HasActedThisTurn = true;
        return null; // Breakable tiles don't take actions
    }

    public void TakeDamage(int damageAmount)
    {
        CurrentHealth -= damageAmount;
        OnHealthChanged?.Invoke();
        if (CurrentHealth <= 0)
        {
            Die();
        }
    }

    public void Die()
    {
        var ll = LevelLoader.instance;
        if (ll != null)
        {
            ll.levelMap[X, Y] = TileSymbols.TypeToDataSymbol(TileType.Empty);
            ll.tileObjects[X, Y] = null;
        }
        Destroy(gameObject);
    }
}