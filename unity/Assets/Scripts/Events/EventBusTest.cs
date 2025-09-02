using UnityEngine;

/// <summary>
/// Simple test component to verify event bus functionality
/// Attach to any GameObject to test event publishing/subscribing
/// </summary>
public class EventBusTest : MonoBehaviour
{
    private void Start()
    {
        // Subscribe to events for testing
        if (GameEventBus.Instance != null)
        {
            GameEventBus.Instance.Subscribe<LevelLoadStarted>(OnLevelLoadStarted);
            GameEventBus.Instance.Subscribe<PlayerSpawned>(OnPlayerSpawned);
            GameEventBus.Instance.Subscribe<AgentActionReceived>(OnAgentActionReceived);
            
            Debug.Log("[EventBusTest] Subscribed to test events");
        }
        else
        {
            Debug.LogWarning("[EventBusTest] GameEventBus.Instance is null!");
        }
    }
    
    private void OnDestroy()
    {
        // Unsubscribe to prevent memory leaks
        if (GameEventBus.Instance != null)
        {
            GameEventBus.Instance.Unsubscribe<LevelLoadStarted>(OnLevelLoadStarted);
            GameEventBus.Instance.Unsubscribe<PlayerSpawned>(OnPlayerSpawned);
            GameEventBus.Instance.Unsubscribe<AgentActionReceived>(OnAgentActionReceived);
        }
    }
    
    private void OnLevelLoadStarted(LevelLoadStarted eventData)
    {
        Debug.Log($"[EventBusTest] ✅ Level load started: {eventData.LevelName} (ID: {eventData.LevelId})");
    }
    
    private void OnPlayerSpawned(PlayerSpawned eventData)
    {
        Debug.Log($"[EventBusTest] ✅ Player spawned: {eventData.Player?.name} at grid({eventData.GridPosition.x}, {eventData.GridPosition.y})");
    }
    
    private void OnAgentActionReceived(AgentActionReceived eventData)
    {
        Debug.Log($"[EventBusTest] ✅ Agent action received: Move={eventData.MoveAction}, Bomb={eventData.BombAction}, Action={eventData.CreatedAction?.GetType().Name}");
    }
}