# HTFMod 一键安装/卸载脚本
# 用法: 双击 安装mod.bat 或 卸载mod.bat (内部调用本脚本)

param(
    [string]$Mode = "install",
    [string]$GamePath = ""
)

$ErrorActionPreference = "Stop"
$modDir = $PSScriptRoot
if (-not $modDir) { $modDir = Split-Path -Parent $MyInvocation.MyCommand.Path }

function Find-GameDir {
    # 1) 手动指定
    if ($GamePath -and (Test-Path (Join-Path $GamePath "How to Fish.exe"))) {
        return $GamePath
    }
    # 2) 从 Steam libraryfolders.vdf 枚举所有游戏库
    $steamRoots = @(
        "${env:ProgramFiles(x86)}\Steam",
        "$env:ProgramFiles\Steam",
        "C:\Steam", "D:\Steam", "E:\Steam"
    ) | Where-Object { $_ -and (Test-Path $_) }

    $libs = New-Object System.Collections.Generic.List[string]
    foreach ($s in $steamRoots) {
        $libs.Add($s)
        $vdf = Join-Path $s "steamapps\libraryfolders.vdf"
        if (Test-Path $vdf) {
            foreach ($line in Get-Content $vdf -Encoding UTF8) {
                if ($line -match '"path"\s+"([^"]+)"') {
                    $p = $Matches[1] -replace '\\\\', '\'
                    if (Test-Path $p) { $libs.Add($p) }
                }
            }
        }
    }
    # 3) 常见 SteamLibrary 盘根兜底扫描
    foreach ($drv in @("C:", "D:", "E:", "F:", "G:")) {
        $libs.Add("$drv\SteamLibrary")
        $libs.Add("$drv\Steam")
    }
    foreach ($lib in $libs) {
        $p = Join-Path $lib "steamapps\common\How to Fish\How to Fish"
        if (Test-Path (Join-Path $p "How to Fish.exe")) { return $p }
    }
    return $null
}

function Assert-GameClosed {
    if (Get-Process "How to Fish" -ErrorAction SilentlyContinue) {
        Write-Host ""
        Write-Host "[!] 游戏正在运行 - 请先完全关闭游戏,再重新运行本脚本" -ForegroundColor Yellow
        exit 1
    }
}

Write-Host "==============================================" -ForegroundColor Cyan
Write-Host "  HTFMod 鱼塘改造器 - 管理脚本 ($Mode)" -ForegroundColor Cyan
Write-Host "==============================================" -ForegroundColor Cyan

if ($Mode -eq "install") {

    # 校验发布包文件齐全
    $need = @("winhttp.dll", "doorstop_config.ini", ".doorstop_version", "BepInEx")
    foreach ($f in $need) {
        if (-not (Test-Path (Join-Path $modDir $f))) {
            Write-Host "[✗] 发布包缺文件: $f (请使用完整的发布包)" -ForegroundColor Red
            exit 1
        }
    }

    $game = Find-GameDir
    if (-not $game) {
        Write-Host ""
        Write-Host "[?] 没有自动找到游戏目录" -ForegroundColor Yellow
        $GamePath = Read-Host "请粘贴游戏目录路径 (How to Fish.exe 所在文件夹),或直接回车退出"
        if (-not $GamePath -or -not (Test-Path (Join-Path $GamePath "How to Fish.exe"))) {
            Write-Host "[✗] 路径无效,退出" -ForegroundColor Red
            exit 1
        }
        $game = $GamePath
    }
    Write-Host "[✓] 游戏目录: $game"
    Assert-GameClosed

    Copy-Item (Join-Path $modDir "winhttp.dll") $game -Force
    Copy-Item (Join-Path $modDir "doorstop_config.ini") $game -Force
    Copy-Item (Join-Path $modDir ".doorstop_version") $game -Force
    Copy-Item (Join-Path $modDir "BepInEx") $game -Recurse -Force

    $ok = (Test-Path (Join-Path $game "winhttp.dll")) -and
          (Test-Path (Join-Path $game "BepInEx\plugins\HTFMod.dll"))
    if ($ok) {
        Write-Host ""
        Write-Host "[✓] 安装成功!" -ForegroundColor Green
        Write-Host "    通过 Steam 启动游戏,进游戏按 INSERT 打开菜单" -ForegroundColor Green
        Write-Host "    若杀毒软件删除了 winhttp.dll,请恢复并把游戏目录加白名单"
    } else {
        Write-Host "[✗] 安装后校验失败 - 检查杀毒软件是否拦截" -ForegroundColor Red
    }
}
elseif ($Mode -eq "uninstall") {

    $game = Find-GameDir
    if (-not $game) {
        Write-Host ""
        $GamePath = Read-Host "请粘贴游戏目录路径 (How to Fish.exe 所在文件夹),或直接回车退出"
        if (-not $GamePath -or -not (Test-Path (Join-Path $GamePath "How to Fish.exe"))) {
            Write-Host "[✗] 路径无效,退出" -ForegroundColor Red
            exit 1
        }
        $game = $GamePath
    }
    Write-Host "[✓] 游戏目录: $game"
    Assert-GameClosed

    $removed = 0
    foreach ($f in @("winhttp.dll", "doorstop_config.ini", ".doorstop_version")) {
        $p = Join-Path $game $f
        if (Test-Path $p) { Remove-Item $p -Force; $removed++ }
    }
    $bep = Join-Path $game "BepInEx"
    if (Test-Path $bep) { Remove-Item $bep -Recurse -Force; $removed++ }

    if ($removed -gt 0) {
        Write-Host ""
        Write-Host "[✓] 卸载完成 - 游戏已恢复原版" -ForegroundColor Green
    } else {
        Write-Host "[i] 没有发现已安装的 mod" -ForegroundColor Yellow
    }
}
else {
    Write-Host "[✗] 未知模式: $Mode (用 install 或 uninstall)" -ForegroundColor Red
    exit 1
}
