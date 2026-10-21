#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")/.."
xcrun clang++ -std=c++17 -fobjc-arc -dynamiclib -O2 -arch arm64 -arch x86_64 -mmacosx-version-min=14.0 -framework Foundation -framework Vision -framework CoreVideo -framework CoreGraphics Tools/Native/KagemushaPose.mm -o Assets/Plugins/macOS/KagemushaPose.bundle/Contents/MacOS/KagemushaPose
