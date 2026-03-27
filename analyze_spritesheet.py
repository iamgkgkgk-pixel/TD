"""
处理 GodMode AI 生成的 spritesheet：
1. 去除背景色（变透明）
2. 从42帧中均匀抽取8帧
3. 裁剪角色区域（去除多余空白）
4. 重新打包成一行排列的 spritesheet
"""
from PIL import Image
import numpy as np
import os
import sys

# 修复Windows GBK编码问题
sys.stdout.reconfigure(encoding='utf-8')

# === 配置 ===
INPUT_PATH = r'D:\AIRearch\TD\Unity\AetheraSurvivors\Assets\Resources\Sprites\Enemies\enemy_infantry_sprite_topdown_fast_run_looping.png'
OUTPUT_PATH = r'D:\AIRearch\TD\Unity\AetheraSurvivors\Assets\Resources\Sprites\Enemies\enemy_infantry_walk.png'
GRID_COLS = 6
GRID_ROWS = 7
FRAME_W = 512
FRAME_H = 512
TARGET_FRAMES = 8  # 抽取8帧
TARGET_FRAME_SIZE = 128  # 最终每帧尺寸
BG_TOLERANCE = 40  # 背景色容差（加大一些）

# === 加载图片 ===
img = Image.open(INPUT_PATH).convert('RGBA')
arr = np.array(img)
print(f'原始图片: {img.size}, 模式: {img.mode}')

# === 检测背景色（取四个角的中位数） ===
corners = np.concatenate([
    arr[0:20, 0:20, :3].reshape(-1, 3),
    arr[0:20, -20:, :3].reshape(-1, 3),
    arr[-20:, 0:20, :3].reshape(-1, 3),
])
bg_color = np.median(corners, axis=0).astype(int)
print(f'检测到背景色: RGB({bg_color[0]}, {bg_color[1]}, {bg_color[2]})')

# 分析第一帧的颜色分布
first_frame = arr[0:FRAME_H, 0:FRAME_W]
first_rgb = first_frame[:,:,:3].astype(int)
diff = np.abs(first_rgb - bg_color.reshape(1,1,3))
max_diff_per_pixel = np.max(diff, axis=2)

print(f'第一帧像素与背景色的最大差值分布:')
for threshold in [10, 20, 30, 40, 50, 60]:
    pct = np.sum(max_diff_per_pixel < threshold) / (FRAME_W * FRAME_H) * 100
    print(f'  差值<{threshold}: {pct:.1f}%')

# === 提取所有帧 ===
all_frames = []
for r in range(GRID_ROWS):
    for c in range(GRID_COLS):
        x1, y1 = c * FRAME_W, r * FRAME_H
        frame = img.crop((x1, y1, x1 + FRAME_W, y1 + FRAME_H))
        all_frames.append(frame)

total_frames = len(all_frames)
print(f'总帧数: {total_frames}')

# === 去除背景色（变透明） ===
def remove_background(frame_img, bg_rgb, tolerance):
    """将接近背景色的像素变为透明"""
    arr = np.array(frame_img).copy()
    rgb = arr[:, :, :3].astype(int)
    diff = np.max(np.abs(rgb - bg_rgb.reshape(1, 1, 3)), axis=2)
    bg_mask = diff < tolerance
    arr[bg_mask, 3] = 0  # 设置alpha为0
    return Image.fromarray(arr)

# === 均匀抽取帧 ===
step = total_frames / TARGET_FRAMES
selected_indices = [int(i * step) for i in range(TARGET_FRAMES)]
print(f'抽取帧索引: {selected_indices}')

selected_frames = []
for idx in selected_indices:
    frame = all_frames[idx]
    frame = remove_background(frame, bg_color, BG_TOLERANCE)
    selected_frames.append(frame)

# === 裁剪角色区域 ===
def get_content_bbox(frame_img):
    arr = np.array(frame_img)
    alpha = arr[:, :, 3]
    rows = np.any(alpha > 0, axis=1)
    cols = np.any(alpha > 0, axis=0)
    if not np.any(rows) or not np.any(cols):
        return (0, 0, frame_img.width, frame_img.height)
    rmin, rmax = np.where(rows)[0][[0, -1]]
    cmin, cmax = np.where(cols)[0][[0, -1]]
    return (cmin, rmin, cmax + 1, rmax + 1)

all_bboxes = [get_content_bbox(f) for f in selected_frames]
min_x = min(b[0] for b in all_bboxes)
min_y = min(b[1] for b in all_bboxes)
max_x = max(b[2] for b in all_bboxes)
max_y = max(b[3] for b in all_bboxes)

padding = 5
min_x = max(0, min_x - padding)
min_y = max(0, min_y - padding)
max_x = min(FRAME_W, max_x + padding)
max_y = min(FRAME_H, max_y + padding)

content_w = max_x - min_x
content_h = max_y - min_y
print(f'角色区域: ({min_x},{min_y}) -> ({max_x},{max_y}), 尺寸: {content_w}x{content_h}')

