using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Layered grid system for conflict-free Bomberman gameplay
/// Each layer handles specific game elements independently
/// </summary>
public class LayeredGridService : MonoBehaviour
{
    public static LayeredGridService Instance { get; private set; }
    
    [Header("Grid Dimensions")]
    [SerializeField] private int width;
    [SerializeField] private int height;
    [SerializeField] private int tileSize = 30;
    
    // Layer bitmasks for advanced collision detection
    [System.Flags]
    public enum LayerMask
    {
        None = 0,
        BlocksMovement = 1,
        BlocksFire = 2,
        Destructible = 4,
        Temporary = 8,
        Interactive = 16
    }
    
    // Individual layer grids
    private LayerMask[,] staticLayer;      // Walls, indestructibles (never change)
    private LayerMask[,] destructibleLayer; // Breakable blocks (destroyed by fire)
    private GameObject[,] destructibleObjectLayer; // GameObject references for destructible tiles
    private GameObject[,] actorLayer;       // Players, enemies (1 per cell max)
    private GameObject[,] bombLayer;        // Bombs (1 per cell max)  
    private GameObject[,] effectLayer;      // Explosions, fire (temporary)
    private GameObject[,] itemLayer;        // Collectibles (coins, health)
    
    // Quick access arrays for performance
    private List<GameObject> allActors = new List<GameObject>();
    private List<GameObject> allBombs = new List<GameObject>();
    private List<GameObject> allItems = new List<GameObject>();
    
    // Events for layer changes
    public System.Action<int, int, GameObject> OnActorAdded;
    public System.Action<int, int, GameObject> OnActorRemoved;
    public System.Action<int, int, GameObject> OnBombAdded;
    public System.Action<int, int, GameObject> OnBombRemoved;
    public System.Action<int, int, GameObject> OnItemAdded;
    public System.Action<int, int, GameObject> OnItemRemoved;
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    /// <summary>
    /// Initialize grid with specified dimensions
    /// </summary>
    public void Initialize(int gridWidth, int gridHeight)
    {
        Debug.Log($"[🏗️ LAYERED_INIT] Initialize called with dimensions: {gridWidth}x{gridHeight}");
        width = gridWidth;
        height = gridHeight;
        
        // Initialize all layer grids
        staticLayer = new LayerMask[width, height];
        destructibleLayer = new LayerMask[width, height];
        destructibleObjectLayer = new GameObject[width, height];
        actorLayer = new GameObject[width, height];
        bombLayer = new GameObject[width, height];
        effectLayer = new GameObject[width, height];
        itemLayer = new GameObject[width, height];
        
        // Clear tracking lists
        allActors.Clear();
        allBombs.Clear();
        allItems.Clear();
        
        Debug.Log($"[🏗️ LAYERED_INIT] Successfully initialized {width}x{height} layered grid system");
    }
    
    /// <summary>
    /// Check if a position is within grid bounds
    /// </summary>
    public bool IsValidPosition(int x, int y)
    {
        return x >= 0 && x < width && y >= 0 && y < height;
    }
    
    /// <summary>
    /// Get the tile type at a specific position by analyzing all layers
    /// </summary>
    public TileType GetTileTypeAt(int x, int y)
    {
        if (!IsValidPosition(x, y)) return TileType.Empty;
        
        // Check layers in priority order
        if (destructibleObjectLayer[x, y] != null)
        {
            var tileBase = destructibleObjectLayer[x, y].GetComponent<TileBase>();
            if (tileBase != null) return tileBase.TileType;
        }
        
        if (actorLayer[x, y] != null)
        {
            var tileBase = actorLayer[x, y].GetComponent<TileBase>();
            if (tileBase != null) return tileBase.TileType;
        }
        
        if (bombLayer[x, y] != null)
        {
            var tileBase = bombLayer[x, y].GetComponent<TileBase>();
            if (tileBase != null) return tileBase.TileType;
        }
        
        if (effectLayer[x, y] != null)
        {
            var tileBase = effectLayer[x, y].GetComponent<TileBase>();
            if (tileBase != null) return tileBase.TileType;
        }
        
        if (itemLayer[x, y] != null)
        {
            var tileBase = itemLayer[x, y].GetComponent<TileBase>();
            if (tileBase != null) return tileBase.TileType;
        }
        
        // Check destructible layer
        if ((destructibleLayer[x, y] & LayerMask.Destructible) != 0)
        {
            return TileType.Breakable;
        }
        
        // Check static layer
        if ((staticLayer[x, y] & LayerMask.BlocksMovement) != 0)
        {
            return TileType.Wall;
        }
        
        return TileType.Empty;
    }
    
