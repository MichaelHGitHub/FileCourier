param(
    [Parameter(Mandatory=$true)]
    [string]$SourceDir,
    
    [Parameter(Mandatory=$true)]
    [string]$DestDir
)

# Ensure SourceDir ends with a backslash
if (-not $SourceDir.EndsWith("\")) { $SourceDir += "\" }

$ZipFileName = "FileCourier.zip"
$ZipFilePath = Join-Path $DestDir $ZipFileName
$TempStageDir = Join-Path $env:TEMP "FileCourier_Stage_$([Guid]::NewGuid().ToString().Substring(0,8))"

Write-Host "--- Deployment & Packaging Script ---"
Write-Host "Source: $SourceDir"
Write-Host "Target: $ZipFilePath"

# 1. Ensure destination folder exists
if (-not (Test-Path $DestDir)) {
    Write-Host "Creating destination folder..."
    New-Item -Path $DestDir -ItemType Directory -Force
}

# 2. Create a temporary staging area
Write-Host "Staging files in temporary directory..."
if (Test-Path $TempStageDir) { Remove-Item $TempStageDir -Recurse -Force }
New-Item -Path $TempStageDir -ItemType Directory -Force
Copy-Item -Path "$SourceDir*" -Destination $TempStageDir -Recurse -Force

# 3. Clean up non-essential files in staging
Write-Host "Removing debug symbols and documentation..."
Get-ChildItem -Path $TempStageDir -Include *.pdb, *.xml -Recurse | Remove-Item -Force

# 4. Create ZIP archive directly in destination
Write-Host "Compressing files into $ZipFileName..."
# We zip the *contents* of the staging directory
Compress-Archive -Path "$TempStageDir\*" -DestinationPath $ZipFilePath -Force

# 5. Clean up staging area
Write-Host "Cleaning up staging area..."
Remove-Item -Path $TempStageDir -Recurse -Force

Write-Host "Packaging complete. Output: $ZipFilePath"
