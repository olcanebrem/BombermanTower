using UnityEngine;

public class ProjectileMoveAction : IGameAction
{
    private readonly Projectile projectile;

    public ProjectileMoveAction(Projectile projectile)
    {
        this.projectile = projectile;
    }
    
    public GameObject Actor => projectile.gameObject;   
    
    public void Execute()
    {
        // Bu mantık, eski Projectile.ExecuteTurn'den kopyalandı.
        if (projectile.isFirstTurn)
        {
            projectile.isFirstTurn = false;
            return;
        }

        if (MovementHelper.TryMove(projectile, projectile.direction, out Vector3 targetPos))
        {
            projectile.StartMoveAnimation(targetPos);
        }
        else
        {
            projectile.Die();
        }
    }
}