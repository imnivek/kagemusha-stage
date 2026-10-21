# 影武者 Kagemusha Stage

舞台表演用的 Unity 體感互動遊戲原型。以攝影機追蹤舞者，上半身或全身入鏡皆可用姿勢觸發六招，3D 骨架即時跟隨。

**試玩與交接下載：[v0.4.2 完整交接包](https://github.com/imnivek/kagemusha-stage/releases/tag/v0.4.2)**

下載 Release 裡的 **Kagemusha-Handoff-v0.4.2.zip**，完整解壓後閱讀「先讀我_交接說明.txt」。內含 Mac app、Unity 原始碼、啟動器、教學、接手任務、真人驗收表與檔案完整性檢查。GitHub 的「Source code」壓縮檔只含原始碼；試玩請下載上述完整 ZIP。

## 直接試玩

需要 **macOS 14 以上**及攝影機；不需安裝 Unity。app 包含 Apple Silicon／Intel 架構，尚未逐機型驗收。

1. 先執行「開啟效果示範.command」，查看 3D 骨架與程序特效。
2. 按 V 切換真人鏡頭、允許相機權限，按 C 重定位。
3. 按 T 開教學，選課後按 Enter 開始練習。

本版為本機 Mac app；朋友需下載執行。app 採本機 ad-hoc 簽章，尚未完成 Apple 散布公證。

## 六招

- 螺旋丸：雙手畫圈集氣、舉手，再拋出。
- 水遁・水龍彈：胸前合掌集氣，再向兩側推開。
- 劍術：雙手回左腰握柄、向右上拔劍；回左腰保持即可收劍。
- 影分身：保持上身前傾、雙臂後伸的忍者跑姿勢。
- 龍脈召喚：雙掌在頭頂靠近集氣，再張開。
- 火遁：雙手在嘴前靠近，頭部與手位引導噴射方向。

自由演出直接以姿勢換招。詳細時序、鏡頭距離與操作請讀 [交接說明](先讀我_交接說明.txt) 及 [專案 README](Kagemusha/README.md)。

## 接手開發

Unity **6000.3.26f1**、URP **17.3.0**。在 Unity Hub 加入 `Kagemusha` 資料夾，開啟 `Assets/Kagemusha/Scenes/Kagemusha.unity`。原生 Mac 追蹤 bundle 與 Objective-C++ 原始碼均已附入。

- [接手任務](接手任務_可直接貼給開發夥伴.txt)
- [真人驗收表](真人驗收表.csv)
- [驗證範圍](驗證紀錄/驗證範圍.txt)
- [第三方素材聲明](Kagemusha/THIRD_PARTY_NOTICES.md)

v0.4.1 已修正慢速水遁被其他招式搶判，以及左腰收劍後立即重拔。v0.4.2 改用附 SIL OFL 1.1 的 Noto Sans TC，重建試玩版，並將最低 macOS 版本統一為 14.0。

六招、教學、自適應追蹤、水遁／收劍的合成測試與中文字形檢查通過。最新水遁／收劍仍待真人驗收；合成示範與靜態照片測試不能代替真人表演測試。

目前以程序生成的 3D 骨架和特效驗證表演流程。正式角色的 Humanoid rig、動作轉接及蒙皮尚待製作。
