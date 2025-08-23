using UnityEngine;
using System.Collections;
using System.Collections.Generic;
public class BombController : MonoBehaviour
{
    [Header("Bomb Settings")]
    public float explosionDelay = 3f;
    public int explosionRange = 2;
    public GameObject explosionPrefab;
    public LayerMask damageableLayers;
    
    [Header("Visual Effects")]
    public SpriteRenderer spriteRenderer;
    public Color warningColor = Color.red;
    private Color originalColor;
    
    private PlayerAgent owner;
    private EnvManager envManager;
    private bool hasExploded = false;
    private float timer;
    
    private void Start()
    {
        envManager = FindObjectOfType<EnvManager>();
        envManager.RegisterBomb(gameObject);
        
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
        
        if (spriteRenderer != null)
            originalColor = spriteRenderer.color;
        
        timer = explosionDelay;
        StartCoroutine(BombTimer());
    }
    
    public void SetOwner(PlayerAgent playerAgent)
    {
        owner = playerAgent;
    }
    
    private IEnumerator BombTimer()
    {
        while (timer > 0 && !hasExploded)
        {
            timer -= Time.deltaTime;
            
            // Visual warning effect
            if (spriteRenderer != null && timer <= 1f)
            {
                float blinkSpeed = Mathf.Lerp(2f, 10f, 1f - timer);
                float alpha = Mathf.PingPong(Time.time * blinkSpeed, 1f);
                spriteRenderer.color = Color.Lerp(originalColor, warningColor, alpha);
            }
            
            yield return null;
        }
        
        if (!hasExploded)
        {
            Explode();
        }
    }
    
    public void TriggerExplosion()
    {
        if (!hasExploded)
        {
            StopAllCoroutines();
            Explode();
        }
    }
    
    private void Explode()
    {
        if (hasExploded) return;
        hasExploded = true;
        
        Vector2Int bombGridPos = envManager.WorldToGrid(transform.position);
        
        // Create explosion pattern
        List<Vector2Int> explosionCells = new List<Vector2Int>();
        explosionCells.Add(bombGridPos); // Center explosion
        
        // Create cross-shaped explosion
        Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        
        foreach (Vector2Int direction in directions)
        {
            for (int i = 1; i <= explosionRange; i++)
            {
                Vector2Int explosionPos = bombGridPos + direction * i;
                
                // Check if explosion can continue using LevelManager
                bool shouldStop = false;
                bool isBreakable = false;
                
                if (LevelManager.Instance != null)
                {
                    char cellChar = LevelManager.Instance.GetCellAtGrid(explosionPos);
                    TileType cellType = TileSymbols.DataSymbolToType(cellChar);
                    
                    if (cellType == TileType.Wall) // Unbreakable wall - stop explosion
                    {
                        shouldStop = true;
                    }
                    else if (cellType == TileType.Breakable) // Breakable wall
                    {
                        isBreakable = true;
                        shouldStop = true;
                    }
                }
                
                if (shouldStop && !isBreakable)
                {
                    break; // Hit unbreakable wall
                }
                
                explosionCells.Add(explosionPos);
                
                if (isBreakable)
                {
                    // Handle breakable wall destruction through level system
                    if (owner != null)
                    {
                        owner.GetComponent<RewardSystem>().ApplyWallDestroyReward();
                    }
                    break; // Stop explosion after destroying breakable wall
                }
            }
        }
        
        // Apply explosion effects
        foreach (Vector2Int explosionPos in explosionCells)
        {
            ProcessExplosionAt(explosionPos);
        }
        
        // Create visual explosions
        StartCoroutine(CreateExplosionVisuals(explosionCells));
        
        // Note: Owner notification removed - handled through reward system
        
        // Unregister and destroy bomb
        envManager.UnregisterBomb(gameObject);
        Destroy(gameObject);
    }
    
    private void ProcessExplosionAt(Vector2Int gridPos)
    {
        // Register explosion in environment
        envManager.AddExplosion(gridPos, 1f);
        
        // Check for entities at this position
        Vector3 worldPos = envManager.GridToWorld(gridPos);
        Collider2D[] hitColliders = Physics2D.OverlapCircleAll(worldPos, 0.4f, damageableLayers);
        
        foreach (Collider2D collider in hitColliders)
        {
            ProcessExplosionHit(collider.gameObject, gridPos);
        }
        
        // Chain reaction - explode other bombs
        if (envManager.HasBombAt(gridPos))
        {
            GameObject[] allBombs = GameObject.FindGameObjectsWithTag("Bomb");
            foreach (GameObject bomb in allBombs)
            {
                if (bomb != gameObject)
                {
                    Vector2Int bombPos = envManager.WorldToGrid(bomb.transform.position);
                    if (bombPos == gridPos)
                    {
                        BombController otherBomb = bomb.GetComponent<BombController>();
                        if (otherBomb != null)
                        {
                            otherBomb.TriggerExplosion();
                        }
                    }
                }
            }
        }
    }
    
    private void ProcessExplosionHit(GameObject hitObject, Vector2Int explosionPos)
    {
        // Check if it's the player
        PlayerController player = hitObject.GetComponent<PlayerController>();
        if (player != null)
        {
            player.TakeDamage(1);
            
            // Apply self-damage penalty if it's the owner's player
            PlayerAgent playerAgent = player.GetComponent<PlayerAgent>();
            if (playerAgent == owner)
            {
                RewardSystem rewardSystem = playerAgent.GetComponent<RewardSystem>();
                if (rewardSystem != null)
                {
                    rewardSystem.ApplyBombSelfDamagePenalty();
                }
            }
            return;
        }
        
        // Note: Enemy damage not handled here - enemy kills tracked through action system in PlayerAgent
        
        // Check if it's a collectible that gets destroyed by bomb - apply penalty to owner
        if (hitObject.CompareTag("Collectible"))
        {
            if (owner != null)
            {
                RewardSystem rewardSystem = owner.GetComponent<RewardSystem>();
                if (rewardSystem != null)
                {
                    // Apply penalty for destroying collectibles with bomb
                    rewardSystem.ApplyCollectibleDestroyPenalty();
                }
            }
            Destroy(hitObject);
            return;
        }
    }
    
    private IEnumerator CreateExplosionVisuals(List<Vector2Int> explosionCells)
    {
        List<GameObject> explosionEffects = new List<GameObject>();
        
        // Create explosion visual effects
        foreach (Vector2Int cell in explosionCells)
        {
            Vector3 worldPos = envManager.GridToWorld(cell);
            
            if (explosionPrefab != null)
            {
                GameObject explosionEffect = Instantiate(explosionPrefab, worldPos, Quaternion.identity);
                explosionEffects.Add(explosionEffect);
            }
        }
        
        // Wait for explosion duration
        yield return new WaitForSeconds(1f);
        
        // Clean up explosion effects
        foreach (GameObject effect in explosionEffects)
        {
            if (effect != null)
            {
                Destroy(effect);
            }
        }
    }
    
    private void OnTriggerEnter2D(Collider2D other)
    {
        // Optional: Handle trigger-based explosion (e.g., when hit by another explosion)
        if (other.CompareTag("Explosion"))
        {
            TriggerExplosion();
        }
    }
    
    private void OnDestroy()
    {
        if (envManager != null)
        {
            envManager.UnregisterBomb(gameObject);
        }
    }
}