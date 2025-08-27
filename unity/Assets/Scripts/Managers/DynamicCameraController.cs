using UnityEngine;
using System.Collections;

/// <summary>
/// Dinamik kamera konumlandırma sistemi
/// Harita boyutları ve player konumuna göre kamerayı otomatik olarak konumlandırır
/// </summary>
public class DynamicCameraController : MonoBehaviour
{
    [Header("Camera Settings")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private float smoothTime = 0.5f;
    [SerializeField] private float zOffset = -10f;
    
    [Header("Zoom Settings")]
    [SerializeField] private float minOrthographicSize = 5f;
    [SerializeField] private float maxOrthographicSize = 20f;
    [SerializeField] private float zoomPadding = 2f; // Harita kenarlarından ne kadar boşluk bırakılacak
    
    [Header("Position Blending")]
    [Range(0f, 1f)]
    [SerializeField] private float mapCenterWeight = 0.3f; // Harita merkezinin ağırlığı
    [Range(0f, 1f)]
    [SerializeField] private float playerWeight = 0.7f; // Player konumunun ağırlığı
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugGizmos = true;
    [SerializeField] private bool enableDebugLogs = false;
    
    // Component references
    private LevelLoader levelLoader;
    private PlayerController player;
    private Transform playerTransform;
    
    // Camera movement
    private Vector3 targetPosition;
    private Vector3 velocity;
    private float targetOrthographicSize;
    private float sizeVelocity;
    
    // Cache values
    private Vector3 mapCenter;
    private Vector2 mapBounds;
    private bool cacheValid = false;
    
    private void Awake()
    {
        // Get or assign camera
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
            if (targetCamera == null)
            {
                targetCamera = GetComponent<Camera>();
            }
        }
        
        if (targetCamera == null)
        {
            Debug.LogError("[DynamicCameraController] No camera found!");
            enabled = false;
            return;
        }
        
        // Ensure camera is orthographic for 2D game
        if (!targetCamera.orthographic)
        {
            Debug.LogWarning("[DynamicCameraController] Camera is not orthographic. Setting to orthographic mode.");
            targetCamera.orthographic = true;
        }
    }
    
    private void Start()
    {
        // Find level loader
        levelLoader = FindObjectOfType<LevelLoader>();
        if (levelLoader == null)
        {
            Debug.LogError("[DynamicCameraController] LevelLoader not found!");
            return;
        }
        
        // Find player
        FindPlayer();
        
        // Subscribe to level loading events
        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.OnLevelLoaded += OnLevelLoaded;
        }
        
