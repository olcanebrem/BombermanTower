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
    [SerializeField] private int turnsToShoot = 4;
    private bool isAnimating = false;

    // --- YENİ "HAFIZA" DEĞİŞKENİ ---
    private Vector2Int lastFacingDirection;
    // --------------------------------

    // --- Can Sistemi ---
    [SerializeField] private int startingHealth = 1;
    public int CurrentHealth { get; private set; }
    public int MaxHealth { get; private set; }
    public event Action OnHealthChanged;

    #region Kayıt ve Kurulum

    void OnEnable() { if (TurnManager.Instance != null) TurnManager.Instance.Register(this); }
    void OnDisable() { /* ... önceki gibi ... */ }

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

    public void ExecuteTurn()
    {
        if (HasActedThisTurn) return;

        turnCounter++;
        if (turnCounter >= turnsToShoot)
        {
            // Ateş etme zamanı geldi. "Hafızadaki" yöne doğru ateş et.
            Shoot(lastFacingDirection);
            turnCounter = 0;
        }
        else
        {
            // %50 ihtimalle hareket etmeyi dene.
            if (UnityEngine.Random.value > 0.5f)
            {
                Vector2Int[] cardinalDirections = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
                Vector2Int moveDirection = cardinalDirections[UnityEngine.Random.Range(0, cardinalDirections.Length)];
                
                if (MovementHelper.TryMove(this, moveDirection, out Vector3 targetPos))
                {
                    // --- YENİ HAFIZA GÜNCELLEMESİ ---
                    // Başarıyla hareket ettiyse, yeni baktığı yön bu olur.
                    lastFacingDirection = moveDirection;
                    // ---------------------------------
                    StartCoroutine(SmoothMove(targetPos));
                }
            }
        }
        
        HasActedThisTurn = true;
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
        // Mantıksal ve nesne haritalarındaki izini temizle.
        var ll = LevelLoader.instance;
        ll.levelMap[X, Y] = TileSymbols.TypeToDataSymbol(TileType.Empty);
        ll.tileObjects[X, Y] = null;
        
        // GameObject'i yok et.
        Destroy(gameObject);
    }
    #endregion

    #region Hareket ve Diğer Eylemler

    public void OnMoved(int newX, int newY) { this.X = newX; this.Y = newY; }

    // Metodun adını ve imzasını daha spesifik hale getirelim.
    void Shoot(Vector2Int direction)
    {
        if (projectilePrefab == null) return;
        
        int startX = X + direction.x;
        int startY = Y + direction.y;
        var ll = LevelLoader.instance;

        // Güvenlik kontrolleri (harita dışı, geçilemezlik)
        if (startX < 0 || startX >= ll.width || startY < 0 || startY >= ll.height) return;
        if (!MovementHelper.IsTilePassable(TileSymbols.DataSymbolToType(ll.levelMap[startX, startY])))
        {
            // Eğer hedefte duvar gibi bir şey varsa, ona hasar ver ama mermi oluşturma.
            ll.tileObjects[startX, startY]?.GetComponent<IDamageable>()?.TakeDamage(1);
            return;
        }

        // Mermiyi oluştur ve kur.
        Projectile.Spawn(this.projectilePrefab, startX, startY, direction);
    }

    #endregion

    #region Görsel Efektler

    private IEnumerator SmoothMove(Vector3 targetPosition)
    {
        isAnimating = true;
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
