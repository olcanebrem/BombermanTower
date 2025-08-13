import hou

node = hou.pwd()
geo = node.geometry()
input_geo = node.inputs()[0].geometry()

# Temizle
geo.clear()

# Instance attrib ekle
if not geo.findPointAttrib("instance"):
    geo.addAttrib(hou.attribType.Point, "instance", "")

for prim in input_geo.prims():
    tile_type = prim.stringAttribValue("tile_type")
    if not tile_type:
        continue
    
    # Primitive merkez pozisyonu
    pos = hou.Vector3(0, 0, 0)
    for pt in prim.points():
        pos += pt.position()
    pos /= len(prim.points())
    
    # Yeni point oluştur
    pt = geo.createPoint()
    pt.setPosition(pos)
    
    # instance path → font objelerinin tam yolu
    font_obj_path = "/obj/font_" + tile_type
    pt.setAttribValue("instance", font_obj_path)
