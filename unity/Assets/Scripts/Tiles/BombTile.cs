using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using System;
public class BombTile : TileBase, ITurnBased, IInitializable
{   
    public int X { get; set; }
    public int Y { get; set; }
    public int explosionRange;
    public float turnDuration;

    private bool exploded = false;
    private int turnsElapsed = 0;
    private int turnsToExplode = 3;

    public bool HasActedThisTurn { get; set; }
    public void ResetTurn() { HasActedThisTurn = false; }

    public GameObject explosionPrefab; 
    Text text;
    
    void OnEnable()
    {
        // Kendini TurnManager'ın listesine kaydettirir.
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.Register(this);
        }
    }

    void OnDisable()
    {
        // Kendini TurnManager'ın listesinden siler.
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.Unregister(this);
        }
    }
    
    void Start()
    {
        gameObject.name = "Bomb";
        text = GetComponent<Text>();
    }
    
    public void Init(int x, int y)
    {
        this.X = x;
        this.Y = y;
        
    }
    
    void OnTurn()
    {
        if (HasActedThisTurn || exploded) return;

        turnsElapsed++;
        if (turnsElapsed >= turnsToExplode)
        {
            // Explode metodunu doğrudan çağırmak yerine, Coroutine'i başlatıyoruz.
            StartCoroutine(ExplosionCoroutine());
        }
        HasActedThisTurn = true;
    }

    // --- Patlama Mantığı (Coroutine ile) ---
    private IEnumerator ExplosionCoroutine()
    {
        exploded = true;
        float delay = 0.05f; // Her bir patlama halkası arasındaki saniye cinsinden gecikme

        // Önce bombanın kendi görselini haritadan kaldır.
        LevelLoader.instance.levelMap[X, Y] = TileSymbols.TypeToDataSymbol(TileType.Empty);
        GetComponent<CanvasRenderer>().SetAlpha(0); // Bombayı görünmez yap ama script'in çalışması için objeyi yok etme

        // Merkezden başla
        CreateExplosionAt(X, Y);
        yield return new WaitForSeconds(delay);

        // Halkaları dışa doğru oluştur
        for (int i = 1; i <= explosionRange; i++)
        {
            CreateExplosionAt(X + i, Y);
            CreateExplosionAt(X - i, Y);
            CreateExplosionAt(X, Y + i);
            CreateExplosionAt(X, Y - i);
            yield return new WaitForSeconds(delay);
        }

        // Patlama bitti, şimdi bombanın kendisini tamamen yok et.
        Destroy(gameObject);
    }
    public void ExecuteTurn()
    {
        if (HasActedThisTurn) return;

        OnTurn();
        
        HasActedThisTurn = true;
    }
        void CreateExplosionAt(int px, int py)
    {
        var ll = LevelLoader.instance;
        if (px < 0 || px >= ll.width || py < 0 || py >= ll.height) return;

        // --- CASUS KODU ---
        // Bu patlamanın kimi hedef aldığını bize söyle.
        TileType targetType = TileSymbols.DataSymbolToType(ll.levelMap[px, py]);
        Debug.LogWarning($"Patlama ({px},{py}) koordinatını etkiliyor. Hedefteki Tip: {targetType}");
        // ------------------

        // Hedefteki nesneyi bul ve potansiyel olarak yok et.
        GameObject targetObject = ll.tileObjects[px, py];
        if (targetObject != null)
        {
            // EĞER HEDEF OYUNCUYSA, KIRMIZI ALARM VER!
            if (targetObject.GetComponent<PlayerController>() != null)
            {
                Debug.LogError($"!!! PATLAMA OYUNCUYU VURDU !!! ({px},{py})", targetObject);
            }
            
            // Burada hedefi yok eden bir kod olabilir.
            // Örneğin: Destroy(targetObject);
        }

        // ... Geri kalan patlama oluşturma kodlarınız ...
        // Örneğin: Instantiate(explosionPrefab, ...);
    }
}
