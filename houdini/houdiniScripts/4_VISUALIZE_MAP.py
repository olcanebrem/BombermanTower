# 4_VISUALIZE_MAP - Python SOP Kodu (Primitive Renkleri)
import hou

node = hou.pwd()
geo = node.geometry()

# --- 1. Gerekli Attribute'ları Doğru Seviyede Oluştur ---

if not geo.findPrimAttrib("Cd"):
    geo.addAttrib(hou.attribType.Prim, "Cd", (0.0, 0.0, 0.0))

if not geo.findPointAttrib("tile_char"):
    geo.addAttrib(hou.attribType.Point, "tile_char", "")

# Merkezi Tile Tanımlama Sözlüğü
tile_properties = {
    "wall":         {"color": (0.2, 0.2, 0.2), "char": "#"},
    "player":       {"color": (0.0, 0.8, 1.0), "char": "P"},
    "enemy":        {"color": (1.0, 0.2, 0.2), "char": "E"},
    "enemy_shooter":{"color": (1.0, 0.5, 0.0), "char": "F"},
    "coin":         {"color": (1.0, 0.8, 0.0), "char": "$"},
    "health":       {"color": (0.0, 1.0, 0.0), "char": "H"},
    "stairs":       {"color": (0.8, 0.8, 0.8), "char": "S"},
    "empty":        {"color": (0.1, 0.1, 0.1), "char": "."},
    "breakable":    {"color": (0.5, 0.3, 0.1), "char": "B"},
    # Diğer tipler...
}
default_tile = "empty"

# --- 2. GÖREV: Primitive Renklerini Ayarla ---

# Her bir primitive (kare) üzerinden döngüye gir.
# HATA BURADAYDI: primitives() -> prims() OLARAK DÜZELTİLDİ
for prim in geo.prims():
    # Bir primitive'i oluşturan noktalardan herhangi birini alabiliriz.
    if prim.numVertices() > 0:
        point = prim.vertices()[0].point()
        
        # Point'ten tile_type'ı oku.
        tile_type = point.stringAttribValue("tile_type")
        
        # Sözlükten ilgili özellikleri al.
        properties = tile_properties.get(tile_type, tile_properties[default_tile])
        
        # Rengi doğrudan PRIMITIVE'in kendisine ata.
        prim.setAttribValue("Cd", properties["color"])

# --- 3. GÖREV: Point Karakterlerini Ayarla (Export için) ---

# Bu döngü değişmeden kalır, çünkü tile_char hâlâ noktalarda yaşıyor.
for point in geo.points():
    tile_type = point.stringAttribValue("tile_type")
    properties = tile_properties.get(tile_type, tile_properties[default_tile])
    
    # Karakteri POINT'e ata.
    point.setAttribValue("tile_char", properties["char"])

# (İsteğe bağlı) Temizlik: Artık kullanılmayan eski point Cd attribute'unu silebiliriz.
if geo.findPointAttrib("Cd"):
    geo.destroyPointAttrib("Cd")

geo.addAttrib(hou.attribType.Point, "text", "")
# sonra tile_char değerini text attribute'una da kopyala
point.setAttribValue("text", properties["char"])
