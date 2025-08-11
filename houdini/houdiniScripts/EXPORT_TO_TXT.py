# 5_EXPORT_TO_TXT - Python SOP Kodu
import hou

node = hou.pwd()
geo = node.geometry()
geo.addAttrib(hou.attribType.Point, "tile_char", "")

# ANSI renk kodları sadece konsol çıktısı için burada
ANSI_RESET = "\033[0m"
# ...diğer ANSI kodları...

# Renkleri `tile_char`'a bağlayan basit bir sözlük
# Not: Bu, önceki nod'daki büyük sözlüğün bir kopyası/özeti olabilir.
# Veya `tile_type`ı tekrar okuyarak da yapılabilir. Sadelik için `tile_char` kullanalım.
char_to_ansi = {
    "#": "\033[90m", # Gri
    "P": "\033[94m", # Mavi
    "E": "\033[91m", # Kırmızı
    "F": "\033[95m", # Macenta
    "$": "\033[93m", # Sarı
    "H": "\033[92m", # Yeşil
    "S": "\033[97m", # Beyaz
    "B": "\033[33m", # Kahverengi
    ".": ANSI_RESET
}
default_ansi = ANSI_RESET

# --- Haritayı grid'e çevirme kısmı aynı kalıyor ---
bbox = geo.boundingBox()
if not bbox.isValid():
    print("Geometri bulunamadı.")
else:
    width = int(bbox.sizevec()[0]) + 1
    height = int(bbox.sizevec()[2]) + 1
    min_pos = bbox.minvec()
    
    char_grid = [[' ' for _ in range(width)] for _ in range(height)]
    
    for pt in geo.points():
        pos = pt.position()
        x = int(round(pos[0] - min_pos[0]))
        y = int(round(pos[2] - min_pos[2]))
        y = (height - 1) - y

        if 0 <= y < height and 0 <= x < width:
            # SADECE tile_char okunuyor!
            char_grid[y][x] = pt.stringAttribValue("tile_char")

    # --- 1. ÇIKTI: Unity için Temiz Metin Dosyası ---
    clean_output_string = ""
    for row in char_grid:
        clean_output_string += "".join(row) + "\n"
    
    output_path = "E:/UNITY/BombermanTower/Unity/Assets/Levels/level_01.txt" # Kendi yolunuzu yazın
    try:
        with open(output_path, "w") as f:
            f.write(clean_output_string)
        print(f"Temiz harita şuraya yazıldı: {output_path}")
    except Exception as e:
        hou.ui.displayMessage(f"Dosya yazma hatası: {e}", severity=hou.severityType.Error)

    # --- 2. ÇIKTI: Houdini Konsolu için Renkli Önizleme ---
    print("\n--- Renkli Harita Önizlemesi ---")
    colored_output_string = ""
    for row in char_grid:
        for char in row:
            color_code = char_to_ansi.get(char, default_ansi)
            colored_output_string += f"{color_code}{char}{ANSI_RESET}"
        colored_output_string += "\n"
        
    print(colored_output_string)
    print("--- Önizleme Sonu ---")