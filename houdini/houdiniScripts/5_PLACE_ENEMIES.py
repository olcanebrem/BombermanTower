# 5_PLACE_ENEMIES.py
import hou
import random

node = hou.pwd()
geo = node.geometry()

# --- 1. KONTROL PANELİNİ BUL VE PARAMETRELERİ OKU ---
try:
    controller = hou.node("../CONTROLLER")
    seed = controller.parm("seed").evalAsInt()
    enemy_density = controller.parm("enemy_density").evalAsFloat()
    
    # Farklı bir rastgelelik için seed'i yine biraz değiştir.
    random.seed(seed + 2) 

except AttributeError:
    print("UYARI: CONTROLLER veya gerekli parametreler bulunamadı. Düşman yerleştirilmeyecek.")
    enemy_density = 0.0 # Varsayılan olarak hiç düşman koyma

# --- 2. DÜŞMAN YERLEŞTİRME MANTIĞI ---

# Önce, düşman yerleştirmek için uygun boş noktaları bul.
# Oyuncunun hemen dibinde başlamamaları için, 'player' ve 'gate' olmayanları alalım.
suitable_empty_points = []
for pt in geo.points():
    if pt.stringAttribValue("tile_type") == "empty":
        suitable_empty_points.append(pt)

# Eğer yerleştirilecek uygun yer varsa ve yoğunluk sıfırdan büyükse devam et.
if suitable_empty_points and enemy_density > 0:
    
    # Yoğunluğa göre yerleştirilecek düşman SAYISINI hesapla.
    # Örneğin, tüm boş alanların en fazla %15'i düşmanla dolsun.
    max_possible_enemies = int(len(suitable_empty_points) * 0.15) 
    num_to_place = int(max_possible_enemies * enemy_density)

    # Hesaplanan sayıda düşmanı rastgele seç ve yerleştir.
    if num_to_place > 0 and len(suitable_empty_points) >= num_to_place:
        points_to_populate = random.sample(suitable_empty_points, num_to_place)
        
        for pt in points_to_populate:
            # Şimdilik basit bir mantıkla, %20 ihtimalle atıcı, %80 ihtimalle normal düşman koyalım.
            # Bu mantığı daha sonra "uzun koridor bulma" gibi daha akıllı bir hale getirebiliriz.
            if random.random() < 0.2:
                pt.setAttribValue("tile_type", "enemy_shooter") # S
            else:
                pt.setAttribValue("tile_type", "enemy") # E