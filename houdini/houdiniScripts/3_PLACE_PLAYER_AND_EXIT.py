import hou
import random

node = hou.pwd()
geo = node.geometry()

# Bütün 'empty' tile'ları bul
empty_points = [pt for pt in geo.points() if pt.stringAttribValue("tile_type") == "empty"]

if len(empty_points) > 2:
    # Rastgele iki farklı boş nokta seç
    spawn_points = random.sample(empty_points, 2)

    # Oyuncu ve Merdiveni yerleştir
    spawn_points[0].setAttribValue("tile_type", "player")
    spawn_points[1].setAttribValue("tile_type", "stairs")

# Buraya Düşman, Coin, Health yerleştirme mantığı eklenebilir
# Örnek:
empty_points = [pt for pt in geo.points() if pt.stringAttribValue("tile_type") == "empty"]
num_coins = 10
if len(empty_points) > num_coins:
    coin_points = random.sample(empty_points, num_coins)
    for pt in coin_points:
        pt.setAttribValue("tile_type", "coin")