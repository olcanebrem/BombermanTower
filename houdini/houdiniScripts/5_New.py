import hou

node = hou.pwd()
geo = node.geometry()
geo.clear()

input_geo = node.inputs()[0].geometry()

font_node_path = "/obj/main/fonti"
font_node = hou.node(font_node_path)
if not font_node:
    raise RuntimeError("Font SOP node bulunamadı: " + font_node_path)

font_size = 0.5
font_node.parm("fontsize").set(font_size)

for pt in input_geo.points():
    char = pt.stringAttribValue("tile_char")
    pos = pt.position()

    font_node.parm("text").set(char)

    font_geo = font_node.geometry()
    copied_geo = hou.Geometry()
    copied_geo.copy(font_geo)

    # -90 derece X ekseni etrafında döndürme matrisi oluştur (radyan cinsinden)
    import math
    from math import cos, sin, radians
    
    # -90 derece X ekseni etrafında döndürme matrisi
    angle = radians(-90)
    c = cos(angle)
    s = sin(angle)
    
    # Döndürme matrisi (X ekseni etrafında)
    def rotate_x(v):
        x, y, z = v
        new_y = y * c - z * s
        new_z = y * s + z * c
        return hou.Vector3(x, new_y, new_z)
    
    # Her noktayı döndür ve ilgili pozisyona taşı
    for p in copied_geo.points():
        # Noktanın orijinal pozisyonunu al ve döndür
        rotated_pos = rotate_x(p.position())
        
        # Döndürülmüş pozisyona hedef pozisyonu ekle
        final_pos = rotated_pos + pos
        final_pos[2] += 0.01  # Z'de biraz yukarı kaldır
        
        # Yeni pozisyonu ata
        p.setPosition(final_pos)

    geo.merge(copied_geo)
