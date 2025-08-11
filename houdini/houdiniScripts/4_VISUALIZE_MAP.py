# 4_VISUALIZE_MAP.py - TEMİZLENMİŞ Hali
import hou

node = hou.pwd()
geo = node.geometry()

# --- 1. Gerekli Attribute'ları PRIMITIVE seviyesinde oluştur ---
if not geo.findPrimAttrib("Cd"):
    geo.addAttrib(hou.attribType.Prim, "Cd", (0.0, 0.0, 0.0))
if not geo.findPrimAttrib("tile_char"):
    geo.addAttrib(hou.attribType.Prim, "tile_char", "")

# --- TILE SÖZLÜĞÜ ---
tile_properties = {
    "wall":         {"color": (0.2, 0.2, 0.2), "char": "#"},
    "player":       {"color": (0.0, 0.8, 1.0), "char": "P"},
    "enemy":        {"color": (1.0, 0.2, 0.2), "char": "E"},
    "enemy_shooter":{"color": (1.0, 0.5, 0.0), "char": "F"},
    "stairs":       {"color": (0.8, 0.8, 0.8), "char": "S"},
    "empty":        {"color": (0.1, 0.1, 0.1), "char": "."},
    "coin":         {"color": (1.0, 0.8, 0.0), "char": "C"},
    "health":       {"color": (0.0, 1.0, 0.0), "char": "H"},
    "breakable":    {"color": (0.5, 0.3, 0.1), "char": "B"},
}
default_tile = "empty"

# --- 2. Primitive'lere Hem Renk Hem Karakter Ata ---
for prim in geo.prims():
    if prim.numVertices() > 0:
        point = prim.vertices()[0].point()
        tile_type = point.stringAttribValue("tile_type")
        
        properties = tile_properties.get(tile_type, tile_properties[default_tile])
        
        prim.setAttribValue("Cd", properties["color"])
        prim.setAttribValue("tile_char", properties["char"])

# --- SİLME KISMI TAMAMEN KALDIRILDI ---
# Artık burada attribute silmeye çalışan bir kod yok.