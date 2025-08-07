// Dosya Adı: ShootAction.cs
using UnityEngine;

public class ShootAction : IGameAction
{
    private readonly EnemyShooterTile shooter; // Şimdilik sadece EnemyShooter ateş edebilir
    private readonly Vector2Int direction;

    public GameObject Actor => shooter.gameObject;

    public ShootAction(EnemyShooterTile shooter, Vector2Int direction)
    {
        this.shooter = shooter;
        this.direction = direction;
    }

    public void Execute()
    {
        // Bu eylemin tek görevi, EnemyShooterTile'ın Shoot metodunu çağırmaktır.
        // Shoot metodu, merminin başlangıç pozisyonunu hesaplama ve
        // Projectile.SpawnAndSetup'ı çağırma mantığını zaten içeriyor.
        if (shooter != null)
        {
            shooter.Shoot(direction);
        }
    }
}