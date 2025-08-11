import hou

node = hou.pwd()
geo = node.geometry()

# --- 1. Gerekli Attribute'ları Doğru Seviyede Oluştur ---

if not geo.findPrimAttrib("Cd"):
    geo.addAttrib(hou.attribType.Prim, "Cd", (0.0, 0.0, 0.0))

if not geo.findPrimAttrib("tile_char"):
    geo.addAttrib(hou.attribType.Prim, "tile_char", "")

if not geo.findPrimAttrib("text"):
    geo.addAttrib(hou.attribType.Prim, "text", "")

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
}

default_tile = "empty"

# --- 2. Primitive renk ve tile_char/text attribute'larını ata ---

for prim in geo.prims():
    if prim.numVertices() > 0:
        # İlk vertex üzerinden point al
        point = prim.vertices()[0].point()

        tile_type = point.stringAttribValue("tile_type")
        properties = tile_properties.get(tile_type, tile_properties[default_tile])

        # Primitive renk
        prim.setAttribValue("Cd", properties["color"])

        # tile_char ve text attribute'larını primitive üzerine ata
        prim.setAttribValue("tile_char", properties["char"])
        prim.setAttribValue("text", properties["char"])
