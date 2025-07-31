using System.Collections;
using UnityEngine;
using UnityEngine.UI;
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

    void Start()
    {
        gameObject.name = "Bomb";
        text = GetComponent<Text>();
    }
    void OnEnable()
    {
        if (TurnManager.Instance != null) TurnManager.Instance.Register(this);
        TurnManager.OnTurnAdvanced += OnTurn;
    }

    void OnDisable()
    {
        if (TurnManager.Instance != null) TurnManager.Instance.Unregister(this);
        TurnManager.OnTurnAdvanced -= OnTurn;
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
        LevelLoader.instance.levelMap[X, Y] = TileSymbols.TypeToSymbol(TileType.Empty);
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

    void CreateExplosionAt(int px, int py)
    {
        var ll = LevelLoader.instance;
        if (px < 0 || px >= ll.width || py < 0 || py >= ll.height) return;

        // Hedefteki duvarı kırma gibi etkileşimler buraya eklenebilir.
        if (TileSymbols.SymbolToType(ll.levelMap[px,py]) == TileType.Wall) return;

        // Mantıksal haritayı 'patlama' olarak işaretle
        ll.levelMap[px, py] = '※'; // Patlama için özel bir sembol

        // Explosion prefabını oluştur
        GameObject explosionGO = Instantiate(explosionPrefab, 
            new Vector3(px * ll.tileSize, (ll.height - py - 1) * ll.tileSize, 0), 
            Quaternion.identity, ll.transform);
            
        // Oluşturulan patlamayı kur
        explosionGO.GetComponent<IInitializable>()?.Init(px, py);
        explosionGO.GetComponent<TileBase>()?.SetVisual('※');
    }

}
