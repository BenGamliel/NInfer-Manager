param(
    [Parameter(Mandatory = $true)]
    [string] $EngineSource,
    [string] $Configuration = 'Release',
    [string] $Version = '1.0.0'
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$sourceProject = Join-Path $projectRoot 'src\NInferManager\NInferManager.csproj'
$buildRoot = Join-Path $projectRoot 'build'
$publishRoot = Join-Path $buildRoot 'publish'
$portableRoot = Join-Path $projectRoot 'dist\Portable\NInfer Manager'
$installedRoot = Join-Path $buildRoot 'installed-payload'
$distRoot = Join-Path $projectRoot 'dist'
$engineSourcePath = (Resolve-Path -LiteralPath $EngineSource).Path

function Assert-ProjectChild([string] $path) {
    $full = [System.IO.Path]::GetFullPath($path)
    $prefix = [System.IO.Path]::GetFullPath($projectRoot).TrimEnd('\') + '\'
    if (-not $full.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to modify a path outside the project: $full"
    }
}

if (-not (Test-Path -LiteralPath (Join-Path $engineSourcePath 'ninfer-serve.exe'))) {
    throw 'EngineSource must point to an extracted official ninfer-windows release.'
}

Assert-ProjectChild $buildRoot
Assert-ProjectChild $portableRoot
Remove-Item -LiteralPath $buildRoot -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $portableRoot -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $publishRoot, $portableRoot, $installedRoot, $distRoot | Out-Null

dotnet publish $sourceProject -c $Configuration -r win-x64 --self-contained true `
    -p:Version=$Version -o $publishRoot
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }

function Copy-Payload([string] $target, [bool] $portable) {
    New-Item -ItemType Directory -Force -Path $target | Out-Null
    Copy-Item -LiteralPath (Join-Path $publishRoot 'NInfer Manager.exe') -Destination $target
    Copy-Item -LiteralPath (Join-Path $projectRoot 'src\NInferManager\Assets\ninfer-manager.png') -Destination (Join-Path $target 'NInfer Manager.png')
    Copy-Item -LiteralPath (Join-Path $projectRoot 'README.md') -Destination (Join-Path $target 'README.md')
    Copy-Item -LiteralPath (Join-Path $projectRoot 'LICENSE') -Destination (Join-Path $target 'LICENSE')
    Copy-Item -LiteralPath (Join-Path $projectRoot 'NOTICE') -Destination (Join-Path $target 'NOTICE')
    Copy-Item -LiteralPath (Join-Path $projectRoot 'THIRD-PARTY-NOTICES.txt') -Destination (Join-Path $target 'THIRD-PARTY-NOTICES.txt')
    Copy-Item -LiteralPath (Join-Path $projectRoot 'docs') -Destination (Join-Path $target 'Docs') -Recurse

    $engineTarget = Join-Path $target 'Engine'
    $licenseTarget = Join-Path $target 'Licenses'
    $modelsTarget = Join-Path $target 'Models'
    New-Item -ItemType Directory -Force -Path $engineTarget, $licenseTarget, $modelsTarget | Out-Null
    $engineFiles = @(
        'ninfer.exe', 'ninfer-serve.exe', 'avcodec-62.dll', 'avformat-62.dll', 'avutil-60.dll',
        'swresample-6.dll', 'swscale-9.dll', 'libcurl.dll', 'z.dll',
        'MSVCP140.dll', 'VCRUNTIME140.dll', 'VCRUNTIME140_1.dll', 'README.txt', 'SHA256SUMS'
    )
    foreach ($file in $engineFiles) {
        $source = Join-Path $engineSourcePath $file
        if (Test-Path -LiteralPath $source) { Copy-Item -LiteralPath $source -Destination $engineTarget }
    }
    $webUiSource = Join-Path $engineSourcePath 'models\webui'
    if (-not (Test-Path -LiteralPath (Join-Path $webUiSource 'index.html'))) {
        throw 'The EngineSource does not contain models\webui\index.html.'
    }
    Copy-Item -LiteralPath $webUiSource -Destination (Join-Path $engineTarget 'webui') -Recurse
    Copy-Item -LiteralPath (Join-Path $projectRoot 'LICENSE') -Destination (Join-Path $licenseTarget 'NInfer-and-Manager-Apache-2.0.txt')
    Copy-Item -LiteralPath (Join-Path $projectRoot 'THIRD-PARTY-NOTICES.txt') -Destination $licenseTarget
    Set-Content -LiteralPath (Join-Path $modelsTarget 'README.txt') -Value @'
Models are intentionally not bundled.
Open NInfer Manager, select Models, and install an official artifact.
'@
    if ($portable) { Set-Content -LiteralPath (Join-Path $target 'portable.mode') -Value 'Portable data mode' }
}

Copy-Payload -target $portableRoot -portable $true
Copy-Payload -target $installedRoot -portable $false

$portableZip = Join-Path $distRoot "NInfer-Manager-Portable-$Version.zip"
Remove-Item -LiteralPath $portableZip -Force -ErrorAction SilentlyContinue
Compress-Archive -LiteralPath $portableRoot -DestinationPath $portableZip -CompressionLevel Optimal

$isccCandidates = @(
    (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe')
)
$iscc = $isccCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $iscc) { throw 'Inno Setup 6 was not found. Install JRSoftware.InnoSetup with winget.' }
& $iscc (Join-Path $projectRoot 'installer\NInferManager.iss') "/DAppVersion=$Version"
if ($LASTEXITCODE -ne 0) { throw 'Inno Setup compilation failed.' }

Get-ChildItem -LiteralPath $distRoot -File -Recurse |
    Where-Object Extension -in '.zip', '.exe' |
    ForEach-Object { "{0} *{1}" -f (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), $_.FullName.Substring($distRoot.Length + 1) } |
    Set-Content -LiteralPath (Join-Path $distRoot 'SHA256SUMS.txt')

Write-Host "Portable: $portableRoot"
Write-Host "Installer: $(Join-Path $distRoot "Installer\NInfer-Manager-Setup-$Version.exe")"