    /// <summary>
    /// Check if position is walkable for the given actor
    /// </summary>
    public bool IsWalkable(int x, int y, GameObject actor = null)
    {
        if (!IsValidPosition(x, y)) return false;
        
        // Static obstacles always block
        if ((staticLayer[x, y] & LayerMask.BlocksMovement) != 0) return false;
        
        // Destructible blocks block movement
        if ((destructibleLayer[x, y] & LayerMask.BlocksMovement) != 0) return false;
        
        // Other actors block movement (except self)
        GameObject occupyingActor = actorLayer[x, y];
        if (occupyingActor != null && occupyingActor != actor) return false;
        
        // Bombs block movement (with soft-pass exception)
        GameObject bomb = bombLayer[x, y];
        if (bomb != null)
        {
            var bombComponent = bomb.GetComponent<BombTile>();
            if (bombComponent != null && bombComponent.Owner != actor)
            {
                // Only owner can pass through bomb on placement turn
                return false;
            }
        }
        
        // Items block movement for non-players
        GameObject item = itemLayer[x, y];
        if (item != null)
        {
            // Check if the actor is a player
            bool isPlayer = actor != null && actor.GetComponent<PlayerController>() != null;
            
            if (!isPlayer)
            {
                // Non-players (enemies) cannot walk through items
                return false;
            }
            
            // Players can walk through items (will trigger collection in AtomicMovementHelper)
        }
        
        return true;
    }
    
    /// <summary>
    /// Check if fire can pass through position
    /// </summary>
    public bool IsFirePassable(int x, int y)
    {
        if (!IsValidPosition(x, y)) return false;
        
        // Static fire blockers
        if ((staticLayer[x, y] & LayerMask.BlocksFire) != 0) return false;
        
        // Destructible blocks stop fire (but get destroyed)
        if ((destructibleLayer[x, y] & LayerMask.BlocksFire) != 0) return false;
        
        return true;
    }
    
    // =================================================================
    // STATIC LAYER OPERATIONS (Walls, Indestructible blocks)
    // =================================================================
    
    public void SetStaticTile(int x, int y, LayerMask mask)
    {
        if (!IsValidPosition(x, y)) return;
        staticLayer[x, y] = mask;
        
        // Debug only first few tiles to avoid spam
        if ((x < 3 && y < 3) || (x == 0) || (y == 0))
        {
            Debug.Log($"[🧱 STATIC_TILE] Set static tile at ({x},{y}) with mask: {mask}");
        }
    }
    
    public LayerMask GetStaticTile(int x, int y)
    {
        if (!IsValidPosition(x, y)) return LayerMask.None;
        return staticLayer[x, y];
    }
    
    // =================================================================
    // DESTRUCTIBLE LAYER OPERATIONS (Breakable blocks)
    // =================================================================
    
    public void SetDestructibleTile(int x, int y, LayerMask mask)
    {
        if (!IsValidPosition(x, y)) return;
        destructibleLayer[x, y] = mask;
    }
    
    public bool PlaceDestructibleObject(GameObject destructibleObj, int x, int y)
    {
        if (!IsValidPosition(x, y) || destructibleObj == null) return false;
        
        // Check if position is already occupied by a destructible object
        if (destructibleObjectLayer[x, y] != null)
        {
            Debug.LogWarning($"[LayeredGridService] Destructible position ({x}, {y}) already occupied by {destructibleObjectLayer[x, y].name}");
            return false;
        }
        
        // Set both the layer mask and object reference
        destructibleLayer[x, y] = LayerMask.Destructible | LayerMask.BlocksMovement | LayerMask.BlocksFire;
        destructibleObjectLayer[x, y] = destructibleObj;
        
        Debug.Log($"[LayeredGridService] Placed destructible object {destructibleObj.name} at ({x}, {y})");
        return true;
    }
    
    public GameObject GetDestructibleObjectAt(int x, int y)
    {
        if (!IsValidPosition(x, y)) return null;
        return destructibleObjectLayer[x, y];
    }
    
    public bool DestroyDestructibleTile(int x, int y)
    {
        if (!IsValidPosition(x, y)) return false;
        
        bool wasDestructible = (destructibleLayer[x, y] & LayerMask.Destructible) != 0;
        if (wasDestructible)
        {
            destructibleLayer[x, y] = LayerMask.None;
            destructibleObjectLayer[x, y] = null; // Clear object reference too
            Debug.Log($"[LayeredGridService] Destroyed destructible tile at ({x}, {y})");
        }
        return wasDestructible;
    }
    
    public LayerMask GetDestructibleTile(int x, int y)
    {
        if (!IsValidPosition(x, y)) return LayerMask.None;
        return destructibleLayer[x, y];
    }
    
