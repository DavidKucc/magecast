#!/usr/bin/env bash
# Publishes Builds/Web to the gh-pages branch, which GitHub Pages serves at
# https://davidkucc.github.io/magecast/
#
# Build first: Unity > Tools > Arena > Build Web. The branch holds only the latest build and is
# replaced wholesale each time -- the history of a 17 MB binary blob is not worth keeping.
set -euo pipefail

root="$(cd "$(dirname "$0")" && pwd)"
build="$root/Builds/Web"
[ -f "$build/index.html" ] || { echo "no web build in $build - run Tools > Arena > Build Web first"; exit 1; }

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
cp -r "$build/." "$work/"
touch "$work/.nojekyll"

cd "$work"
git init -q -b gh-pages
git add -A
git commit -q -m "Web build of Mage Cast ($(date +%Y-%m-%d\ %H:%M))"
git push -f "$(git -C "$root" remote get-url origin)" gh-pages
echo "published - live in a minute or two at https://davidkucc.github.io/magecast/"
