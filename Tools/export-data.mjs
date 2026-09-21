// 2D 드래고니아의 데이터 모듈을 그대로 실행해서 JSON 으로 뽑는다.
//
// 이야기·퀘스트·NPC·보스·성장 트리는 3D 로 옮겨도 하나도 안 바뀐다.
// 손으로 옮겨 적으면 반드시 틀리므로, 원본 모듈을 Node 에서 import 해서 직접 덤프한다.
// (data/ 는 브라우저 API 를 쓰지 않아서 Node 에서 그냥 돌아간다)
//
// 쓰기:  node Tools/export-data.mjs <2D저장소경로> [내보낼곳]
// 예:    node Tools/export-data.mjs ../Dragonia Assets/StreamingAssets/Data

import { readdir, mkdir, writeFile } from 'node:fs/promises';
import { pathToFileURL } from 'node:url';
import path from 'node:path';

const srcRepo = process.argv[2] || '../Dragonia';
const outDir = process.argv[3] || 'Assets/StreamingAssets/Data';
const dataDir = path.resolve(srcRepo, 'src/data');

// 함수로 된 값(설명문 생성기, 조건식 …)은 JSON 으로 못 담는다.
// 버리지 않고 소스를 문자열로 남겨 둔다 — C# 으로 옮길 때 이 목록만 보면 된다.
const functions = [];
function replacer(key, value) {
    if (typeof value === 'function') {
        functions.push({ where: this && this.id ? `${this.id}.${key}` : key, src: value.toString().replace(/\s+/g, ' ').slice(0, 200) });
        return { __fn: value.toString() };
    }
    if (value instanceof Set) return [...value];
    if (value instanceof Map) return Object.fromEntries(value);
    return value;
}

const files = (await readdir(dataDir)).filter(f => f.endsWith('.js')).sort();
await mkdir(outDir, { recursive: true });

const report = [];
for (const file of files) {
    const mod = await import(pathToFileURL(path.join(dataDir, file)).href);
    const out = {};
    for (const [name, value] of Object.entries(mod)) {
        if (typeof value === 'function' && /^[a-z]/.test(name)) continue;   // 도우미 함수는 건너뛴다
        out[name] = value;
    }
    const before = functions.length;
    const json = JSON.stringify(out, replacer, 2);
    const name = file.replace(/\.js$/, '');
    await writeFile(path.join(outDir, `${name}.json`), json, 'utf8');
    report.push({
        name,
        exports: Object.keys(out).length,
        kb: Math.round(json.length / 1024),
        fns: functions.length - before,
    });
}

// 웹(WebGL)에는 파일 시스템이 없어서 폴더를 훑을 수가 없다.
// 어떤 파일이 있는지 게임이 알 수 있도록 목록을 같이 남긴다.
await writeFile(path.join(outDir, '_files.json'), JSON.stringify(report.map(r => r.name), null, 2), 'utf8');

const width = Math.max(...report.map(r => r.name.length));
console.log('\n뽑아낸 것 →', path.resolve(outDir), '\n');
for (const r of report) {
    console.log(`  ${r.name.padEnd(width)}  ${String(r.exports).padStart(2)}개 항목  ${String(r.kb).padStart(3)}KB` + (r.fns ? `  · 함수 ${r.fns}개는 손으로 옮겨야 함` : ''));
}
console.log(`\n  합계 ${report.reduce((n, r) => n + r.kb, 0)}KB · 파일 ${report.length}개`);

if (functions.length) {
    await writeFile(path.join(outDir, '_functions-to-port.json'), JSON.stringify(functions, null, 2), 'utf8');
    console.log(`\n  C# 으로 손수 옮겨야 하는 함수 ${functions.length}개 → _functions-to-port.json`);
}
