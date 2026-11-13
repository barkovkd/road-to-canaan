"""Shows only the named objects (ARGS show=a,b,c; empty = everything) and optionally dresses the head with a hat."""
import bpy
names = set(n for n in ARGS.get("show", "").split(",") if n)
for ob in bpy.data.objects:
    if ob.type in ("MESH",):
        hide = bool(names) and ob.name not in names
        ob.hide_set(hide)
        ob.hide_render = hide
    elif ob.type in ("ARMATURE", "EMPTY"):
        ob.hide_set(bool(names))
print("visible:", sorted(o.name for o in bpy.data.objects if o.type == "MESH" and not o.hide_get()))
