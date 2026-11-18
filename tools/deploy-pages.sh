#!/bin/bash
# 웹 빌드 폴더를 gh-pages 브랜치로 올린다. 브랜치는 매번 빌드 한 벌만 남도록 새로 만든다(이력 누적 없음).
# 사용: tools/deploy-pages.sh <빌드 폴더> [원격 이름]
set -euo pipefail
BUILD="${1:?빌드 폴더를 지정하세요 (index.html이 있는 폴더)}"
REMOTE="${2:-origin}"
[ -f "$BUILD/index.html" ] || { echo "index.html이 없습니다: $BUILD" >&2; exit 1; }

REPO="$(cd "$(dirname "$0")/.." && pwd)"
BUILD="$(cd "$BUILD" && pwd)"
URL="$(git -C "$REPO" remote get-url "$REMOTE")"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

cp -R "$BUILD"/. "$WORK"/
touch "$WORK/.nojekyll"   # 밑줄로 시작하는 파일도 그대로 내보내게
cd "$WORK"
git init -q -b gh-pages
git add -A
git -c user.name="$(git -C "$REPO" config user.name)" -c user.email="$(git -C "$REPO" config user.email)" \
  commit -q -m "웹 빌드 $(date '+%Y-%m-%d %H:%M')"
git push -q -f "$URL" gh-pages
echo "올림: $URL (gh-pages)"
