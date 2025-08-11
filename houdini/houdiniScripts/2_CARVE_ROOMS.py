# 2_CARVE_ROOMS.py - CONTROLLER Entegreli Hali
import hou
import random

# Mevcut Python SOP nod'unu al
node = hou.pwd()
geo = node.geometry()

# --- 1. KONTROL PANELİNİ BUL VE PARAMETRELERİ OKU (YENİ EKLENEN KISIM) ---
try:
    # Ağaçtaki CONTROLLER nod'unu bul.
    # Eğer Python SOP'unuz bir Subnetwork içindeyse, yol "../CONTROLLER" olabilir.
    # Eğer aynı seviyedeyse, "CONTROLLER" olabilir. En sağlamı tam yoldur.
    controller = hou.node("../CONTROLLER") # Veya tam yolu: /obj/geo1/CONTROLLER
    
    # İhtiyacımız olan parametreleri oku
    seed = controller.parm("seed").evalAsInt()
    room_count = controller.parm("room_count").evalAsInt()
    
    # Rastgelelik için seed'i ayarla. Bu, aynı seed ile her zaman aynı haritayı üretir.
    random.seed(seed)

except AttributeError:
    # Eğer CONTROLLER bulunamazsa veya parametreler yoksa, hata vermemesi için
    # varsayılan değerler kullan. Bu, script'i daha sağlam yapar.
    print("UYARI: CONTROLLER nod'u veya parametreleri bulunamadı. Varsayılan değerler kullanılıyor.")
    room_count = 5 # Varsayılan oda sayısı

# --- 2. SİZİN ORİJİNAL VE ÇALIŞAN KODUNUZ (DEĞİŞTİRİLMEDİ) ---

# Harita sınırlarını al (bounding box)
bbox = geo.boundingBox()
min_x, max_x = int(bbox.minvec()[0]), int(bbox.maxvec()[0])
min_z, max_z = int(bbox.minvec()[2]), int(bbox.maxvec()[2]) # Houdini'de Y yukarı, Z derinliktir

# Birkaç tane rastgele oda oy
# ESKİ: num_rooms = 5
# YENİ: num_rooms değişkenini, CONTROLLER'dan okuduğumuz room_count ile değiştiriyoruz.
num_rooms = room_count 
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