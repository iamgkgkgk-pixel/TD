#!/usr/bin/env python3
"""
地图素材后处理脚本 — #415
对已有资源执行：
  1. deco_*.png: 缩放到128×128（保持透明底），高质量LANCZOS
  2. tile_*.png: 缩放到256×256（满铺无透明）
  3. 清理杂文件名
"""

import os
import sys
from PIL import Image

MAPS_DIR = os.path.join(os.path.dirname(__file__), "..",
    "Unity", "AetheraSurvivors", "Assets", "Resources", "Sprites", "Maps")

def process_decorations():
    """装饰物：缩放到128×128，居中，保持透明底"""
    count = 0
    for f in sorted(os.listdir(MAPS_DIR)):
        if f.startswith("deco_") and f.endswith(".png"):
            path = os.path.join(MAPS_DIR, f)
            img = Image.open(path)
            
            # 确保RGBA
            if img.mode != "RGBA":
                img = img.convert("RGBA")
            
            original_size = img.size
            
            # 裁剪透明边缘（去掉多余空白）
            bbox = img.getbbox()
            if bbox:
                img = img.crop(bbox)
            
            # 缩放到128×128（保持宽高比，居中放置）
            target_size = 128
            img.thumbnail((target_size, target_size), Image.LANCZOS)
            
            # 居中放置到128×128画布
            canvas = Image.new("RGBA", (target_size, target_size), (0, 0, 0, 0))
            offset_x = (target_size - img.width) // 2
            offset_y = (target_size - img.height) // 2
            canvas.paste(img, (offset_x, offset_y))
            
            canvas.save(path, "PNG", optimize=True)
            count += 1
            print(f"  ✅ Deco: {f} ({original_size} → {target_size}×{target_size})")
    
    return count

def process_tiles():
    """地形Tile：缩放到256×256"""
    count = 0
    for f in sorted(os.listdir(MAPS_DIR)):
        if f.startswith("tile_") and f.endswith(".png"):
            path = os.path.join(MAPS_DIR, f)
            img = Image.open(path)
            
            original_size = img.size
            if img.size != (256, 256):
                img = img.resize((256, 256), Image.LANCZOS)
                img.save(path, "PNG", optimize=True)
                print(f"  ✅ Tile: {f} ({original_size} → 256×256)")
                count += 1
            else:
                print(f"  ⏭️  Tile: {f} (已经是256×256，跳过)")
    
    return count

def cleanup_junk():
    """清理杂文件（非tile_/deco_前缀的杂乱命名文件）"""
    count = 0
    for f in sorted(os.listdir(MAPS_DIR)):
        if f.endswith(".png") and not f.startswith("tile_") and not f.startswith("deco_"):
            if f.endswith(".meta"):
                continue
            path = os.path.join(MAPS_DIR, f)
            size_kb = os.path.getsize(path) / 1024
            print(f"  ⚠️  杂文件: {f} ({size_kb:.0f}KB) — 未删除，请手动确认")
            count += 1
    return count

if __name__ == "__main__":
    print(f"🗂️  目标目录: {MAPS_DIR}")
    print()
    
    print("📐 处理装饰物 (deco_*.png → 128×128):")
    deco_count = process_decorations()
    print(f"   共处理 {deco_count} 张装饰物")
    print()
    
    print("📐 处理地形Tile (tile_*.png → 256×256):")
    tile_count = process_tiles()
    print(f"   共处理 {tile_count} 张地形")
    print()
    
    print("🧹 检查杂文件:")
    junk_count = cleanup_junk()
    if junk_count == 0:
        print("   无杂文件")
    print()
    
    print(f"🎉 后处理完成！装饰物{deco_count}张 + 地形{tile_count}张")
