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
    public Vector2Int direction;
    public bool isFirstTurn = true;
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
        Vector3 pos = new Vector3(x * ll.tileSize, (ll.Height - y - 1) * ll.tileSize, 0);
        
        // 1. Fiziksel nesneyi oluştur.
        GameObject projectileGO = Instantiate(prefabToSpawn, pos, Quaternion.identity, ll.transform);
        Projectile proj = projectileGO.GetComponent<Projectile>();

        if (proj == null)
        {
            Debug.LogError($"Verilen prefabda ('{prefabToSpawn.name}') Projectile bileşeni bulunamadı!", prefabToSpawn);
            return null;
        }

        // 2. İç verilerini kur.
        proj.Init(x, y);
        proj.direction = direction;
        proj.ownerType = owner;

        // --- YENİ VE EN ÖNEMLİ KISIM: MANTIKSAL KAYIT ---
        // 3. Bu yeni mermiyi, oyunun mantıksal haritalarına kaydet.
        ll.levelMap[x, y] = TileSymbols.TypeToDataSymbol(proj.TileType);
        ll.tileObjects[x, y] = projectileGO;
        // -------------------------------------------------

        // 4. Görselini ayarla.
        Sprite projectileSprite = ll.spriteDatabase.GetSprite(proj.TileType);
        proj.GetComponent<TileBase>()?.SetVisual(projectileSprite);
        
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
    public IGameAction GetAction()
    {
        if (HasActedThisTurn) return null;
        
        HasActedThisTurn = true;
        // Her tur, benim görevim hareket etmektir. İşte eylem planım:
        return new ProjectileMoveAction(this);
    }

    // IMovable'ın yeni metodu
    public void StartMoveAnimation(Vector3 targetPosition)
    {
        StartCoroutine(SmoothMove(targetPosition));
    }
    #region Tur Tabanlı Eylemler (ITurnBased)

    public void ResetTurn() => HasActedThisTurn = false;

        public void ExecuteTurn()
    {
        if (HasActedThisTurn) return;
        if (isFirstTurn)
        {
            isFirstTurn = false;
            HasActedThisTurn = true;
            return;
        }

        // Hareketi, tüm kontrolleri ve güncellemeleri yapan merkezi metoda bırakalım.
        if (MovementHelper.TryMove(this, this.direction, out Vector3 targetPos))
        {
            StartCoroutine(SmoothMove(targetPos));
        }
        else
        {
            // Eğer hareket başarısız olduysa (bir engele çarptı),
            // MovementHelper zaten hasar verme işini halletti.
            // Bizim tek yapmamız gereken, kendimizi yok etmek.
            Die();
        }

        HasActedThisTurn = true;
    }

    public void Die()
    {
        var ll = LevelLoader.instance;

        // --- GÜVENLİK KİLİDİ ---
        // Haritadaki izimizi silmeden önce, o izin gerçekten bize ait olduğunu doğrula.
        // Bu, başka bir merminin yerini yanlışlıkla silmemizi engeller.
        if (X >= 0 && X < ll.Width && Y >= 0 && Y < ll.Height && ll.tileObjects[X, Y] == this.gameObject)
        {
            ll.levelMap[X, Y] = TileSymbols.TypeToDataSymbol(TileType.Empty);
            ll.tileObjects[X, Y] = null;
        }
        
        // GameObject'i her halükarda yok et.
        Destroy(gameObject);
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

    #endregion
}