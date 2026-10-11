<#
.SYNOPSIS
    QuickTranslate AI'yi derler, tek dosyalık bir sürüm üretir ve isteğe bağlı olarak
    kurulum paketini oluşturur.

.EXAMPLE
    .\build.ps1
    Yayın sürümünü publish\ klasörüne üretir.

.EXAMPLE
    .\build.ps1 -Installer
    Ayrıca Inno Setup ile installer\Output\QuickTranslateAI-Setup.exe üretir.

.EXAMPLE
    .\build.ps1 -FrameworkDependent
    Daha küçük (~35 MB) bir çıktı üretir; hedef bilgisayarda
    ".NET 9 Desktop Runtime" kurulu olmalıdır.
#>
[CmdletBinding()]
param(
    [switch]$Installer,

    # Varsayılan çıktı kendi kendine yeterlidir: hedef bilgisayarda .NET aranmaz.
    [switch]$FrameworkDependent,

    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$project = Join-Path $root 'src\QuickTranslateAI\QuickTranslateAI.csproj'
$publish = Join-Path $root 'publish'

function Find-Dotnet {
    $cmd = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }

    $fallback = Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'
    if (Test-Path $fallback) { return $fallback }

    throw '.NET SDK bulunamadi. Kurulum: winget install Microsoft.DotNet.SDK.9'
}

$dotnet = Find-Dotnet
Write-Host "dotnet: $dotnet" -ForegroundColor DarkGray

if (Test-Path $publish) {
    Remove-Item $publish -Recurse -Force
}

# Varsayilan: kendi kendine yeten tek dosya. Hedef bilgisayarda .NET aranmaz,
# karsiliginda cikti buyur (~150 MB). -FrameworkDependent ile ~35 MB'a iner.
# $args otomatik bir degisken oldugu icin kendi adimizi kullaniyoruz.
$publishArgs = @(
    'publish', $project,
    '-c', $Configuration,
    '-r', 'win-x64',
    '-o', $publish,
    '--nologo',
    '--self-contained', $(if ($FrameworkDependent) { 'false' } else { 'true' }),
    '-p:PublishSingleFile=true',
    '-p:IncludeNativeLibrariesForSelfExtract=true',
    '-p:DebugType=none'
)

Write-Host 'Derleniyor...' -ForegroundColor Cyan
& $dotnet @publishArgs
if ($LASTEXITCODE -ne 0) { throw "Derleme basarisiz (exit $LASTEXITCODE)." }

$exe = Join-Path $publish 'QuickTranslateAI.exe'
if (-not (Test-Path $exe)) { throw "Beklenen cikti olusmadi: $exe" }

$sizeMb = [math]::Round((Get-Item $exe).Length / 1MB, 1)
Write-Host "Hazir: $exe ($sizeMb MB)" -ForegroundColor Green

if (-not $Installer) { return }

# winget, Inno Setup'i makineye de kullanici profiline de kurabiliyor.
$iscc = @(
    (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
    (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe'),
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe')
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $iscc) {
    throw 'Inno Setup 6 bulunamadi. Kurulum: winget install JRSoftware.InnoSetup'
}

Write-Host 'Kurulum paketi olusturuluyor...' -ForegroundColor Cyan
& $iscc (Join-Path $root 'installer\QuickTranslateAI.iss')
if ($LASTEXITCODE -ne 0) { throw "Inno Setup basarisiz (exit $LASTEXITCODE)." }

Write-Host "Hazir: $(Join-Path $root 'installer\Output\QuickTranslateAI-Setup.exe')" -ForegroundColor Green
