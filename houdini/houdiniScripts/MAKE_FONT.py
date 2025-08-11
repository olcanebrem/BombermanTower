import hou
import math
from math import cos, sin, radians

node = hou.pwd()
geo = node.geometry()

# Giriş geometrisini al (üzerinde tile_char olan harita)
input_geo = node.inputs()[0].geometry()

# Font SOP'unun yolunu belirt. Bu yolun doğru olduğundan emin ol.
font_node_path = "/obj/main/fonti" 
font_node = hou.node(font_node_path)
if not font_node:
    raise RuntimeError("Font SOP node bulunamadı: " + font_node_path)

# Font ayarlarını yap
font_size = 0.6
font_node.parm("fontsize").set(font_size)

# Rotasyon için matematiksel hesaplamalar
angle = radians(-90)
c = cos(angle)
s = sin(angle)

# X ekseninde döndürme fonksiyonu
def rotate_x(v):
    x, y, z = v
    new_y = y * c - z * s
    new_z = y * s + z * c
    return hou.Vector3(x, new_y, new_z)

# Ana döngü: Girişteki her bir kare (primitive) için çalışır
for prim in input_geo.prims():
    # Primitive üzerindeki tile_char attribute'unu al
    tile_char = prim.stringAttribValue("tile_char")
    if not tile_char:
        continue

    # Karenin merkez pozisyonunu hesapla
    points = prim.points()
    pos = hou.Vector3(0, 0, 0)
    for pt in points:
        pos += pt.position()
    pos /= len(points)

    # Font SOP'una hangi harfi üreteceğini söyle
    font_node.parm("text").set(tile_char)

    # Font geometriyi al ve güvenli bir kopyasını oluştur
    font_geo = font_node.geometry()
    copied_geo = hou.Geometry()
    copied_geo.copy(font_geo)

    # Oluşturulan harfin her bir noktasını gez ve pozisyonunu ayarla
    for p in copied_geo.points():
        final_pos = p.position() + pos
        
        # --- YENİ EKLENEN SATIR ---
        # Hesaplanan son pozisyonun X değerine 0.1 ekle.
        final_pos[1] += 0.01
        
        # Z'de biraz yukarı al (gölge çakışmasını önlemek için)
        final_pos[2] += 0.01
        
        # Noktanın son pozisyonunu ayarla
        p.setPosition(final_pos)

    # Tamamen dönüştürülmüş harfi ana geometriye ekle
    geo.merge(copied_geo)