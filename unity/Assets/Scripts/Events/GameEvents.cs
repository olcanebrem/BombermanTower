using UnityEngine;

/// <summary>
/// All game event definitions for the event bus system
/// </summary>

#region Level Lifecycle Events

public struct LevelLoadStarted : IGameEvent
{
    public string LevelId { get; }
    public string LevelName { get; }
    
    public LevelLoadStarted(string levelId, string levelName)
    {
        LevelId = levelId;
        LevelName = levelName;
    }
}

public struct LevelLoadCompleted : IGameEvent
{
    public string LevelId { get; }
    public string LevelName { get; }
    public HoudiniLevelData LevelData { get; }
    public bool Success { get; }
    
    public LevelLoadCompleted(string levelId, string levelName, HoudiniLevelData levelData, bool success)
    {
        LevelId = levelId;
        LevelName = levelName;
        LevelData = levelData;
        Success = success;
    }
}

public struct LevelCleanupStarted : IGameEvent
{
    public string PreviousLevelId { get; }
    
    public LevelCleanupStarted(string previousLevelId)
    {
        PreviousLevelId = previousLevelId;
    }
}

public struct LevelCleanupCompleted : IGameEvent
{
    public string PreviousLevelId { get; }
    public bool Success { get; }
    
    public LevelCleanupCompleted(string previousLevelId, bool success)
    {
        PreviousLevelId = previousLevelId;
        Success = success;
    }
}

#endregion

#region Player Lifecycle Events

public struct PlayerSpawned : IGameEvent
{
    public PlayerController Player { get; }
    public Vector2Int GridPosition { get; }
    public Vector3 WorldPosition { get; }
    
    public PlayerSpawned(PlayerController player, Vector2Int gridPosition, Vector3 worldPosition)
    {
        Player = player;
        GridPosition = gridPosition;
        WorldPosition = worldPosition;
    }
}

public struct PlayerRegistered : IGameEvent
{
    public PlayerController Player { get; }
    public bool HasMLAgent { get; }
    
    public PlayerRegistered(PlayerController player, bool hasMLAgent)
    {
        Player = player;
        HasMLAgent = hasMLAgent;
    }
}

public struct PlayerDestroyed : IGameEvent
{
    public PlayerController Player { get; }
    public string Reason { get; }
    
    public PlayerDestroyed(PlayerController player, string reason)
    {
        Player = player;
        Reason = reason;
    }
}

#endregion

#region ML-Agent Events

public struct AgentActionReceived : IGameEvent
{
    public PlayerController Player { get; }
    public int MoveAction { get; }
    public int BombAction { get; }
    public IGameAction CreatedAction { get; }
    
    public AgentActionReceived(PlayerController player, int moveAction, int bombAction, IGameAction createdAction)
    {
        Player = player;
        MoveAction = moveAction;
        BombAction = bombAction;
        CreatedAction = createdAction;
    }
}

public struct AgentDecisionRequested : IGameEvent
{
    public PlayerController Player { get; }
    public bool IsHeuristicMode { get; }
    
    public AgentDecisionRequested(PlayerController player, bool isHeuristicMode)
    {
        Player = player;
        IsHeuristicMode = isHeuristicMode;
    }
}

public struct AgentEpisodeEnded : IGameEvent
{
    public PlayerController Player { get; }
    public string Reason { get; }
    public int EpisodeSteps { get; }
    
    public AgentEpisodeEnded(PlayerController player, string reason, int episodeSteps)
    {
        Player = player;
        Reason = reason;
        EpisodeSteps = episodeSteps;
    }
}

#endregion

#region Container Management Events

public struct ContainerCreated : IGameEvent
{
    public GameObject Container { get; }
    public string ContainerType { get; }
    public string LevelId { get; }
    
    public ContainerCreated(GameObject container, string containerType, string levelId)
    {
        Container = container;
        ContainerType = containerType;
        LevelId = levelId;
    }
}

public struct ContainerCleared : IGameEvent
{
    public string ContainerType { get; }
    public string LevelId { get; }
    public int ObjectsDestroyed { get; }
    
    public ContainerCleared(string containerType, string levelId, int objectsDestroyed)
    {
        ContainerType = containerType;
        LevelId = levelId;
        ObjectsDestroyed = objectsDestroyed;
    }
}

#endregion