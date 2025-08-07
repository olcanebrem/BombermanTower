using UnityEngine;

public interface IMovable
{
    int X { get; }
    int Y { get; }
    TileType TileType { get; }
    GameObject gameObject { get; }

    void OnMoved(int newX, int newY);

    /// <summary>
    /// Birimin, verilen hedef pozisyona doğru akıcı hareket animasyonunu başlatır.
    /// </summary>
    void StartMoveAnimation(Vector3 targetPosition);
}