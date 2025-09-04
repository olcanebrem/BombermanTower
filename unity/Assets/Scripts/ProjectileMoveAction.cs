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
        
        // Move projectile immediately - no waiting for first turn
        if (AtomicMovementHelper.TryMove(projectile, projectile.direction, out Vector3 targetPos))
        {
            projectile.StartMoveAnimation(targetPos);
        }
        else
        {
            // If can't move, projectile hits something and dies
            projectile.Die();
        }
    }
}