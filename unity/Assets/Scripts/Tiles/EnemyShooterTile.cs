using UnityEngine;
using System.Collections;
using System;
public class EnemyShooterTile : TileBase, IMovable, ITurnBased, IInitializable, IDamageable
{
    // --- Arayüzler ve Değişkenler ---
    public int X { get; private set; }
    public int Y { get; private set; }
    public TileType TileType => TileType.EnemyShooter;
    public bool HasActedThisTurn { get; set; }
    public GameObject projectilePrefab;
    private int turnCounter = 0;
    private int turnsToShoot = 4;
    public int CurrentHealth { get; private set; }
    public int MaxHealth { get; private set; }
    public event Action OnHealthChanged;
    
    //=========================================================================
    // KAYIT VE KURULUM
    //=========================================================================
    void OnEnable() { if (TurnManager.Instance != null) TurnManager.Instance.Register(this); }
    void OnDisable() { if (TurnManager.Instance != null) TurnManager.Instance.Unregister(this); }
    public void Init(int x, int y) { this.X = x; this.Y = y; this.MaxHealth = 1; this.CurrentHealth = MaxHealth; }
    public void OnMoved(int newX, int newY) { this.X = newX; this.Y = newY; }

    //=========================================================================
    // TUR TABANLI EYLEMLER (ITurnBased)
    //=========================================================================
    public void ResetTurn() => HasActedThisTurn = false;

    public void ExecuteTurn()
    {
        if (HasActedThisTurn) return;

        turnCounter++;
        if (turnCounter >= turnsToShoot)
        {
            ShootRandomDirection();
            turnCounter = 0;
            HasActedThisTurn = true;
        }
        else
        {
            if (UnityEngine.Random.value > 0.5f)
            {
                Vector2Int moveDirection = new Vector2Int(UnityEngine.Random.Range(-1, 2), UnityEngine.Random.Range(-1, 2));
                
                if (MovementHelper.TryMove(this, moveDirection, out Vector3 targetPos))
                {
                    StartCoroutine(SmoothMove(targetPos));
                    HasActedThisTurn = true;
                }
            }
        }
    }

    //=========================================================================
    // HAREKET VE DİĞER EYLEMLER
    //=========================================================================
    private IEnumerator SmoothMove(Vector3 targetPosition)
    {
        TurnManager.Instance.ReportAnimationStart();
        Vector3 startPosition = transform.position;
        float elapsedTime = 0f;
        float moveDuration = 0.15f;

        while (elapsedTime < moveDuration)
        {
            transform.position = Vector3.Lerp(startPosition, targetPosition, elapsedTime / moveDuration);
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        transform.position = targetPosition;
        TurnManager.Instance.ReportAnimationEnd();
    }

    void ShootRandomDirection()
    {
        if (projectilePrefab == null) return;
        
        Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        Vector2Int dir = directions[UnityEngine.Random.Range(0, directions.Length)];
        
        int startX = X + dir.x;
        int startY = Y + dir.y;

        var ll = LevelLoader.instance;
        if (startX < 0 || startX >= ll.width || startY < 0 || startY >= ll.height) return;

        TileType targetType = TileSymbols.DataSymbolToType(ll.levelMap[startX, startY]);
        if (MovementHelper.IsTilePassable(targetType))
        {
            Projectile.Spawn(this.projectilePrefab, startX, startY, dir);
        }
    }

    public void TakeDamage(int damageAmount)
    {
        CurrentHealth -= damageAmount;
        OnHealthChanged?.Invoke();
        if (CurrentHealth <= 0) Die();
    }
    private void Die()
    {
        // Mantıksal haritadaki izini temizle.
        LevelLoader.instance.levelMap[X, Y] = TileSymbols.TypeToDataSymbol(TileType.Empty);
        // Nesne haritasındaki referansını temizle.
        LevelLoader.instance.tileObjects[X, Y] = null;
        // GameObject'i yok et.
        Destroy(gameObject);
    }
}