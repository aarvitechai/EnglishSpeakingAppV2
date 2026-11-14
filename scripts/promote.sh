#!/usr/bin/env bash
# Promote dev -> staging -> production with explicit --no-ff merge commits.
# Usage: scripts/promote.sh "Phase N: short description"
set -euo pipefail

label="${1:?Usage: scripts/promote.sh \"<release label>\"}"

if [[ -n "$(git status --porcelain)" ]]; then
  echo "Working tree is not clean. Commit or stash changes first." >&2
  exit 1
fi

git checkout -q staging
git merge --no-ff -q dev -m "Merge dev into staging: ${label}"
git checkout -q production
git merge --no-ff -q staging -m "Merge staging into production: ${label}"
git checkout -q dev

echo "Promoted '${label}' to staging and production."
git log --oneline --graph -6 --all
