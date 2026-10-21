#!/usr/bin/env bash
# Puts the README's screenshots and the brand mark into site/assets/generated, so the website always
# shows the same pictures as the README. The Pages workflow runs it; run it yourself to preview the
# site, then open site/index.html.
set -euo pipefail
cd "$(dirname "$0")/.."

out=site/assets/generated
rm -rf "$out"
mkdir -p "$out"

cp docs/screenshots/sweep.png docs/screenshots/sweep-session.png docs/screenshots/market.png "$out/"
cp docs/brand/mark-dark.svg docs/brand/app-icon-256.png "$out/"

echo "Staged $(ls "$out" | wc -l) files in $out"
