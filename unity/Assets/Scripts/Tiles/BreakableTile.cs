using UnityEngine;
using System;
public class Breakable : TileBase, IDamageable
{
    public int X { get; set; }
    public int Y { get; set; }
    public TileType TileType => TileType.Breakable;
    public bool HasActedThisTurn { get; set; }
    
    public void Init(int x, int y)
    {
        this.X = x;
        this.Y = y;
    }
    
    public int CurrentHealth { get; private set; }
    public int MaxHealth { get; private set; }
    public event Action OnHealthChanged;

    public void TakeDamage(int damageAmount)
    {
        CurrentHealth -= damageAmount;
        OnHealthChanged?.Invoke();
    }
}
