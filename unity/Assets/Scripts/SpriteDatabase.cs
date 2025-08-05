using UnityEngine;
using System.Collections.Generic;

// Bu, Unity'nin "Create" menüsüne yeni bir seçenek ekler.
[CreateAssetMenu(fileName = "SpriteDatabase", menuName = "MyGame/Sprite Database")]
public class SpriteDatabase : ScriptableObject
{
    // Inspector'da görünecek olan liste için bir yapı.
    [System.Serializable]
    public class SpriteMapping
    {
        public TileType type;
        public Sprite sprite;
    }

    // Inspector'dan dolduracağımız asıl liste.
    public List<SpriteMapping> spriteMappings;

    // Hızlı erişim için kullanılacak olan dahili sözlük.
    private Dictionary<TileType, Sprite> spriteDict;

    /// <summary>
    /// Oyun başladığında LevelLoader tarafından çağrılır.
    /// Listeyi, hızlı bir sözlüğe çevirir.
    /// </summary>
    public void Initialize()
    {
        spriteDict = new Dictionary<TileType, Sprite>();
        foreach (var mapping in spriteMappings)
        {
            if (mapping.sprite != null && !spriteDict.ContainsKey(mapping.type))
            {
                spriteDict.Add(mapping.type, mapping.sprite);
            }
        }
    }

    /// <summary>
    /// Verilen bir TileType'a karşılık gelen Sprite'ı döndürür.
    /// </summary>
    public Sprite GetSprite(TileType type)
    {
        // Sözlüğün başlatıldığından ve anahtarı içerdiğinden emin ol.
        if (spriteDict != null && spriteDict.TryGetValue(type, out Sprite sprite))
        {
            return sprite;
        }
        
        // Eğer sprite bulunamazsa, bir uyarı ver ve null döndür.
        Debug.LogWarning($"SpriteDatabase'de '{type}' için bir sprite bulunamadı!");
        return null;
    }
}