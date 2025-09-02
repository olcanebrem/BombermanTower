using UnityEngine;

/// <summary>
/// Service interface for player management
/// Handles player lifecycle, spawning, and registration with game systems
/// </summary>
public interface IPlayerService
{
    // Player creation and destruction
    PlayerController CreatePlayerAtPosition(Vector2Int gridPosition, Vector3 worldPosition);
    void DestroyCurrentPlayer();
    void ClearAllPlayers();
    
    // Player registration
    void RegisterPlayerWithSystems(PlayerController player);
    void UnregisterPlayerFromSystems(PlayerController player);
    
    // Player state
    PlayerController GetCurrentPlayer();
    bool HasCurrentPlayer();
    Vector2Int GetPlayerSpawnPosition();
    
    // Player validation
    bool ValidatePlayerSpawnPosition(Vector2Int position);
    bool IsPlayerPositionSafe(Vector2Int position);
    
    // Movement and placement
    bool TryMovePlayer(Vector2Int fromPos, Vector2Int toPos);
    bool PlacePlayerInGrid(PlayerController player, Vector2Int position);
    
    // Properties
    GameObject PlayerPrefab { get; set; }
    PlayerController CurrentPlayer { get; }
    
    // Events
    event System.Action<PlayerController> OnPlayerCreated;
    event System.Action<PlayerController> OnPlayerDestroyed;
    event System.Action<PlayerController> OnPlayerRegistered;
    event System.Action<PlayerController> OnPlayerMoved;
}