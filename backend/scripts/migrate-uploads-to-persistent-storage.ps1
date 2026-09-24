<#
.SYNOPSIS
    Copies existing uploaded photos out of the app's wwwroot into the new persistent storage
    root (Storage:RootPath) so they survive future deployments.

.DESCRIPTION
    Non-destructive: this ONLY COPIES files. It never deletes or modifies the originals under
    wwwroot/uploads. Safe to re-run - existing destination files are overwritten with an
    identical copy, nothing is skipped or lost.

    Run this once per environment (local dev machine, then again on the production server)
    after deploying the storage-separation change, so already-uploaded trip/room/vehicle/
    highlight/organizer photos become visible through the new persistent-storage path instead
    of only existing in the (now git-untracked) wwwroot/uploads copy.

.PARAMETER SourceRoot
    Where the existing uploads currently live. Defaults to this repo's wwwroot/uploads.

.PARAMETER DestinationRoot
    The persistent storage's Uploads folder. Defaults to D:\GhumoOdishaData\Uploads - match
    whatever Storage:RootPath\Uploads is configured to on the machine you're running this on.
#>
param(
    [string]$SourceRoot = (Join-Path $PSScriptRoot "..\GhumoOdisha.Api\wwwroot\uploads"),
    [string]$DestinationRoot = "D:\GhumoOdishaData\Uploads"
)

if (-not (Test-Path $SourceRoot)) {
    Write-Host "No existing uploads found at $SourceRoot - nothing to migrate."
    exit 0
}

New-Item -ItemType Directory -Force -Path $DestinationRoot | Out-Null
$sourceFull = (Resolve-Path $SourceRoot).Path

$copied = 0
Get-ChildItem -Path $SourceRoot -Recurse -File | ForEach-Object {
    $relative = $_.FullName.Substring($sourceFull.Length).TrimStart('\')
    if ($relative -eq '.gitkeep') { return }

    $destPath = Join-Path $DestinationRoot $relative
    New-Item -ItemType Directory -Force -Path (Split-Path $destPath) | Out-Null
    Copy-Item -Path $_.FullName -Destination $destPath -Force
    $copied++
    Write-Host "Copied: $relative"
}

Write-Host ""
Write-Host "Done. $copied file(s) copied to $DestinationRoot."
Write-Host "Originals were left in place at $SourceRoot untouched."
Write-Host "Verify the app serves images correctly from the new location before deleting that folder yourself."
