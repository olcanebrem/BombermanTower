import hou

node = hou.pwd()
geo = node.geometry()

# Temel attribute'ları ekle (varsa hata vermez)
geo.addAttrib(hou.attribType.Point, "tile_type", "wall")
geo.addAttrib(hou.attribType.Point, "Cd", (0.0, 0.0, 0.0))
geo.addAttrib(hou.attribType.Point, "tile_char", "") # Bunu sonraki nod'lar dolduracak

# Bütün noktaları 'wall' olarak ayarla
# Not: addAttrib zaten varsayılan değer atadığı için bu döngüye gerek kalmayabilir.
# Ama açıkça göstermek için burada bırakıyorum.
for point in geo.points():
    point.setAttribValue("tile_type", "wall")