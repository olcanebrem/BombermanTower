# 2_6_CLEANUP.py (Sadece Temizlik Yapan Versiyon)
import hou

main_node = hou.pwd()

# --- 1. TEMİZLENECEK NOD'U BUL ---
# Bir önceki script'in yarattığı 'workspace' nod'unu ismiyle buluyoruz.
parent_network = main_node.parent()
workspace_to_delete = parent_network.node("temp_pathfinding_workspace")

# --- 2. GÜVENLİK KONTROLÜ VE SİLME ---
# Eğer nod bulunursa, onu yok et.



# --- 3. GEOMETRİYE DOKUNMA ---
# Bu script'in geometriyi değiştirmemesi önemli.
def delete_nodes_later():
    ws = hou.node("/obj/geo1/workspace_temp")
    if ws:
        ws.destroy()

hou.ui.addEventLoopCallback(delete_nodes_later)

# Sadece girişindeki geometriyi kopyalayıp çıkışına verir.
geo = main_node.geometry()
geo.clear()
geo.merge(main_node.inputs()[0].geometry())