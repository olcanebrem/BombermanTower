# 4_VISUALIZE_MAP.py (KeyError Düzeltmesi)
import hou

node = hou.pwd()
geo = node.geometry()

# --- Gerekli Attribute'ları PRIMITIVE seviyesinde oluştur ---
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
    "stairs":       {"color": (0.8, 0.5, 1.0), "char": "S"},
    "coin":         {"color": (1.0, 0.8, 0.0), "char": "C"},
    "health":       {"color": (0.0, 1.0, 0.0), "char": "H"},
    "breakable":    {"color": (0.5, 0.3, 0.1), "char": "B"},
    "1":            {"color": (0.4, 0.4, 0.0), "char": "1"},
    "empty":        {"color": (0.1, 0.1, 0.1), "char": "."}, 
}
default_tile = "empty"
path_highlight_color = (0.4, 0.4, 0.0)

# --- 'path' grubunu bir kez bulup değişkene ata ---
path_group = geo.findPointGroup("path")

# --- Primitive'lere Renk ve Karakter Ata ---
for prim in geo.prims():
    if prim.numVertices() > 0:
        owner_point = prim.points()[0]
        
        tile_type = owner_point.stringAttribValue("tile_type")
        properties = tile_properties.get(tile_type, tile_properties[default_tile])
        
        prim.setAttribValue("Cd", properties["color"])
        prim.setAttribValue("tile_char", properties["char"])

        is_on_path = False
        if path_group and path_group.contains(owner_point):
            is_on_path = True
        
        if is_on_path:
            prim.setAttribValue("Cd", path_highlight_color)
            prim.setAttribValue("tile_char", "1")

# (İsteğe bağlı) Temizlik
# ...