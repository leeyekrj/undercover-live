#!/bin/bash
# 웹(WebGL) 빌드. 열어 둔 에디터와 부딪히지 않게 프로젝트 복사본에서 배치 모드로 빌드한다.
# 사용: tools/build-web.sh [출력 폴더] [작업 폴더]
#   출력 기본값 Builds/WebGL, 작업 폴더 기본값 Builds/_work (둘 다 깃에서 빠짐)
#   작업 폴더는 남겨 두면 다음 빌드가 빨라진다.
set -euo pipefail
REPO="$(cd "$(dirname "$0")/.." && pwd)"
OUT="${1:-$REPO/Builds/WebGL}"
WORK="${2:-$REPO/Builds/_work}"
VER="$(sed -n 's/^m_EditorVersion: //p' "$REPO/ProjectSettings/ProjectVersion.txt")"
UNITY="${UNITY:-/Applications/Unity/Hub/Editor/$VER/Unity.app/Contents/MacOS/Unity}"
[ -x "$UNITY" ] || { echo "Unity $VER 를 찾지 못했습니다: $UNITY (UNITY=경로 로 지정)" >&2; exit 1; }

mkdir -p "$WORK"
# 처음이면 캐시(Library)를 APFS 클론으로 가져와 가져오기 시간을 줄인다 (디스크를 거의 안 씀)
if [ ! -d "$WORK/Library" ] && [ -d "$REPO/Library" ]; then cp -Rc "$REPO/Library" "$WORK/Library"; fi
rsync -a --delete "$REPO/Assets/" "$WORK/Assets/"
rsync -a --delete "$REPO/ProjectSettings/" "$WORK/ProjectSettings/"
rsync -a --delete "$REPO/Packages/" "$WORK/Packages/"

# com.unity.ai.inference가 끌고 오는 com.unity.dt.app-ui의 웹 플러그인(libAppUITextMateLib.a)이
# 이 버전의 웹 링커와 맞지 않아 _embind_*/_emval_* 미정의로 링크가 실패한다. 게임은 이 패키지를 쓰지 않으므로 복사본에서만 뺀다.
python3 - "$WORK/Packages/manifest.json" <<'EOF'
import json, sys
p = sys.argv[1]
d = json.load(open(p))
d["dependencies"].pop("com.unity.ai.inference", None)
json.dump(d, open(p, "w"), indent=2)
EOF
rm -f "$WORK/Packages/packages-lock.json"

rm -rf "$OUT"
"$UNITY" -batchmode -projectPath "$WORK" -buildTarget WebGL \
  -executeMethod UndercoverLive.EditorTools.WebBuild.BuildCli -buildOutput "$OUT" -logFile "$WORK/build.log" \
  || { echo "빌드 실패. 로그: $WORK/build.log" >&2; grep -E "error CS|wasm-ld: error|\[WebBuild\]" "$WORK/build.log" | tail -10 >&2; exit 1; }
grep "\[WebBuild\]" "$WORK/build.log" | tail -1
du -sh "$OUT"
