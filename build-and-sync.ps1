param (
    [string]$BuildType = "release",
    [switch]$BumpPatch,
    [switch]$BumpMinor,
    [switch]$BumpMajor
)

$ErrorActionPreference = "Stop"

# Version properties path
$VersionFile = Join-Path $PSScriptRoot "version.properties"
$Major = 1
$Minor = 0
$Patch = 1
$BuildNumber = 2

if (Test-Path $VersionFile) {
    Get-Content $VersionFile | ForEach-Object {
        if ($_ -match "VERSION_MAJOR=(\d+)") { $Major = [int]$matches[1] }
        if ($_ -match "VERSION_MINOR=(\d+)") { $Minor = [int]$matches[1] }
        if ($_ -match "VERSION_PATCH=(\d+)") { $Patch = [int]$matches[1] }
        if ($_ -match "BUILD_NUMBER=(\d+)") { $BuildNumber = [int]$matches[1] }
    }
}

# Increment build number
$BuildNumber++

if ($BumpMajor) {
    $Major++
    $Minor = 0
    $Patch = 0
} elseif ($BumpMinor) {
    $Minor++
    $Patch = 0
} elseif ($BumpPatch) {
    $Patch++
}

# Save updated version.properties
@"
VERSION_MAJOR=$Major
VERSION_MINOR=$Minor
VERSION_PATCH=$Patch
BUILD_NUMBER=$BuildNumber
"@ | Set-Content -Path $VersionFile -Encoding UTF8

$VersionName = "$Major.$Minor.$Patch"
$Timestamp = Get-Date -Format "yyyyMMdd-HHmm"
$ArtifactZipName = "open-img-v${VersionName}-build${BuildNumber}-${BuildType}-${Timestamp}.zip"
$ArtifactExeName = "open-img-v${VersionName}-build${BuildNumber}.exe"

$DrivePath = "C:\Users\matth\Desktop\vibe-projects\open-img\builds\open-img-Builds"
if (-not (Test-Path $DrivePath)) {
    New-Item -ItemType Directory -Path $DrivePath -Force | Out-Null
}

Write-Host "Building Open Image Native Windows App v$VersionName (Build $BuildNumber, $BuildType)..." -ForegroundColor Cyan

# Publish .NET 9 WPF native application
dotnet publish -c Release -r win-x64 --self-contained false -p:Version=$VersionName -p:FileVersion=$VersionName

if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

$PublishPath = Join-Path $PSScriptRoot "bin\Release\net9.0-windows\win-x64\publish"
if (-not (Test-Path $PublishPath)) {
    Write-Error "Publish output directory was not found at $PublishPath!"
    exit 1
}

$ExeSource = Join-Path $PublishPath "open-img.exe"
if (-not (Test-Path $ExeSource)) {
    Write-Error "Published executable not found at $ExeSource!"
    exit 1
}

# 1. Copy standalone .exe directly for immediate double-click execution
$DestinationExe = Join-Path $DrivePath $ArtifactExeName
Copy-Item $ExeSource $DestinationExe -Force

# 2. Package complete publish payload to standard zip artifact
$DestinationZip = Join-Path $DrivePath $ArtifactZipName
Write-Host "Packaging native Windows build to $DestinationZip..." -ForegroundColor Cyan
Compress-Archive -Path "$PublishPath\*" -DestinationPath $DestinationZip -Force

if (-not (Test-Path $DestinationZip)) {
    Write-Error "Artifact creation failed at $DestinationZip"
    exit 1
}

$FileSize = (Get-Item $DestinationZip).Length / 1MB
$FileSizeFormatted = [math]::Round($FileSize, 2)

# Update build-history.md
$HistoryFile = Join-Path $PSScriptRoot "build-history.md"
$CommitHash = "uncommitted"
try {
    $CommitHash = (git rev-parse --short HEAD) 2>$null
} catch {}

$LogEntry = "| $ArtifactZipName | ${FileSizeFormatted} MB | $(Get-Date -Format 'yyyy-MM-dd HH:mm') | v$VersionName | $BuildNumber | $BuildType | $CommitHash |"

if (-not (Test-Path $HistoryFile)) {
    @"
# Open Image Build History

| Artifact | Size | Date | Version | Build | Type | Commit |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
$LogEntry
"@ | Set-Content -Path $HistoryFile -Encoding UTF8
} else {
    Add-Content -Path $HistoryFile -Value $LogEntry -Encoding UTF8
}

# Also copy build-history.md to builds directory
Copy-Item $HistoryFile -Destination (Join-Path $DrivePath "build-history.md") -Force

Write-Host "Successfully built and packaged Open Image Native Windows App!" -ForegroundColor Green
Write-Host "Direct Windows Executable: $DestinationExe" -ForegroundColor Green
Write-Host "Packaged Artifact: $DestinationZip ($FileSizeFormatted MB)" -ForegroundColor Green
