public interface IMovable
{
    int X { get; set; }
    int Y { get; set; }
    TileType TileType { get; }
    void OnMoved(int newX, int newY);
}