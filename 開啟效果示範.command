#!/bin/zsh
set -euo pipefail
stage_dir="${0:A:h}"
if [[ "$(uname -s)" != "Darwin" ]]; then
  print "此試玩程式需要 macOS 14 以上；其他平台請先閱讀交接說明。"
  exit 1
fi
stage_os_version="$(sw_vers -productVersion)"
if (( ${stage_os_version%%.*} < 14 )); then
  print "真人追蹤需要 macOS 14 以上，目前版本：$stage_os_version"
  exit 1
fi
if [[ ! -d "$stage_dir/Kagemusha/Builds/Kagemusha Stage.app" ]]; then
  print "找不到試玩程式；請先完整解壓交接包，保留資料夾結構。"
  exit 1
fi
print "此模式為合成骨架；若程式已開啟，請在程式內按 D 切換。"
open "$stage_dir/Kagemusha/Builds/Kagemusha Stage.app" --args --demo
