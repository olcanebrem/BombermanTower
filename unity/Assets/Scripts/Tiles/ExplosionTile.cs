using UnityEngine;

public class ExplosionTile : TileBase, IInitializable
{
    public int X { get; private set; }
    public int Y { get; private set; }
    public override TileType TileType => TileType.Explosion;

    [Header("Explosion Settings")]
    [SerializeField] private float explosionDuration = 1f;

    private float timer;

    public void Init(int x, int y)
    {
        X = x;
        Y = y;
        timer = 0f;

        DealDamageAtPosition();

        Debug.Log($"[ExplosionTile] Spawned at ({X},{Y})");
    }

    private void Update()
    {
        timer += Time.deltaTime;
        if (timer >= explosionDuration)
        {
            Die();
        }
    }

    private void DealDamageAtPosition()
    {
        var ll = LevelLoader.instance;
        if (ll == null || X < 0 || X >= ll.Width || Y < 0 || Y >= ll.Height) return;

        GameObject target = ll.tileObjects[X, Y];
        if (target == null) return;

        if (target.TryGetComponent(out IDamageable dmg))
        {
            Debug.Log($"[ExplosionTile] Damaging {target.name} at ({X},{Y})");
            dmg.TakeDamage(1);
        }
    }

    private void Die()
    {
        var ll = LevelLoader.instance;
        if (ll != null && X >= 0 && X < ll.Width && Y >= 0 && Y < ll.Height && ll.tileObjects[X, Y] == gameObject)
        {
            ll.levelMap[X, Y] = TileSymbols.TypeToDataSymbol(TileType.Empty);
            ll.tileObjects[X, Y] = null;
        }

        Destroy(gameObject);
        Debug.Log($"[ExplosionTile] Finished at ({X},{Y})");
    }
}