using UnityEngine;

public class MoveAction : IGameAction
{
    private readonly IMovable mover;
    private readonly Vector2Int direction;

    // IGameAction'ın yeni özelliği
    public GameObject Actor => mover.gameObject;

    public MoveAction(IMovable mover, Vector2Int direction)
    {
        this.mover = mover;
        this.direction = direction;
    }

    public void Execute()
    {
        // Execute'un içi aynı kalır.
        if (MovementHelper.TryMove(mover, direction, out Vector3 targetPos))
        {
            mover.StartMoveAnimation(targetPos);
        }
    }
}