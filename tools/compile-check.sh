#!/bin/bash
# 스크립트 컴파일만 확인한다 (배치 모드, 창 없음). 맥 부하를 막으려고 한 번에 하나만 돌게 잠근다.
# 사용: tools/compile-check.sh [프로젝트 폴더]   (기본: 이 스크립트가 있는 프로젝트)
# 🚨 Paul이 에디터로 열어 둔 ~/UnityProjects/UndercoverLive 에는 돌리지 않는다(같은 프로젝트에 에디터 둘 불가).
set -uo pipefail
PROJ="$(cd "${1:-$(dirname "$0")/..}" && pwd)"
if [ "$PROJ" = "$HOME/UnityProjects/UndercoverLive" ]; then echo "원본 프로젝트에는 돌리지 않습니다. 작업 폴더를 지정하세요." >&2; exit 2; fi
VER="$(sed -n 's/^m_EditorVersion: //p' "$PROJ/ProjectSettings/ProjectVersion.txt")"
UNITY="${UNITY:-/Applications/Unity/Hub/Editor/$VER/Unity.app/Contents/MacOS/Unity}"
LOCK="${TMPDIR:-/tmp}/undercover-unity.lock"
waited=0
until mkdir "$LOCK" 2>/dev/null; do
  sleep 5; waited=$((waited+5))
  [ $waited -ge 1200 ] && { echo "잠금 대기 20분 초과: $LOCK" >&2; exit 3; }
done
trap 'rmdir "$LOCK" 2>/dev/null' EXIT
LOG="$PROJ/compile.log"
"$UNITY" -batchmode -quit -projectPath "$PROJ" -buildTarget WebGL -logFile "$LOG" >/dev/null 2>&1
code=$?
errs=$(grep -E "error CS[0-9]+" "$LOG" | sort -u)
if [ -n "$errs" ]; then echo "컴파일 오류:"; echo "$errs" | head -40; exit 1; fi
warns=$(grep -E "warning CS[0-9]+" "$LOG" | grep -F "Assets/_Game" | sort -u | head -15)
[ -n "$warns" ] && { echo "경고(_Game):"; echo "$warns"; }
echo "컴파일 통과 (Unity 종료 코드 $code)"