# === 裁剪并缩放到目标尺寸 ===
final_frames = []
for frame in selected_frames:
    cropped = frame.crop((min_x, min_y, max_x, max_y))
    ratio = min(TARGET_FRAME_SIZE / content_w, TARGET_FRAME_SIZE / content_h)
    new_w = int(content_w * ratio)
    new_h = int(content_h * ratio)
    resized = cropped.resize((new_w, new_h), Image.LANCZOS)
    
    canvas = Image.new('RGBA', (TARGET_FRAME_SIZE, TARGET_FRAME_SIZE), (0, 0, 0, 0))
    offset_x = (TARGET_FRAME_SIZE - new_w) // 2
    offset_y = (TARGET_FRAME_SIZE - new_h) // 2
    canvas.paste(resized, (offset_x, offset_y))
    final_frames.append(canvas)

# === 拼接成一行 spritesheet ===
sheet_w = TARGET_FRAME_SIZE * TARGET_FRAMES
sheet_h = TARGET_FRAME_SIZE
spritesheet = Image.new('RGBA', (sheet_w, sheet_h), (0, 0, 0, 0))

for i, frame in enumerate(final_frames):
    spritesheet.paste(frame, (i * TARGET_FRAME_SIZE, 0))

spritesheet.save(OUTPUT_PATH)
print(f'[OK] SpriteSheet 已保存: {OUTPUT_PATH}')
print(f'   尺寸: {sheet_w}x{sheet_h}')
print(f'   帧数: {TARGET_FRAMES}')
print(f'   每帧: {TARGET_FRAME_SIZE}x{TARGET_FRAME_SIZE}')

file_size = os.path.getsize(OUTPUT_PATH)
print(f'   文件大小: {file_size/1024:.1f} KB')

# 保存预览
preview_size = 256
preview_w = preview_size * TARGET_FRAMES
preview = Image.new('RGBA', (preview_w, preview_size), (128, 128, 128, 255))
for i, frame in enumerate(final_frames):
    enlarged = frame.resize((preview_size, preview_size), Image.LANCZOS)
    preview.paste(enlarged, (i * preview_size, 0), enlarged)
preview.save(r'D:\AIRearch\TD\spritesheet_preview.png')
print(f'   预览已保存: spritesheet_preview.png')

# 分析原始spritesheet
img = Image.open(r'D:\AIRearch\TD\Unity\AetheraSurvivors\Assets\Art_Source\enemy_infantry_sprite_topdown_fast_run_looping.png')
arr = np.array(img)
w, h = img.size
print(f'原始图片尺寸: {w}x{h}')

# 检查可能的网格布局
print('\n可能的网格布局:')
for cols in [4, 6, 8, 12]:
    fw = w // cols
    for rows in range(1, 20):
        if h % rows == 0:
            fh = h // rows
            if 200 < fw < 600 and 200 < fh < 600:
                print(f'  {cols}列x{rows}行 -> 每帧{fw}x{fh}')

# 提取每一行的第一帧，保存为预览图来分析方向
# 假设 6x7 布局
cols, rows = 6, 7
fw, fh = w // cols, h // rows
print(f'\n使用 {cols}x{rows} 布局, 每帧 {fw}x{fh}')

# 保存每行第一帧的预览
preview_h = fh * rows
preview_w = fw * 2  # 每行显示前2帧
preview = Image.new('RGBA', (preview_w, preview_h), (200, 200, 200, 255))
for r in range(rows):
    for c in range(min(2, cols)):
        frame = img.crop((c * fw, r * fh, (c+1) * fw, (r+1) * fh))
        preview.paste(frame, (c * fw, r * fh))
preview.save(r'D:\AIRearch\TD\row_preview.png')
print(f'行预览已保存: row_preview.png')

# 分析每行帧之间的差异（判断哪些行是同一方向的动画序列）
print('\n每行帧间差异分析:')
for r in range(rows):
    diffs = []
    for c in range(cols - 1):
        f1 = arr[r*fh:(r+1)*fh, c*fw:(c+1)*fw]
        f2 = arr[r*fh:(r+1)*fh, (c+1)*fw:(c+2)*fw]
        diff = np.mean(np.abs(f1.astype(float) - f2.astype(float)))
        diffs.append(diff)
    avg_diff = np.mean(diffs)
    print(f'  行{r}: 平均帧间差异={avg_diff:.2f} ({"动画序列" if avg_diff < 30 else "可能不同方向"})')

# 分析行间差异（判断不同行是否是不同方向）
print('\n行间差异分析（比较每行第一帧）:')
for r in range(rows - 1):
    f1 = arr[r*fh:(r+1)*fh, 0:fw]
    f2 = arr[(r+1)*fh:(r+2)*fh, 0:fw]
    diff = np.mean(np.abs(f1.astype(float) - f2.astype(float)))
    print(f'  行{r} vs 行{r+1}: 差异={diff:.2f}')

# 保存所有42帧的缩略图网格
thumb_size = 128
grid_preview = Image.new('RGBA', (thumb_size * cols, thumb_size * rows), (200, 200, 200, 255))
for r in range(rows):
    for c in range(cols):
        frame = img.crop((c * fw, r * fh, (c+1) * fw, (r+1) * fh))
        frame_thumb = frame.resize((thumb_size, thumb_size), Image.LANCZOS)
        grid_preview.paste(frame_thumb, (c * thumb_size, r * thumb_size))
grid_preview.save(r'D:\AIRearch\TD\grid_preview.png')
print(f'\n全帧网格预览已保存: grid_preview.png ({thumb_size * cols}x{thumb_size * rows})')