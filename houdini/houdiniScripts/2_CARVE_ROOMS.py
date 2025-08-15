import hou
import random
import math

node = hou.pwd()
geo = node.geometry()

# Controller parametreleri
controller = hou.node("../CONTROLLER")
seed = controller.parm("seed").evalAsInt() if controller else 12345
room_count = controller.parm("room_count").evalAsInt() if controller else 5
min_room_size = controller.parm("min_room_size").evalAsInt() if controller else 3
max_room_size = controller.parm("max_room_size").evalAsInt() if controller else 7
noise_scale = controller.parm("noise_scale").eval() if controller else 1.0

random.seed(seed)

# Attribute kontrolü
if not geo.findPointAttrib("class"):
    geo.addAttrib(hou.attribType.Point, "class", -1)
if not geo.findPointAttrib("tile_type"):
    geo.addAttrib(hou.attribType.Point, "tile_type", "wall")

# Başlangıçta tüm noktaları wall yap
for pt in geo.points():
    pt.setAttribValue("class", 0)
    pt.setAttribValue("tile_type", "wall")

# Harita sınırları
bbox = geo.boundingBox()
min_x, max_x = int(bbox.minvec()[0]), int(bbox.maxvec()[0])
min_z, max_z = int(bbox.minvec()[2]), int(bbox.maxvec()[2])

# Odaları carve et
room_id = 0
for _ in range(room_count):
    room_w = random.randint(min_room_size, max_room_size)
    room_h = random.randint(min_room_size, max_room_size)
    room_x = random.randint(min_x, max_x - room_w)
    room_z = random.randint(min_z, max_z - room_h)
    
    room_id += 1
    for pt in geo.points():
        pos = pt.position()
        if (room_x <= pos[0] < room_x + room_w) and (room_z <= pos[2] < room_z + room_h):
            noise_val = math.sin((pos[0] + pos[2]) * noise_scale + random.random() * 2.0)
            if noise_val > 0:
                pt.setAttribValue("tile_type", "empty")
                pt.setAttribValue("class", room_id)

print(f"{room_id} oda carve edildi. Seed: {seed}")
