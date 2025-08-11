# 3_5_GUARANTEE_PATH.py (Nihai "Object Merge ile İzolasyon" Yöntemi)
import hou

main_node = hou.pwd()
# Giriş nod'umuzu alıyoruz, geometrisini değil.
input_node_ref = main_node.inputs()[0]

# --- 1. GEÇİCİ ATÖLYE (SUBNETWORK) OLUŞTUR ---
parent_network = main_node.parent()
workspace = parent_network.createNode("subnet", "temp_pathfinding_workspace")

# --- 2. ATÖLYENİN İÇİNİ İNŞA ET ---
# a. Veri Çekici (Object Merge)
importer = workspace.createNode("object_merge", "INPUT_GEOMETRY")
# b. Maliyet Ekleyici (Attribute Wrangle)
cost_adder = workspace.createNode("attribwrangle", "COST_ADDER")
# c. Asıl İşçi (Find Shortest Path)
path_finder = workspace.createNode("findshortestpath", "PATHFINDER")
# d. Sonuç Çıkış Noktası
output_node = workspace.createNode("output", "FINAL_RESULT")

# Atölye içindeki nod'ları birbirine bağla
cost_adder.setInput(0, importer)
path_finder.setInput(0, cost_adder)
output_node.setInput(0, path_finder)

# --- 3. ATÖLYEYİ AYARLA ---
# a. Object Merge'e, ana girişimizin yolunu vererek veriyi "çekmesini" söyle.
# Bu, kısır döngüyü kıran en önemli adımdır.
importer.parm("objpath1").set(input_node_ref.path())
importer.parm("xformtype").set(1) # "Into This Object"

# b. Maliyet Ekleyici'nin VEX kodunu ayarla.
cost_adder.parm("class").set("point")
cost_adder.parm("snippet").set(
    """f@path_cost = 1.0;
if (@tile_type == "wall") {
    f@path_cost = 1000.0;
}"""
)

# c. Path Finder'ı ayarla (veriyi giriş geometrisinden okuyarak)
input_geo = input_node_ref.geometry()
start_pt_num = -1
end_pt_num = -1
for pt in input_geo.points():
    tile_type = pt.stringAttribValue("tile_type")
    if tile_type == "player":
        start_pt_num = pt.number()
    elif tile_type == "stairs":
        end_pt_num = pt.number()

if start_pt_num != -1 and end_pt_num != -1:
    path_finder.parm("startpts").set(str(start_pt_num))
    path_finder.parm("endpts").set(str(end_pt_num))
    path_finder.parm("enablecost").set(1)
    path_finder.parm("cost").set("path_cost")
    path_finder.parm("enablepathsgroup").set(1)
    path_finder.parm("pathsgroup").set("path")

    # --- 4. SONUCU ATÖLYEDEN AL ---
    # Bu hesaplama, Python SOP'unu hiç tetiklemez.
    result_geo = output_node.geometry()

    # --- 5. SONUCU ANA HARİTAYA UYGULA ---
    output_geo = main_node.geometry()
    output_geo.clear()
    output_geo.merge(input_geo)

    if result_geo:
        path_group = result_geo.findPointGroup("path")
        if path_group:
            path_point_numbers = {pt.number() for pt in path_group.points()}
            
            for pt_num in path_point_numbers:
                pt = output_geo.point(pt_num)
                if pt:
                    pt.setAttribValue("tile_type", "empty")
            
            main_node.geometry().clear()
            main_node.geometry().merge(output_geo)

# --- 6. ATÖLYEYİ VE İÇİNDEKİ HER ŞEYİ YOK ET ---
# Artık hiçbir "cooking" bağı kalmadığı için bu komut güvenle çalışır.
workspace.destroy()