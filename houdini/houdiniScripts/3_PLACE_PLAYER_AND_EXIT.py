# 3_PLACE_PLAYER_AND_EXIT.py
import hou
import random

# Mevcut Python SOP nod'unu al
node = hou.pwd()
geo = node.geometry()

# --- 1. KONTROL PANELİNİ BUL VE PARAMETRELERİ OKU ---
try:
    controller = hou.node("../CONTROLLER")
    seed = controller.parm("seed").evalAsInt()
    
    # Seed'i ayarla. Bu adımın rastgeleliği, bir önceki adımdan
    # farklı ama yine de tekrarlanabilir olmalı.
    # Genellikle seed'e küçük bir sayı eklemek iyi bir pratiktir.
    random.seed(seed + 1) 

except AttributeError:
    print("UYARI: CONTROLLER nod'u veya 'seed' parametresi bulunamadı.")
    # Seed olmadan devam et

# --- 2. OYUNCU VE ÇIKIŞI YERLEŞTİRME MANTIĞI ---

# Önce, haritadaki TÜM 'empty' noktaları bul ve bir listeye ekle.
empty_points = []
for pt in geo.points():
    if pt.stringAttribValue("tile_type") == "empty":
        empty_points.append(pt)

# Eğer yeterli boş nokta varsa (en az 2 tane), devam et.
if len(empty_points) >= 2:
    # Bu boş noktalar listesinden, birbirine benzemeyen (farklı) 2 tane rastgele nokta seç.
    # random.sample() bu iş için mükemmeldir.
    chosen_points = random.sample(empty_points, 2)
    
    # Seçilen ilk noktanın tile_type'ını 'player' yap.
    player_spawn_point = chosen_points[0]
    player_spawn_point.setAttribValue("tile_type", "player")
    
    # Seçilen ikinci noktanın tile_type'ını 'stairs' yap.
    exit_point = chosen_points[1]
    exit_point.setAttribValue("tile_type", "stairs")
    
else:
    # Eğer haritada yeterli boş alan yoksa, bir uyarı ver.
    # Bu, 2_CARVE_ROOMS'da bir sorun olduğunu gösterir.
    print("UYARI: Oyuncu ve çıkış yerleştirmek için yeterli 'empty' alan bulunamadı!")