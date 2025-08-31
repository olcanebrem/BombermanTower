using UnityEngine;
using System.Collections;
using System;
using Debug = UnityEngine.Debug;

public class EnemyShooterTile : TileBase, IMovable, ITurnBased, IInitializable, IDamageable
{
    // --- Arayüzler ve Değişkenler ---
    public int X { get; private set; }
    public int Y { get; private set; }
    public override TileType TileType => TileType.EnemyShooter;
    public bool HasActedThisTurn { get; set; }
    public GameObject projectilePrefab;
    private int turnCounter = 0;
    [SerializeField] private int turnsToShoot = 4;
    private bool isAnimating = false;
    private Vector2Int lastFacingDirection;

    // --- Can Sistemi ---
    [SerializeField] private int startingHealth = 1;
    public int CurrentHealth { get; private set; }
    public int MaxHealth { get; private set; }
    public event Action OnHealthChanged;

    #region Kayıt ve Kurulum

    void OnEnable() { if (TurnManager.Instance != null) TurnManager.Instance.Register(this); }
    void OnDisable()
    {
        // Eğer bu nesne, bir animasyonun ortasındayken yok edilirse...
        if (isAnimating)
        {
            // ...TurnManager'a animasyonun bittiğini bildir ki sayaç takılı kalmasın.
            if (TurnManager.Instance != null)
            {
                TurnManager.Instance.ReportAnimationEnd();
            }
        }
        
        // TurnManager'dan kaydı silme işlemi (bu zaten olmalı).
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.Unregister(this);
        }
    }

    public void Init(int x, int y)
    {
        this.X = x;
        this.Y = y;
        MaxHealth = startingHealth;
        CurrentHealth = MaxHealth;
        // Başlangıçta rastgele bir yöne baksın.
        Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        lastFacingDirection = directions[UnityEngine.Random.Range(0, directions.Length)];
    }

    #endregion

    #region Tur Tabanlı Eylemler (ITurnBased)

    public void ResetTurn() => HasActedThisTurn = false;

    // ITurnBased'in yeni metodu
    public IGameAction GetAction()
    {
        if (HasActedThisTurn) return null;
        
        HasActedThisTurn = true; // Düşman her tur bir şey yapmaya çalışır.

        turnCounter++;
        if (turnCounter >= turnsToShoot)
        {
            turnCounter = 0;
            // Shoot in the current facing direction
            Shoot(lastFacingDirection);
            return null;
        }
        else
        {
            // 50% chance to change direction
            if (UnityEngine.Random.value > 0.5f)
            {
                // 25% chance for each direction
                Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
                lastFacingDirection = directions[UnityEngine.Random.Range(0, directions.Length)];
            }
            
            // Try to move in the current facing direction
            return new MoveAction(this, lastFacingDirection);
        }
    }

    // IMovable'ın yeni metodu
    public void StartMoveAnimation(Vector3 targetPosition)
    {
        StartCoroutine(SmoothMove(targetPosition));
    }
    #endregion

    #region Hasar ve Yok Olma (IDamageable)
    public void TakeDamage(int damageAmount)
    {
        CurrentHealth -= damageAmount;
        OnHealthChanged?.Invoke();

        StartCoroutine(FlashColor(Color.red));

        if (CurrentHealth <= 0)
        {
            Die();
        }
    }

    public void Heal(int healAmount)
    {
        CurrentHealth += healAmount;
        if (CurrentHealth > MaxHealth) CurrentHealth = MaxHealth;
        OnHealthChanged?.Invoke();
        StartCoroutine(FlashColor(Color.green));
    }

    private void Die()
    {
        Debug.Log($"[EnemyShooterTile] EnemyShooter at ({X},{Y}) dying. LevelLoader'a cleanup delegating...");
        
        var ll = LevelLoader.instance;
        if (ll != null)
        {
            // Use centralized tile destruction - LevelLoader handles everything
            ll.DestroyTileAt(X, Y);
        }
        else
        {
            Debug.LogError("[EnemyShooterTile] LevelLoader instance not found! Manual cleanup fallback.");
            Destroy(gameObject);
        }
    }
    #endregion

    #region Hareket ve Diğer Eylemler

    public void OnMoved(int newX, int newY) { this.X = newX; this.Y = newY; }

    // Metodun adını ve imzasını daha spesifik hale getirelim.
    public void Shoot(Vector2Int direction)
    {
        if (projectilePrefab == null) return;
        
        int startX = X + direction.x;
        int startY = Y + direction.y;
        var ll = LevelLoader.instance;

        // Güvenlik kontrolleri (harita dışı, geçilemezlik)
        if (startX < 0 || startX >= ll.Width || startY < 0 || startY >= ll.Height) return;
        if (!MovementHelper.IsTilePassable(this, TileSymbols.DataSymbolToType(ll.levelMap[startX, startY])))
        {
            // Eğer hedefte duvar gibi bir şey varsa, ona hasar ver ama mermi oluşturma.
            ll.tileObjects[startX, startY]?.GetComponent<IDamageable>()?.TakeDamage(1);
            return;
        }

        // Mermiyi oluştururken, son parametre olarak KENDİ TİPİMİZİ (`this.TileType`) gönderiyoruz.
        Projectile.Spawn(this.projectilePrefab, startX, startY, direction, this.TileType);
    }

    #endregion

    #region Görsel Efektler

    private IEnumerator SmoothMove(Vector3 targetPosition)
    {
        // --- BAYRAKLARI AYARLA ---
        isAnimating = true;
        TurnManager.Instance.ReportAnimationStart();
        
        Vector3 startPosition = transform.position;
        float elapsedTime = 0f;
        float moveDuration = (TurnManager.Instance != null) ? (TurnManager.Instance.animationInterval > 0 ? TurnManager.Instance.animationInterval : TurnManager.Instance.turnInterval * 0.5f) : 0.1f;
        while (elapsedTime < moveDuration)
        {
            transform.position = Vector3.Lerp(startPosition, targetPosition, elapsedTime / moveDuration);
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        transform.position = targetPosition;
        
        TurnManager.Instance.ReportAnimationEnd();
        isAnimating = false;
    }
    private IEnumerator FlashColor(Color flashColor)
    {
        var visualImage = GetComponent<TileBase>()?.GetVisualImage();
        if (visualImage == null) yield break;

        Color originalColor = visualImage.color;
        visualImage.color = flashColor;

        yield return new WaitForSeconds(TurnManager.Instance.turnInterval * 0.8f);

        if (visualImage != null)
        {
            visualImage.color = originalColor;
        }
    }
    #endregion
}
