# HTFMod · How to Fish 鱼塘改造器

A cheat menu for How to Fish — BepInEx 6 plugin, press INSERT in game.
给 How to Fish 写的作弊菜单，游戏里按 INSERT 开关菜单。

Works with game version 1.13 (Unity 6000.4.4). Other versions are not guaranteed.
适配游戏版本 1.13，其他版本不保证能跑。

## English

### Features

- Survival: god mode, fullness lock, keep items on death
- Movement: player flight, speed/jump multipliers, third person (clones another
  player's model in multiplayer), FOV multiplier
- Money: add cash/euros, one-shot (fixed 100k damage, works with fists, knife, guns),
  built-in cheats toggle
- Items: search by name and spawn, fish rain, giant multiplier, drip fish
- Combat: no recoil, no spread, infinite ammo, player aimbot, ESP
- Vehicle: boat speed multiplier, boat flight (WASD six directions, bow follows
  your view), island labels (preloads islands ahead of you)
- Fun: island teleport, roulette auto-win (physically forces the ball), landing
  prediction HUD, kill boss, kill/respawn all creatures, unlock all skins

### Install

1. Close the game, extract the release package, double-click `install.bat` —
   the script finds the game folder from your Steam library automatically
2. Antivirus may flag winhttp.dll (injector false positive): restore it and
   whitelist the game folder
3. Launch through Steam (do not double-click the exe). A black console window
   flashing at startup means it loaded
4. Press INSERT in game to open the menu

Uninstall: close the game, double-click `uninstall.bat`, game goes back to vanilla.

Menu language: bottom-right of the menu (Auto / 简体中文 / 繁體中文 / English).

### Multiplayer

Most features need the host to have this mod too (money, spawning and teleports
are decided by the server). Without a modded host, clients only get the local
combat features.

### Build from source

You need the .NET SDK (anything that can target netstandard2.1) and the Steam
version of the game.

1. Edit `GameManaged` in `src/HTFMod/HTFMod.csproj` to point at your own
   `How to Fish_Data\Managed` folder
2. Run `./build.ps1`, or just the mod: `dotnet publish src/HTFMod/HTFMod.csproj -c Release -o build`
3. Drop `build/HTFMod.dll` into the game's `BepInEx/plugins/` folder and restart

The release package is just the BepInEx runtime plus the compiled dll, bundled
so friends can install with one click.

## 简体中文

### 功能

- 生存：上帝模式、锁满饱食度、死亡不掉落
- 移动：玩家飞行、移速/跳跃倍率、第三人称（联机时克隆其他玩家的模型）、FOV 倍率
- 金钱：加现金/欧元、秒杀（固定十万伤害，拳刀枪全覆盖）、游戏内置作弊开关
- 物品：按名字搜物品直接刷、鱼雨、巨型倍率、闪亮鱼
- 战斗：无后座、无扩散、无限子弹、自瞄玩家、透视
- 载具：船速倍率、载具飞行（WASD 六向，船头随视线）、岛屿标签（自动预加载前方岛屿）
- 玩乐：传送岛屿、轮盘必中（直接干预铁球物理）、落点预测 HUD、秒杀 Boss、清空/复活生物、解锁全部皮肤

### 安装

1. 关游戏，解压发布包，双击 `install.bat`，脚本会自动找 Steam 库里的游戏目录
2. 杀毒软件可能报 winhttp.dll（注入器误报），恢复文件并把游戏目录加白名单
3. 从 Steam 启动游戏（别直接双击 exe），启动时闪过一个黑色控制台窗口 = 加载成功
4. 游戏里按 INSERT 打开菜单

卸载：关游戏后双击 `uninstall.bat`，游戏恢复原版。

菜单语言在菜单右下角切换（自动/简体中文/繁體中文/English）。

### 联机

大部分功能要主机也装了这个 mod 才能用（钱、刷东西、传送这些是服务器说了算的）。
主机没装的话，客户端只能用战斗页那几个本地功能。

### 自己编译

需要 .NET SDK（能编 netstandard2.1 就行）和 Steam 版游戏。

1. 改 `src/HTFMod/HTFMod.csproj` 里的 `GameManaged` 路径，指向你自己的
   `How to Fish_Data\Managed` 目录
2. `./build.ps1`，或者只编 mod：`dotnet publish src/HTFMod/HTFMod.csproj -c Release -o build`
3. 拿到 `build/HTFMod.dll`，丢进游戏目录的 `BepInEx/plugins/`，重启游戏

发布包就是 BepInEx 运行时加编好的 dll 打在一起，方便不会配置的朋友一键装。

## 繁體中文

### 功能

- 生存：上帝模式、鎖滿飽食度、死亡不掉落
- 移動：玩家飛行、移速/跳躍倍率、第三人稱（連線時克隆其他玩家的模型）、FOV 倍率
- 金錢：加現金/歐元、秒殺（固定十萬傷害，拳刀槍全覆蓋）、遊戲內置作弊開關
- 物品：按名字搜尋物品直接刷、魚雨、巨型倍率、閃亮魚
- 戰鬥：無後座、無擴散、無限子彈、自瞄玩家、透視
- 載具：船速倍率、載具飛行（WASD 六向，船頭隨視線）、島嶼標籤（自動預載前方島嶼）
- 玩樂：傳送島嶼、輪盤必中（直接干預鐵球物理）、落點預測 HUD、秒殺 Boss、清空/復活生物、解鎖全部皮膚

### 安裝

1. 關遊戲，解壓發布包，雙擊 `install.bat`，腳本會自動找 Steam 庫裡的遊戲目錄
2. 防毒軟體可能報 winhttp.dll（注入器誤報），還原檔案並把遊戲目錄加白名單
3. 從 Steam 啟動遊戲（別直接雙擊 exe），啟動時閃過一個黑色主控台視窗 = 載入成功
4. 遊戲裡按 INSERT 打開選單

卸載：關遊戲後雙擊 `uninstall.bat`，遊戲恢復原版。

選單語言在選單右下角切換（自動/簡体中文/繁體中文/English）。

### 連線

大部分功能要主機也裝了這個 mod 才能用（錢、刷東西、傳送這些是伺服器說了算的）。
主機沒裝的話，用戶端只能用戰鬥頁那幾個本地功能。

### 自己編譯

需要 .NET SDK（能編 netstandard2.1 就行）和 Steam 版遊戲。

1. 改 `src/HTFMod/HTFMod.csproj` 裡的 `GameManaged` 路徑，指向你自己的
   `How to Fish_Data\Managed` 目錄
2. `./build.ps1`，或者只編 mod：`dotnet publish src/HTFMod/HTFMod.csproj -c Release -o build`
3. 拿到 `build/HTFMod.dll`，丟進遊戲目錄的 `BepInEx/plugins/`，重啟遊戲

發布包就是 BepInEx 執行時加編好的 dll 打在一起，方便不會設定的朋友一鍵裝。
