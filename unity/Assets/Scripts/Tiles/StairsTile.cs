using UnityEngine;
public class StairsTile : TileBase
{
    public int X { get; set; }
    public int Y { get; set; }
    public TileType TileType => TileType.Stairs;
    public bool HasActedThisTurn { get; set; }
    
    public void Init(int x, int y)
    {
        this.X = x;
        this.Y = y;
    }
}
