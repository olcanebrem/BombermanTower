using UnityEngine; // GameObject için bu gerekli

public interface IMovable
{
    int X { get; }
    int Y { get; }
    TileType TileType { get; }

    // Hareket eden nesnenin kendi GameObject'ine bir referans.
    // Bu, MovementHelper'ın kimi hareket ettireceğini bilmesini sağlar.
    GameObject gameObject { get; } 

    void OnMoved(int newX, int newY);
}