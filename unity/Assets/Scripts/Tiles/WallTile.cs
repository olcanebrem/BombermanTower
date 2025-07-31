using UnityEngine;
public class WallTile : TileBase
{
    public int X { get; set; }
    public int Y { get; set; }
    public TileType TileType => TileType.Wall;
    public void Init(int x, int y)
    {
        this.X = x;
        this.Y = y;
    }
}