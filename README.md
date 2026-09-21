# DRAGONIA : RE2

2D 드래고니아를 3D 액션 RPG로 옮긴다. **이야기와 흐름은 그대로**, 전투만 탑뷰 슈팅에서
3인칭 젤다식으로 바꾼다.

원본: `../Dragonia` (웹, Canvas 2D, GitHub Pages)

## 무엇이 넘어오고 무엇이 안 넘어오는가

| | 넘어오는가 |
|---|---|
| 이야기 · 퀘스트 · 대사 · NPC · 보스 · 성장 트리 | **그대로** (JSON으로 뽑아 옴) |
| 게임 규칙 (짝 맺기 · 자식 · 굴 꾸미기 · 대장간 · 낮밤) | 설계는 그대로, 코드는 다시 |
| 전투 | **바뀐다** — 마우스 조준 연사 → 락온 + 근접 + 숨결 |
| 그림 · 이펙트 · UI | 전부 새로 |

전투만 바뀌는 이유: 위에서 내려다보며 마우스로 조준하던 것이 3인칭에서는 성립하지 않는다.
대신 2D에서 이미 쓰던 **무적 대시**가 그대로 구르기가 되고, **예고 → 발동 → 숨 고르기**
적 AI가 그대로 3D 보스 패턴이 된다. 설계는 거의 안 버린다.

## 열기

**Unity 6000.3.11f1** (이 컴퓨터에 깔려 있는 버전에 맞춰 뒀다).
Unity Hub ▸ Add ▸ Add project from disk ▸ 이 폴더.

### 이 저장소에 없는 것과 그 이유

여기 있는 건 **소스와 데이터뿐**이다. Unity 가 스스로 만들어 내는 것들은 넣지 않았다
(`.gitignore` 참고). 처음 열면 Unity 가 알아서 만든다:

- `.meta` 파일 — 에셋마다 하나씩, 처음 열 때 생긴다
- `Library/` — 캐시. 처음 열 때 몇 분 걸린다
- `ProjectSettings/` 의 나머지 — 지금은 `ProjectVersion.txt` 만 있고 나머지는
  기본값으로 채워진다

**씬은 없다.** 빈 프로젝트로 열리는 게 정상이고, 아래 "지금 해야 하는 일"대로
아레나 하나를 만들면 된다.

### 입력 방식 주의

스크립트가 예전 입력(`Input.GetAxisRaw`)을 쓴다. 혹시 프로젝트가
**Input System Package (New)** 전용으로 잡혀 있으면 실행하자마자 예외가 난다.
Edit ▸ Project Settings ▸ Player ▸ **Active Input Handling** 을
`Both` 또는 `Input Manager (Old)` 로 둔다.

### 열다가 꼬이면

가장 확실한 길은 Unity Hub 에서 **새 3D 프로젝트를 만들고**, 거기에
이 저장소의 `Assets/`, `Packages/manifest.json`, `Tools/`, `docs/` 를 덮어쓰는 것이다.
그러면 Unity 가 제 손으로 만든 설정 위에 우리 코드만 얹힌다.

## 데이터 다시 뽑기

2D 쪽에서 대사나 퀘스트를 고쳤으면, 손으로 옮겨 적지 말고 이걸 돌린다.

```bash
node Tools/export-data.mjs ../Dragonia Assets/StreamingAssets/Data
```

지금 들어 있는 것: 퀘스트 17개, 보스 5마리, 지도 19장, NPC 대화, 성장 트리, 일과, 기록 —
**22개 묶음 233KB**.

조건식과 대사 생성기 **81개**는 함수라서 JSON에 담기지 않는다. 버리지 않고
`_functions-to-port.json`에 소스를 남겨 뒀다. C#으로 옮길 때 그 목록만 보면 된다.
`GameData.CountUnported()`로 몇 개 남았는지 셀 수 있다.

## 에셋을 갈아끼울 수 있게 만든 방법

