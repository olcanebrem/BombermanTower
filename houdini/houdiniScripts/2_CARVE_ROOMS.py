import hou
import random

node = hou.pwd()
geo = node.geometry()

# --- CONTROLLER parametrelerini oku ---
try:
    controller = hou.node("../CONTROLLER")
    seed = controller.parm("seed").evalAsInt()
    room_count = controller.parm("room_count").evalAsInt()
    random.seed(seed)
except AttributeError:
    print("UYARI: CONTROLLER bulunamadı, varsayılan değerler kullanılıyor.")
    room_count = 5
    seed = 12345
    random.seed(seed)

# --- class ve tile_type attribute yoksa ekle ---
if not geo.findPointAttrib("class"):
    geo.addAttrib(hou.attribType.Point, "class", -1)
if not geo.findPointAttrib("tile_type"):
    geo.addAttrib(hou.attribType.Point, "tile_type", "wall")

# --- Harita sınırları ---
bbox = geo.boundingBox()
min_x, max_x = int(bbox.minvec()[0]), int(bbox.maxvec()[0])
min_z, max_z = int(bbox.minvec()[2]), int(bbox.maxvec()[2])

# --- Odaları carve et ---
room_id = 0
for _ in range(room_count):
    room_w = random.randint(3, 7)
    room_h = random.randint(3, 7)
    room_x = random.randint(min_x, max_x - room_w)
    room_z = random.randint(min_z, max_z - room_h)
    
    room_id += 1
    for pt in geo.points():
        pos = pt.position()
        if (room_x <= pos[0] < room_x + room_w) and (room_z <= pos[2] < room_z + room_h):
            pt.setAttribValue("tile_type", "empty")
            pt.setAttribValue("class", room_id)

print(f"{room_id} oda carve edildi. Seed: {seed}")
