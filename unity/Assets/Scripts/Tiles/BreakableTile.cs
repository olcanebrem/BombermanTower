using UnityEngine;
using System.Collections;

public class BreakableTile : TileBase, ITurnBased, IInitializable, IDamageable
{
    // --- Arayüzler ve Değişkenler ---
    public int X { get; private set; }
    public int Y { get; private set; }
    public TileType TileType => TileType.Breakable;
    public bool HasActedThisTurn { get; set; }

    // --- Can Sistemi ---
    public int CurrentHealth { get; private set; }
    public int MaxHealth { get; private set; }
    public event System.Action OnHealthChanged;

    //=========================================================================
    // KAYIT VE KURULUM
    //=========================================================================
    void OnEnable() { if (TurnManager.Instance != null) TurnManager.Instance.Register(this); }
    void OnDisable() { if (TurnManager.Instance != null) TurnManager.Instance.Unregister(this); }

    public void Init(int x, int y)
    {
        this.X = x;
        this.Y = y;
        // Kırılabilir kutulara 1 can verelim.
        MaxHealth = 1;
        CurrentHealth = MaxHealth;
    }

    //=========================================================================
    // TUR VE HASAR MANTIĞI
    //=========================================================================
    public void ResetTurn() => HasActedThisTurn = false;

    // Kırılabilir kutular, sıraları geldiğinde hiçbir şey yapmazlar.
    public void ExecuteTurn()
    {
        if (HasActedThisTurn) return;
        HasActedThisTurn = true; // Pas geçmek de bir eylemdir.
    }

    public void TakeDamage(int damageAmount)
    {
        CurrentHealth -= damageAmount;
        OnHealthChanged?.Invoke(); // Belki bir "çatlama" efekti için

        // Hasar aldığında anlık olarak renk değiştirsin.
        StartCoroutine(FlashColor(Color.yellow));

        if (CurrentHealth <= 0)
        {
            Die();
        }
    }

    //=========================================================================
    // YOK OLMA VE GÖRSEL EFEKTLER
    //=========================================================================
    private void Die()
    {
        Debug.Log($"Breakable tile at ({X},{Y}) destroyed.");
        var ll = LevelLoader.instance;

        // --- EN ÖNEMLİ KISIM: MANTIĞI TEMİZLEME ---
        // 1. Mantıksal haritadaki ('levelMap') izini sil.
        ll.levelMap[X, Y] = TileSymbols.TypeToDataSymbol(TileType.Empty);

        // 2. Nesne haritasındaki ('tileObjects') referansını sil.
        ll.tileObjects[X, Y] = null;
        // -----------------------------------------

        // 3. Görsel GameObject'i yok et.
        Destroy(gameObject);
    }

    // PlayerController'dan kopyalanan standart hasar efekti.
    private IEnumerator FlashColor(Color flashColor)
    {
        var renderer = GetComponentInChildren<CanvasRenderer>();
        if (renderer == null) yield break;
        Color originalColor = renderer.GetColor();
        renderer.SetColor(flashColor);
        yield return new WaitForSeconds(TurnManager.Instance.turnInterval * 0.8f);
        if (renderer != null) renderer.SetColor(originalColor);
    }
}