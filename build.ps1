# Build script: compile mod DLL + injector, output to build/
$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

Write-Host "[1/3] Building HTFMod.dll ..." -ForegroundColor Cyan
dotnet publish "$root\src\HTFMod\HTFMod.csproj" -c Release -o "$root\build" --nologo
if ($LASTEXITCODE -ne 0) { throw "HTFMod build failed" }

Write-Host "[2/3] Building HTFInjector ..." -ForegroundColor Cyan
dotnet publish "$root\src\Injector\Injector.csproj" -c Release -o "$root\build\injector" --nologo
if ($LASTEXITCODE -ne 0) { throw "HTFInjector build failed" }

Copy-Item "$root\build\injector\HTFInjector.exe" "$root\build\" -Force
Copy-Item "$root\build\injector\HTFInjector.dll" "$root\build\" -Force
Copy-Item "$root\build\injector\HTFInjector.runtimeconfig.json" "$root\build\" -Force

Write-Host "[3/3] Done." -ForegroundColor Green
Get-ChildItem "$root\build" | Select-Object Name, Length | Format-Table -AutoSize