지금은 무료 로우폴리로 시작하지만 나중에 제대로 된 모델로 갈아탈 수 있어야 한다.
그래서 네 가지 규칙을 지킨다.

**1. 게임 코드는 프리팹을 직접 들지 않는다.** `"MORGATH"` 같은 id만 알고,
id → 프리팹은 `AssetRegistry` 한 곳에서만 잇는다. 교체할 때 고치는 곳은 그 표 하나다.

**2. 애니메이션 이름은 우리가 정한다.** `Anim.Idle`, `Anim.Bite`, `Anim.Breath` …
모델이 들고 온 클립 이름은 게임이 모른다. 새 모델은 Animator에 이 이름의 상태만
만들어 주면 된다.

**3. "용"과 "용의 모습"을 나눈다.** 체력·판정·상태는 부모(`DragonController`)에,
모델은 자식(`DragonVisual`)에. 모델을 통째로 갈아도 부모는 모른다.

**4. 모델이 없어도 게임은 돌아간다.** 등록되지 않은 id는 회색 캡슐이 대신 선다.
`AssetRegistry.MissingIds()`로 무엇을 더 구해야 하는지 볼 수 있다.

**갈아끼워지지 않는 것도 알아 두자.** 몸 비율이 크게 다르면 애니메이션이 깨지고,
조명·후처리·이펙트는 룩에 맞춰 조율한 것이라 스타일을 바꾸면 어차피 다시 한다.
"모델 교체"는 대비돼 있고, "스타일 교체"는 공짜가 아니다.

## 지금 있는 것

```
Assets/Scripts/
  Registry/AssetRegistry.cs       id → 프리팹. 갈아끼우는 자리
  Characters/DragonVisual.cs      애니메이션 이름표 + 모습 갈아끼우기
  Characters/DragonController.cs  3인칭 조작 · 구르기(무적) · 기력 · 물기 · 숨결
  Characters/LockOnCamera.cs      어깨 너머 카메라 · 락온
  Combat/Damage.cs                피해 통로 (같은 편끼리는 안 맞는다)
  Combat/Hazard.cs                예고 후 터지는 바닥 장판
  Combat/Projectile.cs            날아가는 것 (직선 · 유도)
  Enemies/EnemyBrain.cs           예고 → 발동 → 숨 고르기 (2D에서 옮김)
  Enemies/BossBrain.cs            보스 패턴 14종, 데이터가 순서를 정한다
  Data/GameData.cs                JSON 읽기
  UI/UIManager.cs                 프리팹으로만 만드는 화면
Assets/StreamingAssets/Data/      뽑아 온 이야기 22개 묶음
Tools/export-data.mjs             2D 데이터 → JSON
Tools/check.py                    데이터·괄호 자가 점검
```

## 보스 패턴

어떤 패턴을 어떤 차례로 쓰는지는 **코드가 아니라 데이터**에 있다
(`enemies.json` 의 `BOSSES[id].patterns`). 순서를 바꾸려면 2D 쪽
`src/data/enemies.js` 를 고치고 내보내기를 다시 돌린다.

| 보스 | 속성 | 패턴 |
|---|---|---|
| 모르가스 | 얼음 | RING · SUMMON · AIMED · BONE_RAIN |
| 잘고라 | 번개 | TWIN_BEAM · AIMED · SPIRAL · AIMED · CHARGE |
| 글라시아 | 얼음 | HOMING · ICE_FIELD · BLIZZARD · RING · HOMING |
| 바실 | 불 | BURROW · CHARGE · QUAKE · AIMED · BURROW |
| 이그나르 | 불 | METEOR_RAIN · AIMED · FLAME_WALL · CHARGE · SPIRAL |

열넷 중 여섯(BONE_RAIN · ICE_FIELD · QUAKE · METEOR_RAIN · FLAME_WALL · CHARGE)은
전부 `Hazard` 하나로 만들어진다 — 바닥에 예고를 깔았다가 터지는 장판이다.
반지름과 터지는 시각만 다르다.

