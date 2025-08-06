using UnityEngine;
using System.Collections;
using Debug = UnityEngine.Debug;

public class Projectile : TileBase, IMovable, ITurnBased, IInitializable
{
    #region Arayüzler ve Değişkenler

    // --- IMovable ---
    public int X { get; private set; }
    public int Y { get; private set; }
    public TileType TileType => TileType.Projectile;

    // --- ITurnBased ---
    public bool HasActedThisTurn { get; set; }
    private bool isAnimating = false;
    // --- Sınıfa Özgü ---
    private Vector2Int direction;
    private bool isFirstTurn = true;
    private Transform visualTransform;
    private TileType ownerType;
    private Vector3 targetPos;
    #endregion

    #region Kayıt ve Kurulum

    void OnEnable()
    {
        if (TurnManager.Instance != null) TurnManager.Instance.Register(this);
    }

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
    }

    /// <summary>
    /// Bir mermi oluşturur, kurar ve görselini ayarlar.
    /// </summary>
    public static Projectile Spawn(GameObject prefabToSpawn, int x, int y, Vector2Int direction, TileType owner)
    {
        var ll = LevelLoader.instance;
        Vector3 pos = new Vector3(x * ll.tileSize, (ll.height - y - 1) * ll.tileSize, 0);
        GameObject projectileGO = Instantiate(prefabToSpawn, pos, Quaternion.identity, ll.transform);
        Projectile proj = projectileGO.GetComponent<Projectile>();

        if (proj != null)
        {
            proj.Init(x, y);
            proj.direction = direction;
            proj.ownerType = owner;
            proj.targetPos = new Vector3(x * ll.tileSize, (ll.height - y - 1) * ll.tileSize, 0);
            // Görselini ayarla
            Sprite projectileSprite = ll.spriteDatabase.GetSprite(TileType.Projectile);
            proj.GetComponent<TileBase>()?.SetVisual(projectileSprite);
        }
        return proj;
        
    }

    // Start metodu, nesne oluşturulduktan sonra SADECE rotasyonu ayarlar.
     void Start()
    {
        // 1. Görselin transform'una olan referansı bul ve sakla.
        visualTransform = transform.Find("Visual");
        if (visualTransform == null)
        {
            Debug.LogError("Prefabda 'Visual' adında bir alt nesne bulunamadı!", gameObject);
            return;
        }

        // 2. Rotasyonu, konteynere değil, SADECE görsele uygula.
        float angle = 0f;
        if (direction == Vector2Int.right) angle = 0f;
        else if (direction == Vector2Int.left) angle = 180f;
        else if (direction == Vector2Int.down) angle = 90f;
        else if (direction == Vector2Int.up) angle = -90f;
        
        visualTransform.rotation = Quaternion.Euler(0, 0, angle);
    }

    #endregion

    #region Tur Tabanlı Eylemler (ITurnBased)

    public void ResetTurn() => HasActedThisTurn = false;

    public void ExecuteTurn()
    {
        if (HasActedThisTurn) return;
        if (isFirstTurn) {isFirstTurn = false;
            HasActedThisTurn = true;
            return; }

        // --- YENİ VE AKILLI ÇARPIŞMA MANTIĞI ---
            // Hareket başarısız olduysa (bir engele çarptı)...
            int targetX = X + direction.x;
            int targetY = Y + direction.y;
            var ll = LevelLoader.instance;

        if (targetX < 0 || targetX >= ll.width || targetY < 0 || targetY >= ll.height)
        {
            Die();
            return;
        }
        
        GameObject targetObject = ll.tileObjects[targetX, targetY];
        TileType targetType = TileSymbols.DataSymbolToType(ll.levelMap[targetX, targetY]);
        
        if (MovementHelper.TryMove(this, this.direction, out Vector3 targetPos))
        {
            // 2. EĞER HAREKET BAŞARILI OLDUYSA (hedef boştu)...
            //    ...sadece görsel animasyonu başlat.
            StartCoroutine(SmoothMove(targetPos));
        }
        else
        {
            // ...hedefin "canı" var mı diye bak.
            var damageable = targetObject?.GetComponent<IDamageable>();
            if (damageable != null)
            {
                // --- YENİ "DOST ATEŞİ" KONTROLÜ ---
                bool ownerIsEnemy = IsEnemy(this.ownerType);
                bool targetIsEnemy = IsEnemy(targetType);

                // Sadece farklı takımdakiler birbirine hasar verebilir.
                if (ownerIsEnemy != targetIsEnemy)
                {
                    damageable.TakeDamage(1);
                }
            }
            
            // ...ve her halükarda kendini yok et.
            Die();
        }
        // ------------------------------------

        HasActedThisTurn = true;
    }

    /// <summary>
    /// Bir TileType'ın düşman olup olmadığını belirleyen yardımcı metod.
    /// </summary>
    private bool IsEnemy(TileType type)
    {
        return type == TileType.Enemy || type == TileType.EnemyShooter;
    }

    #endregion

    #region Hareket ve Yok Olma

    public void OnMoved(int newX, int newY)
    {
        this.X = newX;
        this.Y = newY;
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

        private void Die()
    {
        var ll = LevelLoader.instance;

        // --- YENİ GÜVENLİK KİLİDİ ---
        // Haritadaki izini silmeden önce, kendi koordinatlarının geçerli olduğundan emin ol.
        if (X >= 0 && X < ll.width && Y >= 0 && Y < ll.height)
        {
            ll.levelMap[X, Y] = TileSymbols.TypeToDataSymbol(TileType.Empty);
            ll.tileObjects[X, Y] = null;
        }
        // -----------------------------
        
        // GameObject'i her halükarda yok et.
        Destroy(gameObject);
    }

    #endregion
}