using System.Collections;
using UnityEngine;
using UnityEngine.UI;
public class BombTile : MonoBehaviour, ITurnBased
{
    public int x, y;
    public int explosionRange;
    public float turnDuration;
    private bool exploded = false;
    private int turnsElapsed = 0;
    public bool HasActedThisTurn { get; set; }
    public void ResetTurn() { HasActedThisTurn = false; }

    Text text;

    void Start()
    {
        gameObject.name = "Bomb";
        text = GetComponent<Text>();
    }
    void OnEnable()
    {
        // Önce kendini listeye kaydet
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.Register(this);
        }
        // Sonra event'e abone ol
        TurnManager.OnTurnAdvanced += OnTurn;
    }

    void OnDisable()
    {
        // Önce kaydını sil
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.Unregister(this);
        }
        // Sonra abonelikten çık
        TurnManager.OnTurnAdvanced -= OnTurn;
    }

    public void Init(int x, int y, int range, float turnDuration)
    {
        this.x = x;
        this.y = y;
        this.explosionRange = range;
        this.turnDuration = turnDuration;
    }

    void OnTurn()
    {
        if (HasActedThisTurn || exploded) return;

        turnsElapsed++;
        // UI'daki metni güncelle
        text.text = (3 - turnsElapsed).ToString();
        if (turnsElapsed >= 3) // 3 tur sonra patlasın
        {
            Explode();
        }
    }

    void Explode()
    {
        exploded = true;
        Debug.Log($"Bomb exploded at ({x},{y})");

        // Patlama efektini + şeklinde oluştur
        CreateExplosionAt(x, y);

        for (int i = 1; i <= explosionRange; i++)
        {
            CreateExplosionAt(x + i, y);
            CreateExplosionAt(x - i, y);
            CreateExplosionAt(x, y + i);
            CreateExplosionAt(x, y - i);
        }

        // Patlama sonrası bombayı haritadan kaldır ve objeyi yok et
        LevelLoader.instance.levelMap[x, y] = TileSymbols.TypeToSymbol(TileType.Empty);
        Destroy(gameObject);
    }

    void CreateExplosionAt(int px, int py)
    {
        if (px < 0 || px >= LevelLoader.instance.width || py < 0 || py >= LevelLoader.instance.height)
            return;

        // Burada patlama görselini oluşturabilirsin (örn: Instantiate explosion prefab)
        // Ya da haritada 'X' karakterini koy:
        LevelLoader.instance.levelMap[px, py] = 'X'; // Patlama karakteri

        // İstersen bu patlama alanlarını ayrı objeler olarak instantiate edip belli süre sonra kaldırabilirsin.
    }
}
