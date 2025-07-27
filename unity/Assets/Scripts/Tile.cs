using UnityEngine;

public class Tile : MonoBehaviour
{
    public TileType type;
    public bool isWalkable = true;
    public bool isDestructible = false;
    
    private void Start()
    {
        // Initialize tile based on type
        InitializeTile();
    }
    
    private void InitializeTile()
    {
        switch (type)
        {
            case TileType.Wall:
                isWalkable = false;
                isDestructible = false;
                break;
            case TileType.Breakable:
                isWalkable = false;
                isDestructible = true;
                break;
            case TileType.Empty:
            default:
                isWalkable = true;
                isDestructible = false;
                break;
        }
    }
    
    public void DestroyTile()
    {
        if (isDestructible)
        {
            // Add any destruction effects or logic here
            Destroy(gameObject);
        }
    }
}
