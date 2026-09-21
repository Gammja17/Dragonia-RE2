# -*- coding: utf-8 -*-
# 3D 프로젝트 자가 점검: 스크립트가 찾는 데이터가 실제로 있는지 + C# 괄호가 맞는지
import json, io, glob, os, sys

ok = True
BS = chr(92)   # 역슬래시

want = [('quests', 'QUESTS'), ('enemies', 'BOSSES'), ('enemies', 'ENEMIES'),
        ('maps', 'MAPS'), ('elements', 'STAGES'), ('elements', 'ELEMENTS'),
        ('growth', 'GROWTH_NODES'), ('skills', 'SKILLS'), ('routines', 'ROUTINES')]

print('== GameData.cs 가 꺼내 쓰는 것들 ==')
for f, key in want:
    d = json.load(io.open(f'Assets/StreamingAssets/Data/{f}.json', encoding='utf-8'))
    if key not in d:
        print(f'  [X] {f}.json 에 {key} 없음'); ok = False
    else:
        v = d[key]
        n = len(v) if isinstance(v, (list, dict)) else '?'
        print(f'  [O] {f}.{key}  {n}개')

bad = []
for p in glob.glob('Assets/StreamingAssets/Data/*.json'):
    try:
        json.load(io.open(p, encoding='utf-8'))
    except Exception as e:
        bad.append((os.path.basename(p), str(e)[:60]))
print('\n== JSON 유효성 ==')
print('  깨진 파일:', bad if bad else '없음')
if bad: ok = False

print('\n== C# 중괄호 ==')
for p in sorted(glob.glob('Assets/Scripts/**/*.cs', recursive=True)):
    s = io.open(p, encoding='utf-8').read()
    depth = 0; instr = False; incom = False; esc = False; i = 0
    while i < len(s):
        c = s[i]
        if incom:
            if c == '\n': incom = False
        elif instr:
            if esc: esc = False
            elif c == BS: esc = True
            elif c == '"': instr = False
        elif c == '/' and i + 1 < len(s) and s[i+1] == '/':
            incom = True; i += 1
        elif c == '"':
            instr = True
        elif c == '{':
            depth += 1
        elif c == '}':
            depth -= 1
        i += 1
    if depth != 0: ok = False
    print(f'  [{"O" if depth == 0 else "X"}] {os.path.basename(p):26} {"맞음" if depth == 0 else f"어긋남 {depth}"}')

print('\n결과:', '통과' if ok else '문제 있음')
sys.exit(0 if ok else 1)
