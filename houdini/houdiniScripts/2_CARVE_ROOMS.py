import hou
import random

# Mevcut Python SOP nod'unu al
node = hou.pwd()
geo = node.geometry()

# --- 1. KONTROL PANELİNİ BUL VE PARAMETRELERİ OKU ---
try:
    controller = hou.node("../CONTROLLER")  # Controller node yolunu kendi yapına göre ayarla
    seed = controller.parm("seed").evalAsInt()
    room_count = controller.parm("room_count").evalAsInt()
    random.seed(seed)
except AttributeError:
    print("UYARI: CONTROLLER nod'u veya parametreleri bulunamadı. Varsayılan değerler kullanılıyor.")
    room_count = 5
    seed = 12345
    random.seed(seed)

# --- 2. class Attribute var mı kontrol et, yoksa ekle ---
if not geo.findPointAttrib("class"):
    geo.addAttrib(hou.attribType.Point, "class", -1)

# --- 3. Harita sınırlarını al ---
bbox = geo.boundingBox()
min_x, max_x = int(bbox.minvec()[0]), int(bbox.maxvec()[0])
min_z, max_z = int(bbox.minvec()[2]), int(bbox.maxvec()[2])

# --- 4. Odaları carve et ve class id ata ---
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

# --- 5. (Opsiyonel) Loglama ---
print(f"{room_id} tane oda carve edildi ve class attribute atandı.")
print(f"Seed: {seed}, Room Count: {room_count}")
