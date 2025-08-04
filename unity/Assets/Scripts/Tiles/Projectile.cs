using UnityEngine;
using System;
public class Projectile : TileBase, IMovable, ITurnBased, IInitializable, IDamageable
{
    // --- Arayüzler ve Değişkenler ---
    public int X { get; private set; }
    public int Y { get; private set; }
    public TileType TileType => TileType.Projectile;
    public bool HasActedThisTurn { get; set; }
    private Vector2Int direction;
    private bool isFirstTurn = true;

    //=========================================================================
    // KAYIT VE KURULUM
    //=========================================================================
    void OnEnable() { if (TurnManager.Instance != null) TurnManager.Instance.Register(this); }
    void OnDisable() { if (TurnManager.Instance != null) TurnManager.Instance.Unregister(this); }
    public void Init(int x, int y) { this.X = x; this.Y = y; }

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
        if (direction == Vector2Int.up) angle = 0f;
        else if (direction == Vector2Int.down) angle = 180f;
        else if (direction == Vector2Int.right) angle = -90f;
        else if (direction == Vector2Int.left) angle = 90f;
        transform.rotation = Quaternion.Euler(0, 0, angle);
    }

    //=========================================================================
    // TUR TABANLI EYLEMLER (ITurnBased)
    //=========================================================================
    public void ResetTurn() => HasActedThisTurn = false;

    public void ExecuteTurn()
    {
        if (HasActedThisTurn) return;
        if (isFirstTurn) { isFirstTurn = false; HasActedThisTurn = true; return; }
        Move();
        HasActedThisTurn = true;
    }

    //=========================================================================
    // HAREKET VE YOK OLMA MANTIĞI
    //=========================================================================
    void Move()
    {
        int newX = X + direction.x;
        int newY = Y + direction.y;
        var ll = LevelLoader.instance;

        // 1. Sınır Kontrolü
        if (newX < 0 || newX >= ll.width || newY < 0 || newY >= ll.height)
        {
            Die();
            return;
        }

        // 2. Hedef Analizi
        TileType targetType = TileSymbols.DataSymbolToType(ll.levelMap[newX, newY]);

        // 3. Çarpışma Kontrolü
        if (!MovementHelper.TryMove(this, direction))
        {
            // Hedef geçilebilir değil. Bu bir engeldir.
            // Gelecekte, hedefin ne olduğuna göre farklı eylemler yapılabilir.
            // Örneğin: if (targetType == TileType.Player || targetType == TileType.Enemy) { DealDamage(); }
            
            // Şimdilik, neye çarparsa çarpsın, SADECE KENDİNİ yok et.
            Die();
            return;
        }

        // 4. Hareket Uygulaması (Eğer çarpışma olmadıysa)
        transform.position = new Vector3(newX * ll.tileSize, (ll.height - newY - 1) * ll.tileSize, 0);
        ll.levelMap[X, Y] = TileSymbols.TypeToDataSymbol(TileType.Empty);
        ll.levelMap[newX, newY] = TileSymbols.TypeToDataSymbol(TileType.Projectile);
        OnMoved(newX, newY);
    }

    public void OnMoved(int newX, int newY) { this.X = newX; this.Y = newY; }

    private void Die()
    {
        // Sadece kendi izini haritadan sil.
        LevelLoader.instance.levelMap[X, Y] = TileSymbols.TypeToDataSymbol(TileType.Empty);
        // Sadece kendi GameObject'ini yok et.
        Destroy(gameObject);
    }
}