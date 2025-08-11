import hou
import math
from math import cos, sin, radians

node = hou.pwd()
geo = node.geometry()
geo.clear()

input_geo = node.inputs()[0].geometry()

font_node_path = "/obj/main/fonti"  # Font SOP node yolunu kendine göre ayarla
font_node = hou.node(font_node_path)
if not font_node:
    raise RuntimeError("Font SOP node bulunamadı: " + font_node_path)

font_size = 0.5
font_node.parm("fontsize").set(font_size)

angle = radians(-90)
c = cos(angle)
s = sin(angle)

def rotate_x(v):
    x, y, z = v
    new_y = y * c - z * s
    new_z = y * s + z * c
    return hou.Vector3(x, new_y, new_z)

for prim in input_geo.prims():
    # Primitive üzerindeki tile_char attribute'unu al
    tile_char = prim.stringAttribValue("tile_char")
    if not tile_char:
        continue  # Eğer yoksa atla

    # Primitive ortalama pozisyonunu hesapla
    points = prim.points()
    pos = hou.Vector3(0, 0, 0)
    for pt in points:
        pos += pt.position()
    pos /= len(points)

    # Font SOP text parametresini güncelle
    font_node.parm("text").set(tile_char)

    # Font geometriyi al, kopyala
    font_geo = font_node.geometry()
    copied_geo = hou.Geometry()
    copied_geo.copy(font_geo)

    # Döndür, pozisyona taşı ve biraz Z'de yukarı al
    for p in copied_geo.points():
        rotated_pos = rotate_x(p.position())
        final_pos = rotated_pos + pos
        final_pos[2] += 0.01
        p.setPosition(final_pos)

    # Ana geometriye ekle
    geo.merge(copied_geo)
