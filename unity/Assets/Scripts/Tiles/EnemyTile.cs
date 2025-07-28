using UnityEngine;
public class EnemyTile : TileBehavior
{
    public float moveInterval = 2f;
    private float timer;

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= moveInterval)
        {
            timer = 0;
            MoveRandomly();
        }
    }

    void MoveRandomly()
    {
        // Basit bir random hareket (4 yön)
        Vector2Int dir = Random.Range(0, 4) switch
        {
            0 => Vector2Int.up,
            1 => Vector2Int.down,
            2 => Vector2Int.left,
            _ => Vector2Int.right
        };

        // Move logic burada
    }
}
