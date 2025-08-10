node = hou.pwd()
geo = node.geometry()

if not geo.findPointAttrib("Cd"):
    geo.addAttrib(hou.attribType.Point, "Cd", (1.0, 1.0, 1.0))

for point in geo.points():
    point.setAttribValue("Cd", (1.0, 0.0, 0.0))