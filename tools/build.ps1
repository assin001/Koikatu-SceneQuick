param(
    [string]$GameRoot = 'E:\game\GDAkk\Koikatu',
    [string]$OutDir = '',
    [string]$Csc = '',
    [switch]$Deploy
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrEmpty($OutDir)) { $OutDir = Join-Path $root 'out' }
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

if ([string]::IsNullOrEmpty($Csc)) {
    $local = Join-Path $root '_tools\roslyn\tasks\net472\csc.exe'
    $alt = 'C:\Users\muse\Desktop\py\SceneBrowser\_tools\roslyn\tasks\net472\csc.exe'
    if (Test-Path $local) { $Csc = $local }
    elseif (Test-Path $alt) { $Csc = $alt }
    else { throw "找不到 csc.exe，请用 -Csc 指定（Roslyn 编译器）" }
}

$managed = Join-Path $GameRoot 'CharaStudio_Data\Managed'
$core = Join-Path $GameRoot 'BepInEx\core'
$refs = @(
    "$managed\mscorlib.dll",
    "$managed\System.dll",
    "$managed\System.Core.dll",
    "$managed\UnityEngine.dll",
    "$managed\UnityEngine.UI.dll",
    "$managed\Assembly-CSharp.dll",
    "$core\BepInEx.dll",
    "$core\0Harmony.dll"
)
foreach ($r in $refs) { if (-not (Test-Path $r)) { throw "缺少引用: $r" } }

$srcDir = Join-Path $root 'src\KK_SceneQuick'
$sources = Get-ChildItem $srcDir -Filter *.cs | ForEach-Object { $_.FullName }
$out = Join-Path $OutDir 'KK_SceneQuick.dll'

$args = @('/nologo', '/noconfig', '/target:library', '/nostdlib+', '/langversion:11', '/optimize+',
    '/debug:portable', '/define:KK', "/out:$out") + ($refs | ForEach-Object { "/r:$_" }) + $sources

& $Csc @args
if ($LASTEXITCODE -ne 0) { throw "编译失败 ($LASTEXITCODE)" }
Write-Host "OK -> $out"

if ($Deploy) {
    $plugins = Join-Path $GameRoot 'BepInEx\plugins'
    $target = Join-Path $plugins 'KK_SceneQuick.dll'
    if (Test-Path $target) {
        $bak = "$target.bak_" + (Get-Date -Format 'yyyyMMdd_HHmmss')
        Copy-Item $target $bak -Force
        Write-Host "备份 -> $bak"
    }
    Copy-Item $out $target -Force
    foreach ($ext in @('.pdb')) {
        $p = Join-Path $OutDir "KK_SceneQuick$ext"
        if (Test-Path $p) { Copy-Item $p (Join-Path $plugins "KK_SceneQuick$ext") -Force }
    }
    Write-Host "已部署 -> $target"
}
