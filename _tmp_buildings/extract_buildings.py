# -*- coding: utf-8 -*-
"""从本地反编译代码机械提取缺氧全部建筑定义。仅供参考核对，不修改 E:\\ONI-Ref。"""
import json
import os
import re
import sys

SRC = r"E:\ONI-Ref\Assembly-CSharp\Assembly-CSharp.decompiled.cs"
OUT = os.path.dirname(os.path.abspath(__file__))
sys.stdout.reconfigure(encoding="utf-8")

text = open(SRC, encoding="utf-8-sig", errors="replace").read()
lines = text.split("\n")
print("total lines:", len(lines))

# --- 顶层类型声明的括号范围 ---
scope_re = re.compile(r"^\t(?:public|internal|private|protected)?\s*(?:sealed\s+|abstract\s+|static\s+|partial\s+)*(class|struct)\s+(\w+)")
type_ranges = []
i = 0
n = len(lines)
while i < n:
    m = scope_re.match(lines[i])
    if m:
        depth = 0
        j = i
        started = False
        while j < n:
            depth += lines[j].count("{") - lines[j].count("}")
            if "{" in lines[j]:
                started = True
            if started and depth <= 0:
                break
            j += 1
        type_ranges.append((i, j, m.group(2)))
        i = j + 1
    else:
        i += 1

configs = [(a, b, name, idx) for idx, (a, b, name) in enumerate(type_ranges) if name.endswith("Config")]
print("top-level types:", len(type_ranges), "config types:", len(configs))

CALL = "BuildingTemplates.CreateBuildingDef("
str_arg = re.compile(r'"((?:[^"\\]|\\.)*)"')
named_id = re.compile(r'\bid:\s*"([^"]+)"')


def balanced_args(s, open_idx):
    """open_idx 指向 '('；返回参数列表字符串。"""
    depth = 0
    instr = False
    esc = False
    for k in range(open_idx, len(s)):
        c = s[k]
        if instr:
            if esc:
                esc = False
            elif c == "\\":
                esc = True
            elif c == '"':
                instr = False
            continue
        if c == '"':
            instr = True
        elif c == "(":
            depth += 1
        elif c == ")":
            depth -= 1
            if depth == 0:
                return s[open_idx + 1:k]
    return s[open_idx + 1:]


def split_args(args):
    out, depth, cur, instr, esc = [], 0, [], False, False
    for c in args:
        if instr:
            cur.append(c)
            if esc:
                esc = False
            elif c == "\\":
                esc = True
            elif c == '"':
                instr = False
            continue
        if c == '"':
            instr = True
            cur.append(c)
        elif c in "([{":
            depth += 1
            cur.append(c)
        elif c in ")]}":
            depth -= 1
            cur.append(c)
        elif c == "," and depth == 0:
            out.append("".join(cur).strip())
            cur = []
        else:
            cur.append(c)
    if "".join(cur).strip():
        out.append("".join(cur).strip())
    return out


def first_string(arg):
    m = str_arg.search(arg)
    return m.group(1) if m else None


def floats_in(arg):
    return re.findall(r"(?<![\w.])(\d+(?:\.\d+)?)f", arg)


records = []
for a, b, name, idx in configs:
    body = "\n".join(lines[a:b + 1])
    # 下一个顶层类型起点，限定搜索范围
    nxt = type_ranges[idx + 1][0] if idx + 1 < len(type_ranges) else n
    seg = "\n".join(lines[a:nxt])
    for m in re.finditer(re.escape(CALL), seg):
        argstr = balanced_args(seg, m.end() - 1)
        args = split_args(argstr)
        prefab = None
        rest = args[:]
        # 位置参数形式 "Id", w, h
        if args and args[0].startswith('"'):
            prefab = first_string(args[0])
            rest = args[1:]
        else:
            nm = named_id.search(argstr)
            if nm:
                prefab = nm.group(1)
        rec = {
            "config": name,
            "prefab": prefab,
            "args": args,
            "kwargs": {},
            "raw_head": argstr[:200],
        }
        for a2 in args:
            if re.match(r"^\w+\s*:", a2):
                k, _, v = a2.partition(":")
                rec["kwargs"][k.strip()] = v.strip()
        records.append(rec)

print("create-building-def calls:", len(records))

prefabs = sorted({r["prefab"] for r in records if r["prefab"]})
print("distinct prefabs:", len(prefabs))

with open(os.path.join(OUT, "raw_defs.json"), "w", encoding="utf-8") as f:
    json.dump(records, f, ensure_ascii=False, indent=1)
with open(os.path.join(OUT, "prefabs.txt"), "w", encoding="utf-8") as f:
    f.write("\n".join(prefabs))
print("written")
