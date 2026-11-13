"""Tiles screenshots into contact sheets (run with Blender's Python: numpy + OpenImageIO).
   sheet.py <dir> <out_prefix> [cols=3] [cell_w=640] [per_sheet=9] [name filter]"""
import glob, os, sys
import numpy as np
import OpenImageIO as oiio

src, out = sys.argv[1], sys.argv[2]
cols = int(sys.argv[3]) if len(sys.argv) > 3 else 3
cw = int(sys.argv[4]) if len(sys.argv) > 4 else 640
per = int(sys.argv[5]) if len(sys.argv) > 5 else 9
flt = sys.argv[6] if len(sys.argv) > 6 else ""
files = sorted(f for f in glob.glob(os.path.join(src, "*.jpg")) + glob.glob(os.path.join(src, "*.png"))
               if flt in os.path.basename(f) and "sheet" not in os.path.basename(f))


def load(path, w):
    buf = oiio.ImageBuf(path)
    spec = buf.spec()
    h = int(round(w * spec.height / spec.width))
    small = oiio.ImageBufAlgo.resize(buf, roi=oiio.ROI(0, w, 0, h, 0, 1, 0, 3))
    return np.clip(small.get_pixels(oiio.FLOAT)[:, :, :3], 0, 1)


for s in range(0, len(files), per):
    chunk = files[s:s + per]
    ims = [load(f, cw) for f in chunk]
    ch = ims[0].shape[0]
    rows = (len(ims) + cols - 1) // cols
    sheet = np.full((rows * ch, cols * cw, 3), 0.08, np.float32)
    for i, im in enumerate(ims):
        x, y = (i % cols) * cw, (i // cols) * ch
        sheet[y:y + im.shape[0], x:x + cw] = im[:ch]
    p = "%s_%d.jpg" % (out, s // per + 1)
    o = oiio.ImageBuf(oiio.ImageSpec(sheet.shape[1], sheet.shape[0], 3, oiio.UINT8))
    o.set_pixels(oiio.ROI(0, sheet.shape[1], 0, sheet.shape[0]), sheet)
    o.specmod().attribute("CompressionQuality", 88)
    o.write(p)
    print(p, "<-", " ".join(os.path.basename(f)[:-4] for f in chunk))
