using UnityEngine;
using UnityEngine.UI;

public class LevelLoader : MonoBehaviour
{
    public static LevelLoader instance;

    [SerializeField] public float tileSize = 30f;
    public Font asciiFont;
    public GameObject playerPrefab;

    public int width = 10;
    public int height = 30;

    public GameObject playerObject;
    public GameObject[,] tileObjects;

    public char[,] levelMap;

    void Awake()
    {
        if (instance != null && instance != this)
            Destroy(gameObject);
        else
            instance = this;
    }

    void Start()
    {
        GenerateRandomLevel();
        CreateMapVisual();
    }

    void GenerateRandomLevel()
    {
        levelMap = new char[width, height];

        // Hepsini boş yap
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                levelMap[x, y] = TileSymbols.TypeToSymbol(TileType.Empty);

        // Çevre duvarları koy
        for (int x = 0; x < width; x++)
        {
            levelMap[x, 0] = TileSymbols.TypeToSymbol(TileType.Wall);
            levelMap[x, height - 1] = TileSymbols.TypeToSymbol(TileType.Wall);
        }
        for (int y = 0; y < height; y++)
        {
            levelMap[0, y] = TileSymbols.TypeToSymbol(TileType.Wall);
            levelMap[width - 1, y] = TileSymbols.TypeToSymbol(TileType.Wall);
        }

        // Player spawn'u rastgele yerleştir
        int px = Random.Range(1, width - 1);
        int py = Random.Range(1, height - 1);
        levelMap[px, py] = TileSymbols.TypeToSymbol(TileType.PlayerSpawn);

        // Örnek olarak diğer bazı nesneleri rastgele yerleştir
        PlaceRandom(TileType.Coin, 10);
        PlaceRandom(TileType.Breakable, 5);
        PlaceRandom(TileType.Enemy, 4);
        PlaceRandom(TileType.EnemyShooter, 2);
        PlaceRandom(TileType.Bomb, 3);
        PlaceRandom(TileType.Health, 3);
        PlaceRandom(TileType.Gate, 1);
        PlaceRandom(TileType.Stairs, 1);
    }

    void PlaceRandom(TileType type, int count)
    {
        int placed = 0;
        while (placed < count)
        {
            int x = Random.Range(1, width - 1);
            int y = Random.Range(1, height - 1);
            if (levelMap[x, y] == TileSymbols.TypeToSymbol(TileType.Empty))
            {
                levelMap[x, y] = TileSymbols.TypeToSymbol(type);
                placed++;
            }
        }
    }

    void CreateMapVisual()
{
    // Önce eski objeleri yok et
    if (tileObjects != null)
    {
        for (int y = 0; y < tileObjects.GetLength(1); y++)
            for (int x = 0; x < tileObjects.GetLength(0); x++)
                if (tileObjects[x, y] != null)
                    Destroy(tileObjects[x, y]);
    }

    tileObjects = new GameObject[width, height];

    // Player varsa yok et
    if (playerObject != null)
    {
        Destroy(playerObject);
        playerObject = null;
    }

    for (int y = 0; y < height; y++)
    {
        for (int x = 0; x < width; x++)
        {
            char c = levelMap[x, y];
            Vector3 pos = new Vector3(x * tileSize, (height - y - 1) * tileSize, 0);
            TileType type = TileSymbols.SymbolToType(c);

            GameObject tileGO;

            // Player mı?
            if (type == TileType.PlayerSpawn)
            {
                playerObject = Instantiate(playerPrefab, pos, Quaternion.identity, transform);
                tileObjects[x, y] = playerObject;
                continue; // Tile davranışı yok, sadece player
            }
            else
            {
                tileGO = CreateAsciiTile(c, pos);
                tileObjects[x, y] = tileGO;
            }

            // Tile tipine göre davranış scripti ekle
            switch (type)
            {
                case TileType.Gate: tileGO.AddComponent<GateTile>(); break;
                case TileType.Bomb: tileGO.AddComponent<BombTile>(); break;
                case TileType.Health: tileGO.AddComponent<HealthTile>(); break;
                case TileType.Enemy: tileGO.AddComponent<EnemyTile>(); break;
                case TileType.EnemyShooter: tileGO.AddComponent<EnemyShooterTile>(); break;
                // Gerekirse diğerleri...
            }
        }
    }
}


    GameObject CreateAsciiTile(char symbol, Vector3 position)
    {
        GameObject tileGO = new GameObject($"Tile_{position.x}_{position.y}");
        tileGO.transform.SetParent(this.transform);
        tileGO.transform.position = position;

        RectTransform rt = tileGO.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(tileSize, tileSize);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.zero;
        rt.pivot = Vector2.zero;

        Text text = tileGO.AddComponent<Text>();
        text.text = symbol.ToString();
        text.fontSize = 30;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.font = asciiFont;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        return tileGO;
    }
}