**지키는 규칙:** 예고 시간은 0.7초 밑으로 내리지 않는다. 보고 피할 수 없는 공격은 넣지 않는다.
체력이 절반 밑이면 발악에 들어가 탄이 늘고 쉬는 틈이 짧아진다.

## 조작 (지금 정해 둔 것)

| | |
|---|---|
| WASD | 카메라 기준 이동 |
| Shift(꾹) | 달리기 — 기력을 먹는다 |
| Space | 구르기 — 가운데 구간만 무적 |
| 왼클릭 | 물기 |
| 오른클릭 | 숨결 |
| Q | 락온 걸기/풀기 |

## 웹으로 내보내기 (GitHub Pages)

페이지는 이미 살아 있다 → <https://gammja17.github.io/Dragonia-RE2/>
지금은 안내 페이지만 있고, 빌드가 나오면 그 자리에서 바로 플레이할 수 있다.

빌드는 Unity 에서 직접 뽑는다. CI 로 돌릴 수도 있지만 Unity 라이선스를 비밀값으로
넣어야 해서, 혼자 만드는 동안은 직접 뽑는 쪽이 빠르다.

1. **File ▸ Build Settings ▸ WebGL ▸ Switch Platform**
2. **Player Settings ▸ Publishing Settings** 에서 — *여기가 중요하다*
   - **Compression Format: `Disabled`** (또는 `Gzip` + **`Decompression Fallback` 켜기**)
   - **`Data Caching` 끄기**
3. `docs/game/` 으로 빌드
4. 커밋해서 올리면 `.../Dragonia-RE2/game/` 에서 돌아간다

**압축 설정을 그냥 두면 흰 화면만 나온다.** GitHub Pages 는 `Content-Encoding` 헤더를
못 붙여서, 브라우저가 `.br`/`.gz` 파일을 압축된 줄 모르고 그대로 읽으려다 실패한다.
`Decompression Fallback` 을 켜면 Unity 가 자바스크립트로 직접 풀어서 이 문제를 피한다.

빌드가 20~40MB 쯤 된다. 파일 하나가 100MB 를 넘으면 GitHub 가 거부하니,
그때는 `Compression Format: Gzip` 으로 줄인다.

## 지금 해야 하는 일 (에디터에서)

코드로 못 하는 것들이다. 순서대로 하면 보스 하나짜리 시험판이 돈다.

1. 빈 씬 하나 (`Assets/Scenes/Arena.unity`) — 바닥 평면 하나면 충분하다
2. `Resources/AssetRegistry` 에셋 생성 (Create ▸ Dragonia ▸ Asset Registry)
3. 플레이어: 빈 오브젝트에 `CharacterController` + `DragonController`,
   자식으로 `DragonVisual`. 태그를 `Player`로
4. 카메라에 `LockOnCamera`, `follow`에 플레이어 연결
5. 적: `CharacterController` + `EnemyBrain` + `DragonVisual`
6. 무료 모델을 받아 `AssetRegistry`에 id와 함께 등록

모델이 없어도 4번까지 하면 회색 캡슐로 굴러다닐 수 있다. **조작감부터 본다.**

## 다음

- [ ] 아레나 시험판 — 내 용 하나, 보스 하나, 락온·구르기·숨결
- [x] 숨결 · 물기 판정
- [x] 보스 패턴 14종
- [ ] 함수 81개 C#으로 옮기기
- [ ] 예고 표식 · 피격 이펙트 (지금은 회색 원통으로 대신한다)
- [ ] 지도 19장을 3D 구역으로
- [ ] 생활 시스템 (짝·자식·굴·대장간)

## 출처

이야기·설계는 2D 드래고니아에서 가져왔다. 3D 에셋은 아직 없다.
쓰기 시작하면 원본 2D 쪽처럼 `CREDITS.md`를 만들어 적는다.
