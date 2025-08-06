using UnityEngine;
using System.Collections;
using System;
using System.Diagnostics; // StackTrace için
using Debug = UnityEngine.Debug; // 'Debug' belirsizliğini çözer

public class EnemyTile : TileBase, IMovable, ITurnBased, IInitializable, IDamageable
{
    public int X { get; private set; }
    public int Y { get; private set; }
    public TileType TileType => TileType.Enemy;
    public bool HasActedThisTurn { get; set; }
    private bool isAnimating = false;

    [SerializeField] private int startingHealth = 1;
    public int CurrentHealth { get; private set; }
    public int MaxHealth { get; private set; }
    public event Action OnHealthChanged;

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
    public void Init(int x, int y) { this.X = x; this.Y = y; MaxHealth = startingHealth; CurrentHealth = MaxHealth; }
    public void OnMoved(int newX, int newY) { this.X = newX; this.Y = newY; }
    public void ResetTurn() => HasActedThisTurn = false;

    public void ExecuteTurn()
    {
        if (HasActedThisTurn) return;
        
        if (UnityEngine.Random.value > 0.5f)
        {
            Vector2Int[] cardinalDirections = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
            Vector2Int moveDirection = cardinalDirections[UnityEngine.Random.Range(0, cardinalDirections.Length)];
            
            if (MovementHelper.TryMove(this, moveDirection, out Vector3 targetPos))
            {
                 StartCoroutine(SmoothMove(targetPos));
            }
        }
        
        HasActedThisTurn = true;
    }

    public void TakeDamage(int damageAmount)
    {
        CurrentHealth -= damageAmount;
        UnityEngine.Debug.Log($"EnemyTile ({X},{Y}) {damageAmount} hasar aldı. Kalan can: {CurrentHealth}", this.gameObject);
        OnHealthChanged?.Invoke();
        StartCoroutine(FlashColor(Color.red));
        if (CurrentHealth <= 0)
        {
            Die();
        }
    }
    public void Heal(int amount) { }

    private void Die()
    {
        UnityEngine.Debug.LogError($"ENEMYTILE ÖLÜYOR! Konum: ({X},{Y}). Tetikleyici Zinciri:\n" + 
                       $"{new StackTrace().ToString()}", this.gameObject);
        
        var ll = LevelLoader.instance;
        ll.levelMap[X, Y] = TileSymbols.TypeToDataSymbol(TileType.Empty);
        ll.tileObjects[X, Y] = null;
        Destroy(gameObject);
    }

    private IEnumerator SmoothMove(Vector3 targetPosition)
    {
        // --- BAYRAKLARI AYARLA ---
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
        // -------------------------
    }

    private IEnumerator FlashColor(Color flashColor)
    {
        // Bu metod, PlayerController'daki ile birebir aynıdır.
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
}