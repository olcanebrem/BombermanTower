using UnityEngine;
using System.Collections;

public class ExplosionWave : TileBase, IMovable, ITurnBased, IInitializable
{
    public int X { get; private set; }
    public int Y { get; private set; }
    public TileType TileType => TileType.Explosion;
    public bool HasActedThisTurn { get; set; }

    private Vector2Int direction;
    private int stepsRemaining;

    void OnEnable() { if (TurnManager.Instance != null) TurnManager.Instance.Register(this); }
    void OnDisable() { if (TurnManager.Instance != null) TurnManager.Instance.Unregister(this); }

    public void Init(int x, int y) { this.X = x; this.Y = y; }
    public void OnMoved(int newX, int newY) { this.X = newX; this.Y = newY; }

    // Bu özel Spawn metodu, BombTile tarafından kullanılacak.
    public static void Spawn(GameObject prefab, int x, int y, Vector2Int dir, int range)
    {
        var ll = LevelLoader.instance;
        Vector3 pos = new Vector3(x * ll.tileSize, (ll.height - y - 1) * ll.tileSize, 0);
        GameObject waveGO = Instantiate(prefab, pos, Quaternion.identity, ll.transform);
        ExplosionWave wave = waveGO.GetComponent<ExplosionWave>();
        
        wave.Init(x, y);
        wave.direction = dir;
        wave.stepsRemaining = range;
        
        // Başlangıçta haritayı ve görselleri ayarla
        ll.levelMap[x, y] = TileSymbols.TypeToDataSymbol(wave.TileType);
        ll.tileObjects[x, y] = waveGO;
        wave.SetVisual(TileSymbols.TypeToVisualSymbol(wave.TileType));
    }

    public void ResetTurn() => HasActedThisTurn = false;

    public void ExecuteTurn()
    {
        if (HasActedThisTurn) return;

        // Eğer gidecek adım kalmadıysa, yok ol.
        if (stepsRemaining <= 0)
        {
            Die();
            return;
        }
        stepsRemaining--;

        // Bir sonraki kareyi analiz et ve hasar ver.
        int targetX = X + direction.x;
        int targetY = Y + direction.y;
        var ll = LevelLoader.instance;

        if (targetX >= 0 && targetX < ll.width && targetY >= 0 && targetY < ll.height)
        {
            GameObject targetObject = ll.tileObjects[targetX, targetY];
            if (targetObject != null)
            {
                var damageable = targetObject.GetComponent<IDamageable>();
                damageable?.TakeDamage(1); // 1 hasar ver
            }
        }

        // Hareketi mantıksal olarak dene.
        if (MovementHelper.TryMove(this, direction, out Vector3 targetPos))
        {
            StartCoroutine(SmoothMove(targetPos));
        }
        else
        {
            // Bir engele çarptıysa, hemen yok ol.
            Die();
        }

        HasActedThisTurn = true;
    }

    private void Die()
    {
        LevelLoader.instance.levelMap[X, Y] = TileSymbols.TypeToDataSymbol(TileType.Empty);
        LevelLoader.instance.tileObjects[X, Y] = null;
        Destroy(gameObject);
    }

    private IEnumerator SmoothMove(Vector3 targetPosition)
    {
        TurnManager.Instance.ReportAnimationStart();
        // ... (Diğer script'lerdeki SmoothMove ile aynı kod) ...
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
    }
}