import sys
sys.stdout.reconfigure(encoding='utf-8')
from PIL import Image
import numpy as np
import os

INPUT = r'D:\AIRearch\TD\Unity\AetheraSurvivors\Assets\Art_Source\compressed_sprite_1774525619966_4f7b9c6e-8b37-4d52-9430-3f56595d3303.png'
OUTPUT = r'D:\AIRearch\TD\Unity\AetheraSurvivors\Assets\Resources\Sprites\Enemies\enemy_assassin_walk.png'

img = Image.open(INPUT).convert('RGBA')
arr = np.array(img)
w, h = img.size
print(f'Original: {w}x{h}')

COLS = 4
ROWS = 2
FW = w // COLS
FH = h // ROWS
BG = np.array([147.0, 146.0, 149.0])
TARGET = 128  # 提升到128x128，解决清晰度问题
PAD = 8

def remove_bg(fa, threshold=18, edge_range=12):
    a = fa.copy()
    dist = np.sqrt(np.sum((a[:,:,:3].astype(float) - BG.reshape(1,1,3))**2, axis=2))
    a[dist < threshold, 3] = 0
    edge = (dist >= threshold) & (dist < threshold + edge_range)
    a[edge, 3] = ((dist[edge] - threshold) / edge_range * 255).clip(0, 255).astype(np.uint8)
    return a

# 分析所有帧
all_bboxes = []
all_info = []
for r in range(ROWS):
    for c in range(COLS):
        x0, y0 = c * FW, r * FH
        frame = arr[y0:y0+FH, x0:x0+FW].copy()
        cleaned = remove_bg(frame)
        alpha = cleaned[:,:,3]
        ys, xs = np.where(alpha > 50)
        if len(xs) > 100:
            bx1, by1, bx2, by2 = xs.min(), ys.min(), xs.max()+1, ys.max()+1
        else:
            bx1, by1, bx2, by2 = 0, 0, FW, FH
        all_bboxes.append((bx1, by1, bx2, by2))
        idx = r * COLS + c
        all_info.append({'r': r, 'c': c, 'idx': idx})
        bw, bh = bx2-bx1, by2-by1
        print(f'  [{r},{c}] bbox=({bx1},{by1})->({bx2},{by2}) {bw}x{bh}')

# 统一包围盒
ux1 = max(0, min(b[0] for b in all_bboxes) - PAD)
uy1 = max(0, min(b[1] for b in all_bboxes) - PAD)
ux2 = min(FW, max(b[2] for b in all_bboxes) + PAD)
uy2 = min(FH, max(b[3] for b in all_bboxes) + PAD)
ucw, uch = ux2 - ux1, uy2 - uy1
print(f'Unified: ({ux1},{uy1})->({ux2},{uy2}), {ucw}x{uch}')

# 处理每帧
final = []
for info in all_info:
    r2, c2 = info['r'], info['c']
    x0, y0 = c2 * FW, r2 * FH
    frame = arr[y0:y0+FH, x0:x0+FW].copy()
    cleaned = remove_bg(frame)
    cropped = cleaned[uy1:uy2, ux1:ux2]
    ci = Image.fromarray(cropped)
    
    # 等比缩放到128x128
    ratio = min(TARGET / ucw, TARGET / uch) * 0.92
    nw, nh = int(ucw * ratio), int(uch * ratio)
    resized = ci.resize((nw, nh), Image.LANCZOS)
    
    canvas = Image.new('RGBA', (TARGET, TARGET), (0, 0, 0, 0))
    ox = (TARGET - nw) // 2
    oy = TARGET - nh - 1  # 底部对齐
    canvas.paste(resized, (ox, oy))
    final.append(canvas)

# 拼接成一行
TF = len(final)
sheet = Image.new('RGBA', (TARGET * TF, TARGET), (0, 0, 0, 0))
for i, f in enumerate(final):
    sheet.paste(f, (i * TARGET, 0))
sheet.save(OUTPUT, optimize=True)

fsize = os.path.getsize(OUTPUT)
print(f'\nOutput: {TARGET * TF}x{TARGET}, {TF} frames, {fsize/1024:.1f}KB')
print(f'Auto-detect: {TARGET * TF}/{TARGET} = {TF} frames')

# 预览
ps = 192
preview = Image.new('RGBA', (ps * TF, ps), (80, 80, 80, 255))
for i, f in enumerate(final):
    big = f.resize((ps, ps), Image.LANCZOS)
    preview.paste(big, (i * ps, 0), big)
preview.save(r'D:\AIRearch\TD\assassin_preview.png')
print('Preview saved.')