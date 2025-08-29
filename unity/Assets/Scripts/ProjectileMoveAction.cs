using UnityEngine;

public class ProjectileMoveAction : IGameAction
{
    private readonly Projectile projectile;

    public ProjectileMoveAction(Projectile projectile)
    {
        this.projectile = projectile;
    }
    
    public GameObject Actor => projectile != null ? projectile.gameObject : null;   
    
    public void Execute()
    {
        // Check if projectile still exists
        if (projectile == null || projectile.gameObject == null)
        {
            Debug.LogWarning("[ProjectileMoveAction] Projectile destroyed, skipping execution");
            return;
        }
        
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