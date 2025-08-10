import hou
import random

node = hou.pwd()
geo = node.geometry()

# Harita sınırlarını al (bounding box)
bbox = geo.boundingBox()
min_x, max_x = int(bbox.minvec()[0]), int(bbox.maxvec()[0])
min_z, max_z = int(bbox.minvec()[2]), int(bbox.maxvec()[2]) # Houdini'de Y yukarı, Z derinliktir

# Birkaç tane rastgele oda oy
num_rooms = 5
for _ in range(num_rooms):
    # Oda boyutlarını ve pozisyonunu rastgele belirle
    room_w = random.randint(3, 7)
    room_h = random.randint(3, 7)
    room_x = random.randint(min_x, max_x - room_w)
    room_z = random.randint(min_z, max_z - room_h)

    # Oda içindeki noktaların tile_type'ını 'empty' yap
    for pt in geo.points():
        pos = pt.position()
        if (room_x <= pos[0] < room_x + room_w) and \
           (room_z <= pos[2] < room_z + room_h):
            pt.setAttribValue("tile_type", "empty")