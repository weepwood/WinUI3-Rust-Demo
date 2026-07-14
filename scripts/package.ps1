param(
    [Parameter(Mandatory = $false)]
    [ValidateSet("x64", "ARM64")]
    [string]$Architecture = "x64",

    [Parameter(Mandatory = $false)]
    [string]$Version = "0.1.0"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repositoryRoot "src\SystemCenter\SystemCenter.csproj"
$rustManifest = Join-Path $repositoryRoot "src\sysbridge\Cargo.toml"
$artifactsDirectory = Join-Path $repositoryRoot "artifacts"
$publishDirectory = Join-Path $artifactsDirectory "publish-$Architecture"

if ($Architecture -eq "ARM64") {
    $runtimeIdentifier = "win-arm64"
    $rustTarget = "aarch64-pc-windows-msvc"
    $archiveArchitecture = "arm64"
} else {
    $runtimeIdentifier = "win-x64"
    $rustTarget = "x86_64-pc-windows-msvc"
    $archiveArchitecture = "x64"
}

Write-Host "Preparing Rust target $rustTarget"
rustup target add $rustTarget

Write-Host "Building Rust bridge"
cargo build --manifest-path $rustManifest --release --target $rustTarget

if (Test-Path $publishDirectory) {
    Remove-Item $publishDirectory -Recurse -Force
}
New-Item -ItemType Directory -Path $publishDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $artifactsDirectory -Force | Out-Null

Write-Host "Publishing WinUI application for $runtimeIdentifier"
dotnet publish $projectPath `
    --configuration Release `
    --runtime $runtimeIdentifier `
    --self-contained true `
    --output $publishDirectory `
    -p:Platform=$Architecture `
    -p:SkipRustBuild=true `
    -p:Version=$Version

$bridgePath = Join-Path $repositoryRoot "src\sysbridge\target\$rustTarget\release\sysbridge.exe"
if (-not (Test-Path $bridgePath)) {
    throw "Rust bridge was not created at $bridgePath"
}
Copy-Item $bridgePath (Join-Path $publishDirectory "sysbridge.exe") -Force
Copy-Item (Join-Path $repositoryRoot "README.md") $publishDirectory -Force
Copy-Item (Join-Path $repositoryRoot "LICENSE") $publishDirectory -Force

$archiveName = "WinUI3-Rust-System-Center-$Version-$archiveArchitecture.zip"
$archivePath = Join-Path $artifactsDirectory $archiveName
if (Test-Path $archivePath) {
    Remove-Item $archivePath -Force
}

Write-Host "Creating $archiveName"
Compress-Archive -Path (Join-Path $publishDirectory "*") -DestinationPath $archivePath -CompressionLevel Optimal

$hash = (Get-FileHash $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
$checksumPath = "$archivePath.sha256"
"$hash  $archiveName" | Set-Content -Path $checksumPath -Encoding utf8NoBOM

Write-Host "Package: $archivePath"
Write-Host "Checksum: $checksumPath"
