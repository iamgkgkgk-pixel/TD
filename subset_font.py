#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""字体子集化脚本：从SimHei中提取游戏用到的字符，生成精简字体"""

import os
import re
from fontTools import subset

# 1. 扫描项目中所有.cs文件，提取用到的中文字符
scripts_dir = r'D:\AIRearch\TD\Unity\AetheraSurvivors\Assets\Scripts'
chars = set()
for root, dirs, files in os.walk(scripts_dir):
    for f in files:
        if f.endswith('.cs'):
            try:
                content = open(os.path.join(root, f), encoding='utf-8', errors='ignore').read()
                chars.update(re.findall(r'[\u4e00-\u9fff\u3000-\u303f\uff00-\uffef]', content))
            except:
                pass

# 2. 添加ASCII可打印字符 + 游戏中用到的特殊符号
ascii_chars = set()
for i in range(32, 127):
    ascii_chars.add(chr(i))

# 游戏中用到的特殊符号
special_chars = set('★☆◆◇√×①②③④⑤⑥⑦⑧⑨⑩≡')

all_chars = chars | ascii_chars | special_chars

print(f"总字符数: {len(all_chars)} (中文: {len(chars)}, ASCII: {len(ascii_chars)}, 特殊: {len(special_chars)})")

# 3. 生成unicode列表
unicodes = [ord(c) for c in all_chars]

# 4. 子集化
src = r'D:\AIRearch\TD\Unity\AetheraSurvivors\Assets\Resources\Fonts\SimHei.ttf'
dst = r'D:\AIRearch\TD\Unity\AetheraSurvivors\Assets\Resources\Fonts\SimHei_Subset.ttf'

args = subset.Options()
args.hinting = False
args.desubroutinize = True

font = subset.load_font(src, args)
subsetter = subset.Subsetter(args)
subsetter.populate(unicodes=unicodes)
subsetter.subset(font)
subset.save_font(font, dst, args)


src_size = os.path.getsize(src) / 1024
dst_size = os.path.getsize(dst) / 1024
print(f"原始字体: {src_size:.1f} KB")
print(f"精简字体: {dst_size:.1f} KB")
print(f"压缩率: {dst_size/src_size*100:.1f}%")
print(f"\n输出文件: {dst}")
