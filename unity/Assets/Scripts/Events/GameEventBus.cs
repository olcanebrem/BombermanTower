using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central event bus for loose coupling between game systems
/// Singleton pattern for global access across scenes
/// </summary>
public class GameEventBus : MonoBehaviour
{
    public static GameEventBus Instance { get; private set; }
    
    private readonly Dictionary<Type, List<Delegate>> eventHandlers = new();
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }
    
    /// <summary>
    /// Subscribe to an event
    /// </summary>
    public void Subscribe<T>(Action<T> handler) where T : IGameEvent
    {
        var eventType = typeof(T);
        
        if (!eventHandlers.ContainsKey(eventType))
        {
            eventHandlers[eventType] = new List<Delegate>();
        }
        
        eventHandlers[eventType].Add(handler);
    }
    
    /// <summary>
    /// Unsubscribe from an event
    /// </summary>
    public void Unsubscribe<T>(Action<T> handler) where T : IGameEvent
    {
        var eventType = typeof(T);
        
        if (eventHandlers.ContainsKey(eventType))
        {
            eventHandlers[eventType].Remove(handler);
            
            if (eventHandlers[eventType].Count == 0)
            {
                eventHandlers.Remove(eventType);
            }
        }
    }
    
    /// <summary>
    /// Publish an event to all subscribers
    /// </summary>
    public void Publish<T>(T eventData) where T : IGameEvent
    {
        var eventType = typeof(T);
        
        if (eventHandlers.ContainsKey(eventType))
        {
            var handlers = new List<Delegate>(eventHandlers[eventType]);
            
            foreach (var handler in handlers)
            {
                try
                {
                    ((Action<T>)handler).Invoke(eventData);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error handling event {eventType.Name}: {ex.Message}");
                }
            }
        }
    }
    
    /// <summary>
    /// Clear all event handlers (useful for cleanup)
    /// </summary>
    public void ClearAllHandlers()
    {
        eventHandlers.Clear();
    }
    
    /// <summary>
    /// Get subscriber count for debugging
    /// </summary>
    public int GetSubscriberCount<T>() where T : IGameEvent
    {
        var eventType = typeof(T);
        return eventHandlers.ContainsKey(eventType) ? eventHandlers[eventType].Count : 0;
    }
}