    // =================================================================
    // ACTOR LAYER OPERATIONS (Players, Enemies)
    // =================================================================
    
    public bool PlaceActor(GameObject actor, int x, int y)
    {
        if (!IsValidPosition(x, y) || actor == null) return false;
        
        // Check if position is occupied by another actor
        if (actorLayer[x, y] != null && actorLayer[x, y] != actor)
        {
            Debug.LogWarning($"[LayeredGridService] Actor position ({x}, {y}) already occupied by actor {actorLayer[x, y].name}");
            return false;
        }
        
        // Check if there's an item at this position (actors shouldn't overlap with items)
        if (itemLayer[x, y] != null)
        {
            Debug.LogWarning($"[LayeredGridService] Cannot place actor at ({x}, {y}) - position occupied by item {itemLayer[x, y].name}");
            return false;
        }
        
        actorLayer[x, y] = actor;
        if (!allActors.Contains(actor))
        {
            allActors.Add(actor);
        }
        
        OnActorAdded?.Invoke(x, y, actor);
        Debug.Log($"[LayeredGridService] Placed actor {actor.name} at ({x}, {y})");
        return true;
    }
    
    public bool MoveActor(GameObject actor, int fromX, int fromY, int toX, int toY)
    {
        if (!IsValidPosition(fromX, fromY) || !IsValidPosition(toX, toY)) return false;
        if (actor == null || actorLayer[fromX, fromY] != actor) return false;
        
        // Check if destination is walkable
        if (!IsWalkable(toX, toY, actor)) return false;
        
        // Atomic move operation
        actorLayer[fromX, fromY] = null;
        actorLayer[toX, toY] = actor;
        
        OnActorRemoved?.Invoke(fromX, fromY, actor);
        OnActorAdded?.Invoke(toX, toY, actor);
        
        Debug.Log($"[LayeredGridService] Moved actor {actor.name} from ({fromX}, {fromY}) to ({toX}, {toY})");
        return true;
    }
    
    public void RemoveActor(GameObject actor, int x, int y)
    {
        if (!IsValidPosition(x, y) || actor == null) return;
        
        if (actorLayer[x, y] == actor)
        {
            actorLayer[x, y] = null;
            allActors.Remove(actor);
            OnActorRemoved?.Invoke(x, y, actor);
            Debug.Log($"[LayeredGridService] Removed actor {actor.name} from ({x}, {y})");
        }
    }
    
    public GameObject GetActorAt(int x, int y)
    {
        if (!IsValidPosition(x, y)) return null;
        return actorLayer[x, y];
    }
    
    // =================================================================
    // BOMB LAYER OPERATIONS
    // =================================================================
    
    public bool PlaceBomb(GameObject bomb, int x, int y)
    {
        if (!IsValidPosition(x, y) || bomb == null) return false;
        
        // Check if position already has a bomb
        if (bombLayer[x, y] != null)
        {
            Debug.LogWarning($"[LayeredGridService] Bomb position ({x}, {y}) already occupied");
            return false;
        }
        
        bombLayer[x, y] = bomb;
        if (!allBombs.Contains(bomb))
        {
            allBombs.Add(bomb);
        }
        
        OnBombAdded?.Invoke(x, y, bomb);
        Debug.Log($"[LayeredGridService] Placed bomb {bomb.name} at ({x}, {y})");
        return true;
    }
    
    public void RemoveBomb(GameObject bomb, int x, int y)
    {
        if (!IsValidPosition(x, y) || bomb == null) return;
        
        if (bombLayer[x, y] == bomb)
        {
            bombLayer[x, y] = null;
            allBombs.Remove(bomb);
            OnBombRemoved?.Invoke(x, y, bomb);
            Debug.Log($"[LayeredGridService] Removed bomb {bomb.name} from ({x}, {y})");
        }
    }
    
    public GameObject GetBombAt(int x, int y)
    {
        if (!IsValidPosition(x, y)) return null;
        return bombLayer[x, y];
    }
    
    // =================================================================
    // ITEM LAYER OPERATIONS (Collectibles)
    // =================================================================
    
    public bool PlaceItem(GameObject item, int x, int y)
    {
        if (!IsValidPosition(x, y) || item == null) return false;
        
        // Check if there's already an actor at this position (items shouldn't overlap with actors)
        if (actorLayer[x, y] != null)
        {
            Debug.LogWarning($"[LayeredGridService] Cannot place item at ({x}, {y}) - position occupied by actor {actorLayer[x, y].name}");
            return false;
        }
        
        // Check if there's already an item at this position
        if (itemLayer[x, y] != null)
        {
            Debug.LogWarning($"[LayeredGridService] Cannot place item at ({x}, {y}) - position already has item {itemLayer[x, y].name}");
            return false;
        }
        
        itemLayer[x, y] = item;
        if (!allItems.Contains(item))
        {
            allItems.Add(item);
        }
        
        OnItemAdded?.Invoke(x, y, item);
        Debug.Log($"[LayeredGridService] Placed item {item.name} at ({x}, {y})");
        return true;
    }
    
