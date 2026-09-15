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
$Patch = 0
$BuildNumber = 1

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
$ArtifactName = "open-img-v${VersionName}-build${BuildNumber}-${BuildType}-${Timestamp}.zip"

$DrivePath = "C:\Users\matth\Desktop\vibe-projects\open-img\builds\open-img-Builds"
if (-not (Test-Path $DrivePath)) {
    New-Item -ItemType Directory -Path $DrivePath -Force | Out-Null
}

Write-Host "Building Open Image v$VersionName (Build $BuildNumber, $BuildType)..." -ForegroundColor Cyan
npm run build

if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

$DistPath = Join-Path $PSScriptRoot "dist"
if (-not (Test-Path $DistPath)) {
    Write-Error "Build output directory 'dist' was not found!"
    exit 1
}

$DestinationArtifact = Join-Path $DrivePath $ArtifactName
Write-Host "Packaging build artifact to $DestinationArtifact..." -ForegroundColor Cyan
Compress-Archive -Path "$DistPath\*" -DestinationPath $DestinationArtifact -Force

if (-not (Test-Path $DestinationArtifact)) {
    Write-Error "Artifact creation failed at $DestinationArtifact"
    exit 1
}

$FileSize = (Get-Item $DestinationArtifact).Length / 1MB
$FileSizeFormatted = [math]::Round($FileSize, 2)

# Update build-history.md
$HistoryFile = Join-Path $PSScriptRoot "build-history.md"
$CommitHash = "uncommitted"
try {
    $CommitHash = (git rev-parse --short HEAD) 2>$null
} catch {}

$LogEntry = "| $ArtifactName | ${FileSizeFormatted} MB | $(Get-Date -Format 'yyyy-MM-dd HH:mm') | v$VersionName | $BuildNumber | $BuildType | $CommitHash |"

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

Write-Host "Successfully built and packaged Open Image!" -ForegroundColor Green
Write-Host "Artifact: $ArtifactName ($FileSizeFormatted MB)" -ForegroundColor Green
Write-Host "Destination: $DestinationArtifact" -ForegroundColor Green
