using UnityEngine;
using System;
public class Breakable : TileBase, IDamageable
{
    public int X { get; set; }
    public int Y { get; set; }
    public TileType TileType => TileType.Breakable;
    public bool HasActedThisTurn { get; set; }
    
    public int CurrentHealth { get; private set; }
    public int MaxHealth { get; private set; }
    public event Action OnHealthChanged;

    public void Init(int x, int y) { this.X = x; this.Y = y; this.MaxHealth = 1; this.CurrentHealth = MaxHealth; }
    public void TakeDamage(int damageAmount)
    {
        CurrentHealth -= damageAmount;
        OnHealthChanged?.Invoke();
        if (CurrentHealth <= 0) Die();
    }
    private void Die()
    {
        // Mantıksal haritadaki izini temizle.
        LevelLoader.instance.levelMap[X, Y] = TileSymbols.TypeToDataSymbol(TileType.Empty);
        // Nesne haritasındaki referansını temizle.
        LevelLoader.instance.tileObjects[X, Y] = null;
        // GameObject'i yok et.
        Destroy(gameObject);
    }
}
