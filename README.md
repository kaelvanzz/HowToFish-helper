# HTFMod

Cheat menu mod for **How to Fish** (BepInEx 6). Press INSERT in game.
给 How to Fish 写的作弊菜单，游戏里按 INSERT 开关。

> **官方渠道声明 / Official source only**
> 本仓库(`github.com/kaelvanzz/HowToFish-helper`)及其 Releases 是唯一官方来源。任何其他渠道发布的二进制均为非官方 —— AGPL 3.0 要求它们附带完整源码，**拿不出对应源码的版本不要用**。官方 zip 的 SHA-256 只公布在各 Release 说明里，下载任何副本前先核对。
> 未经许可，"HTFMod" 名称与作者(kaelvanzz)不得用于二次打包版本的宣传。
> This repository and its Releases are the only official source. Binaries from anywhere else are unofficial — AGPL 3.0 obliges them to ship the full source; no matching source, no trust. The name "HTFMod" and the author may not be used to advertise modified builds.

**English**
Install: close game, unzip the release, run `install.bat`, launch via Steam, press INSERT. Antivirus may flag `winhttp.dll` (injector false positive) — whitelist it.
Multiplayer: the host needs the mod too for most features.
Build: .NET SDK; set `GameManaged` in `src/HTFMod/HTFMod.csproj` to your game's `Managed` folder; run `build.ps1`, then drop `build/HTFMod.dll` into `BepInEx/plugins/`.
License: AGPL 3.0. For playing with friends — use at your own risk.

**简体中文**
安装：关游戏，解压 Release，双击 `install.bat`，Steam 启动游戏，按 INSERT 开菜单。杀软可能误报 `winhttp.dll`，加白名单即可。
联机：大部分功能需要主机也装本 mod。
编译：.NET SDK；把 `src/HTFMod/HTFMod.csproj` 里的 `GameManaged` 改成你的游戏 `Managed` 目录；运行 `build.ps1`，产物 `build/HTFMod.dll` 丢进 `BepInEx/plugins/`。
许可证：AGPL 3.0。仅供朋友联机娱乐，后果自负。

**繁體中文**
安裝：關遊戲，解壓 Release，雙擊 `install.bat`，Steam 啟動遊戲，按 INSERT 開選單。防毒可能誤報 `winhttp.dll`，加白名單即可。
連線：大部分功能需要主機也裝本 mod。
編譯：.NET SDK；把 `src/HTFMod/HTFMod.csproj` 裡的 `GameManaged` 改成你的遊戲 `Managed` 目錄；執行 `build.ps1`，產物 `build/HTFMod.dll` 丟進 `BepInEx/plugins/`。
授權條款：AGPL 3.0。僅供朋友連線娛樂，後果自負。
