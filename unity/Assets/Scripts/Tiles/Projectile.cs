using UnityEngine;
using System;
using System.Collections;
public class Projectile : TileBase, IMovable, ITurnBased, IInitializable, IDamageable
{
    // --- Arayüzler ve Değişkenler ---
    public int X { get; private set; }
    public int Y { get; private set; }
    public TileType TileType => TileType.Projectile;
    public bool HasActedThisTurn { get; set; }
    private Vector2Int direction;
    private bool isFirstTurn = true;
    private bool isAnimating = false;

    //=========================================================================
    // KAYIT VE KURULUM
    //=========================================================================
    void OnEnable() { if (TurnManager.Instance != null) TurnManager.Instance.Register(this); }
    void OnDisable() 
    {
    if (isAnimating)
        {
            // ...TurnManager'a animasyonun bittiğini bildir ki sayaç takılı kalmasın.
            if (TurnManager.Instance != null) TurnManager.Instance.ReportAnimationEnd();
            TurnManager.Instance.Unregister(this);
        }
    }
    public void Init(int x, int y) { this.X = x; this.Y = y; this.MaxHealth = 1; this.CurrentHealth = MaxHealth; }

    public static Projectile Spawn(GameObject prefabToSpawn, int x, int y, Vector2Int direction)
    {
        var ll = LevelLoader.instance;
        Vector3 pos = new Vector3(x * ll.tileSize, (ll.height - y - 1) * ll.tileSize, 0);
        GameObject projectileGO = Instantiate(prefabToSpawn, pos, Quaternion.identity, ll.transform);
        Projectile proj = projectileGO.GetComponent<Projectile>();
        proj.Init(x, y);
        proj.direction = direction;
        return proj;
    }
    
    public int CurrentHealth { get; private set; }
    public int MaxHealth { get; private set; }
    public event Action OnHealthChanged;

    public void TakeDamage(int damageAmount)
    {
        CurrentHealth -= damageAmount;
        OnHealthChanged?.Invoke();
    }
    void Start()
    {
        SetVisual(TileSymbols.TypeToVisualSymbol(this.TileType));

        float angle = 0f;

        // PlayerController'daki mantığın aynısını kullanalım:
        // Vector2Int.down -> Görsel olarak YUKARI
        // Vector2Int.up   -> Görsel olarak AŞAĞI

        if (direction == Vector2Int.right) // Görsel olarak SAĞ
        {
            angle = 0f;
        }
        else if (direction == Vector2Int.left) // Görsel olarak SOL
        {
            angle = 180f;
        }
        else if (direction == Vector2Int.down) // Görsel olarak YUKARI
        {
            angle = 90f;
        }
        else if (direction == Vector2Int.up) // Görsel olarak AŞAĞI
        {
            angle = 270f;
        }

        transform.rotation = Quaternion.Euler(0, 0, angle);
    }

    //=========================================================================
    // TUR TABANLI EYLEMLER (ITurnBased)
    //=========================================================================
    public void ResetTurn() => HasActedThisTurn = false;

    public void ExecuteTurn()
    {
        if (HasActedThisTurn) return;

        // Merminin, doğduğu ilk turda hareket etmesini engelle.
        if (isFirstTurn)
        {
            isFirstTurn = false;
            HasActedThisTurn = true;
            return;
        }

        // --- YENİ HAREKET MANTIĞI ---
        // 1. MovementHelper'a mantıksal bir hareket denemesi yaptır.
        if (MovementHelper.TryMove(this, this.direction, out Vector3 targetPos))
        {
            // 2. Eğer hareket mantıksal olarak başarılıysa, GÖRSEL animasyonu başlat.
            StartCoroutine(SmoothMove(targetPos));
        }
        else
        {
            // 3. Eğer hareket mantıksal olarak başarısızsa (bir engele çarptı), kendini yok et.
            Die();
        }
        // -----------------------------

        HasActedThisTurn = true;
    }


    //=========================================================================
    // HAREKET VE YOK OLMA MANTIĞI
    //=========================================================================
    public void OnMoved(int newX, int newY) { this.X = newX; this.Y = newY; }

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