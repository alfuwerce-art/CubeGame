#!/usr/bin/env bash
set -euo pipefail

# One-time import of the approved public Factoria review snapshot into CubeGame.
# NO --squash: preserve the Factoria commit ancestry as a second parent.
# Do not modify the source repository or delete its branches.
TARGET_BRANCH="codex/factoria-unification"
SOURCE_REPO="https://github.com/alfuwerce-art/factoria.git"
SOURCE_SHA="60f984c81b73448d79e8d2e4c57cbc19c20a9393"
PREFIX="Factory"

test "$(git branch --show-current)" = "$TARGET_BRANCH" || { echo "Wrong branch"; exit 1; }
test -z "$(git status --porcelain)" || { echo "Dirty working tree"; exit 1; }
test ! -e "$PREFIX" || { echo "Factory/ already exists; refuse duplicate import"; exit 1; }

git remote add factoria-import "$SOURCE_REPO"
trap 'git remote remove factoria-import >/dev/null 2>&1 || true' EXIT
git fetch --no-tags factoria-import "$SOURCE_SHA"
git cat-file -e "$SOURCE_SHA^{commit}"
git -c user.name="CubeGame Migration" -c user.email="41898282+github-actions[bot]@users.noreply.github.com" subtree add --prefix="$PREFIX" "$SOURCE_SHA" -m "chore(factory): import Factoria repository with full Git ancestry"

echo "Imported source SHA: $SOURCE_SHA"
git ls-tree -r --name-only HEAD "$PREFIX" | wc -l
git show --no-patch --pretty='%H %P' HEAD
git push origin "HEAD:refs/heads/$TARGET_BRANCH"