    public void RemoveItem(GameObject item, int x, int y)
    {
        if (!IsValidPosition(x, y) || item == null) return;
        
        if (itemLayer[x, y] == item)
        {
            itemLayer[x, y] = null;
            allItems.Remove(item);
            OnItemRemoved?.Invoke(x, y, item);
            Debug.Log($"[LayeredGridService] Removed item {item.name} from ({x}, {y})");
        }
    }
    
    public GameObject GetItemAt(int x, int y)
    {
        if (!IsValidPosition(x, y)) return null;
        return itemLayer[x, y];
    }
    
    // =================================================================
    // EFFECT LAYER OPERATIONS (Explosions, Fire)
    // =================================================================
    
    public void PlaceEffect(GameObject effect, int x, int y)
    {
        if (!IsValidPosition(x, y) || effect == null) return;
        
        effectLayer[x, y] = effect;
        Debug.Log($"[LayeredGridService] Placed effect {effect.name} at ({x}, {y})");
    }
    
    public void RemoveEffect(GameObject effect, int x, int y)
    {
        if (!IsValidPosition(x, y) || effect == null) return;
        
        if (effectLayer[x, y] == effect)
        {
            effectLayer[x, y] = null;
            Debug.Log($"[LayeredGridService] Removed effect {effect.name} from ({x}, {y})");
        }
    }
    
    public GameObject GetEffectAt(int x, int y)
    {
        if (!IsValidPosition(x, y)) return null;
        return effectLayer[x, y];
    }
    
    // =================================================================
    // UTILITY METHODS
    // =================================================================
    
    /// <summary>
    /// Get all objects at a specific position across all layers
    /// </summary>
    public List<GameObject> GetAllObjectsAt(int x, int y)
    {
        List<GameObject> objects = new List<GameObject>();
        
        if (!IsValidPosition(x, y)) return objects;
        
        if (destructibleObjectLayer[x, y] != null) objects.Add(destructibleObjectLayer[x, y]);
        if (actorLayer[x, y] != null) objects.Add(actorLayer[x, y]);
        if (bombLayer[x, y] != null) objects.Add(bombLayer[x, y]);
        if (effectLayer[x, y] != null) objects.Add(effectLayer[x, y]);
        if (itemLayer[x, y] != null) objects.Add(itemLayer[x, y]);
        
        return objects;
    }
    
    /// <summary>
    /// Clear all layers (for level transitions)
    /// </summary>
    public void ClearAllLayers()
    {
        if (staticLayer != null)
        {
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    staticLayer[x, y] = LayerMask.None;
                    destructibleLayer[x, y] = LayerMask.None;
                    destructibleObjectLayer[x, y] = null;
                    actorLayer[x, y] = null;
                    bombLayer[x, y] = null;
                    effectLayer[x, y] = null;
                    itemLayer[x, y] = null;
                }
            }
        }
        
        allActors.Clear();
        allBombs.Clear();
        allItems.Clear();
        
        Debug.Log("[LayeredGridService] All layers cleared");
    }
    
    /// <summary>
    /// Convert grid position to world position
    /// </summary>
    public Vector3 GridToWorld(int x, int y)
    {
        return new Vector3(x * tileSize, (height - y - 1) * tileSize, 0);
    }
    
    /// <summary>
    /// Convert world position to grid position
    /// </summary>
    public Vector2Int WorldToGrid(Vector3 worldPos)
    {
        return new Vector2Int(
            Mathf.FloorToInt(worldPos.x / tileSize + 0.5f),
            height - 1 - Mathf.FloorToInt(worldPos.y / tileSize + 0.5f)
        );
    }
    
    // Getters for external access
    public int Width => width;
    public int Height => height;
    public int TileSize => tileSize;
    public List<GameObject> AllActors => new List<GameObject>(allActors);
    public List<GameObject> AllBombs => new List<GameObject>(allBombs);
    public List<GameObject> AllItems => new List<GameObject>(allItems);
    
    // Get all effects from effect layer
    public List<GameObject> AllEffects
    {
        get
        {
            var effects = new List<GameObject>();
            if (effectLayer != null)
            {
                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        if (effectLayer[x, y] != null)
                        {
                            effects.Add(effectLayer[x, y]);
                        }
                    }
                }
            }
            return effects;
        }
    }
}