        // Initial setup
        StartCoroutine(DelayedInitialSetup());
    }
    
    private void OnDestroy()
    {
        // Unsubscribe from events
        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.OnLevelLoaded -= OnLevelLoaded;
        }
    }
    
    private IEnumerator DelayedInitialSetup()
    {
        // Wait for level to be fully loaded
        yield return new WaitForSeconds(0.1f);
        
        UpdateCameraImmediate();
    }
    
    private void OnLevelLoaded(LevelData levelData)
    {
        // Invalidate cache when level changes
        cacheValid = false;
        
        // Find player again (in case it was recreated)
        FindPlayer();
        
        // Update camera position
        StartCoroutine(DelayedInitialSetup());
        
        if (enableDebugLogs)
        {
            Debug.Log($"[DynamicCameraController] Level loaded: {levelData.levelName} ({levelData.width}x{levelData.height})");
        }
    }
    
    private void FindPlayer()
    {
        player = FindObjectOfType<PlayerController>();
        if (player != null)
        {
            playerTransform = player.transform;
            if (enableDebugLogs)
                Debug.Log("[DynamicCameraController] Player found");
        }
        else
        {
            if (enableDebugLogs)
                Debug.LogWarning("[DynamicCameraController] Player not found");
        }
    }
    
    private void Update()
    {
        if (levelLoader == null || targetCamera == null) return;
        
        // Update cache if needed
        if (!cacheValid)
        {
            UpdateMapCache();
        }
        
        // Calculate target position and zoom
        CalculateTargetCameraSettings();
        
        // Smooth movement
        ApplySmoothCameraMovement();
    }
    
    private void UpdateMapCache()
    {
        if (levelLoader.Width <= 0 || levelLoader.Height <= 0)
        {
            cacheValid = false;
            return;
        }
        
        // Calculate map center in world coordinates
        float mapCenterX = (levelLoader.Width - 1) * levelLoader.tileSize * 0.5f;
        float mapCenterY = (levelLoader.Height - 1) * levelLoader.tileSize * 0.5f;
        mapCenter = new Vector3(mapCenterX, mapCenterY, zOffset);
        
        // Calculate map bounds
        mapBounds = new Vector2(
            levelLoader.Width * levelLoader.tileSize,
            levelLoader.Height * levelLoader.tileSize
        );
        
        cacheValid = true;
        
        if (enableDebugLogs)
        {
            Debug.Log($"[DynamicCameraController] Map cache updated - Center: {mapCenter}, Bounds: {mapBounds}");
        }
    }
    
    private void CalculateTargetCameraSettings()
    {
        if (!cacheValid) return;
        
        // Calculate target position as weighted average of map center and player position
        Vector3 targetPos = mapCenter;
        
        if (playerTransform != null)
        {
            Vector3 playerPos = new Vector3(playerTransform.position.x, playerTransform.position.y, zOffset);
            targetPos = Vector3.Lerp(mapCenter, playerPos, playerWeight);
        }
        
        targetPosition = targetPos;
        
        // Calculate optimal orthographic size to fit the entire map
        float mapWidth = mapBounds.x + zoomPadding * 2;
        float mapHeight = mapBounds.y + zoomPadding * 2;
        
        // Calculate size needed to fit map width and height
        float widthBasedSize = mapWidth / (targetCamera.aspect * 2f);
        float heightBasedSize = mapHeight / 2f;
        
        // Use the larger size to ensure entire map fits
        targetOrthographicSize = Mathf.Max(widthBasedSize, heightBasedSize);
        
        // Clamp to min/max values
        targetOrthographicSize = Mathf.Clamp(targetOrthographicSize, minOrthographicSize, maxOrthographicSize);
        
        if (enableDebugLogs)
        {
            Debug.Log($"[DynamicCameraController] Target - Pos: {targetPosition}, Size: {targetOrthographicSize}");
        }
    }
    
    private void ApplySmoothCameraMovement()
    {
        // Smooth position movement
        Vector3 currentPos = targetCamera.transform.position;
        Vector3 newPos = Vector3.SmoothDamp(currentPos, targetPosition, ref velocity, smoothTime);
        targetCamera.transform.position = newPos;
        
        // Smooth zoom
        float currentSize = targetCamera.orthographicSize;
        float newSize = Mathf.SmoothDamp(currentSize, targetOrthographicSize, ref sizeVelocity, smoothTime);
        targetCamera.orthographicSize = newSize;
    }
    
    /// <summary>
    /// Kamerayı anında hedef konuma taşır (smooth movement olmadan)
    /// </summary>
    public void UpdateCameraImmediate()
    {
        if (targetCamera == null) return;
        
        cacheValid = false;
        UpdateMapCache();
        CalculateTargetCameraSettings();
        
        targetCamera.transform.position = targetPosition;
        targetCamera.orthographicSize = targetOrthographicSize;
        
        // Reset velocity
        velocity = Vector3.zero;
        sizeVelocity = 0f;
        
        if (enableDebugLogs)
        {
            Debug.Log($"[DynamicCameraController] Camera updated immediately - Pos: {targetPosition}, Size: {targetOrthographicSize}");
        }
    }
    
    /// <summary>
    /// Kamera ayarlarını runtime'da değiştirmek için
    /// </summary>
    public void SetCameraSettings(float newMapCenterWeight, float newPlayerWeight, float newSmoothTime)
    {
        mapCenterWeight = Mathf.Clamp01(newMapCenterWeight);
        playerWeight = Mathf.Clamp01(newPlayerWeight);
        smoothTime = Mathf.Max(0.1f, newSmoothTime);
        
        // Normalize weights
        float totalWeight = mapCenterWeight + playerWeight;
        if (totalWeight > 0f)
        {
            mapCenterWeight /= totalWeight;
            playerWeight /= totalWeight;
        }
    }
    
    /// <summary>
    /// Manuel zoom ayarı
    /// </summary>
    public void SetZoomRange(float newMinSize, float newMaxSize)
    {
        minOrthographicSize = Mathf.Max(1f, newMinSize);
        maxOrthographicSize = Mathf.Max(minOrthographicSize, newMaxSize);
    }
    
    /// <summary>
    /// Debug bilgileri için public properties
    /// </summary>
    public Vector3 MapCenter => mapCenter;
    public Vector2 MapBounds => mapBounds;
    public Vector3 CurrentTargetPosition => targetPosition;
    public float CurrentTargetZoom => targetOrthographicSize;
    
    private void OnDrawGizmos()
    {
        if (!enableDebugGizmos || !cacheValid) return;
        
        // Draw map bounds
        Gizmos.color = Color.cyan;
        Vector3 boundsCenter = new Vector3(mapCenter.x, mapCenter.y, 0);
        Vector3 boundsSize = new Vector3(mapBounds.x, mapBounds.y, 0);
        Gizmos.DrawWireCube(boundsCenter, boundsSize);
        
        // Draw map center
        Gizmos.color = Color.blue;
        Gizmos.DrawSphere(mapCenter, 0.5f);
        
        // Draw player position if available
        if (playerTransform != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(playerTransform.position, 0.3f);
        }
        
        // Draw camera target position
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(targetPosition, 0.4f);
        
        // Draw line between map center and player
        if (playerTransform != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(mapCenter, playerTransform.position);
        }
    }
}