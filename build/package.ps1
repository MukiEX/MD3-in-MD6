# Makes the two release zips from a built MD3inMD6.dll:
#   dist/MD3-in-MD6-<ver>.zip                     plugin only (BepInEx already installed)
#   dist/MD3-in-MD6-<ver>-with-BepInEx-x64.zip    BepInEx 5 x64 + plugin, extract into the game folder
#
#   pwsh build/package.ps1 -Dll <MD3inMD6.dll> -BepInExZip <BepInEx_win_x64_5.4.23.2.zip>
param(
  [Parameter(Mandatory)] [string]$Dll,
  [string]$BepInExZip,
  [string]$Version
)
$ErrorActionPreference = "Stop"
$root = Resolve-Path "$PSScriptRoot/.."
if (-not $Version) {
  $Version = ([regex]::Match((Get-Content "$root/src/MD3inMD6/MD3inMD6.cs" -Raw), 'Version = "([^"]+)"')).Groups[1].Value
}
$dist = "$root/dist"; $stage = "$root/obj/stage"
Remove-Item -Recurse -Force $stage -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force "$stage/plugin/BepInEx/plugins/MD3-in-MD6", $dist | Out-Null
Copy-Item $Dll "$stage/plugin/BepInEx/plugins/MD3-in-MD6/"
Copy-Item "$root/README.md", "$root/LICENSE" "$stage/plugin/BepInEx/plugins/MD3-in-MD6/" -ErrorAction SilentlyContinue
$plain = "$dist/MD3-in-MD6-$Version.zip"
Remove-Item $plain -ErrorAction SilentlyContinue
Compress-Archive -Path "$stage/plugin/*" -DestinationPath $plain
Write-Host "wrote $plain"
if ($BepInExZip) {
  New-Item -ItemType Directory -Force "$stage/full" | Out-Null
  Expand-Archive $BepInExZip "$stage/full"
  Copy-Item -Recurse "$stage/plugin/*" "$stage/full/" -Force
  $full = "$dist/MD3-in-MD6-$Version-with-BepInEx-x64.zip"
  Remove-Item $full -ErrorAction SilentlyContinue
  Compress-Archive -Path "$stage/full/*" -DestinationPath $full
  Write-Host "wrote $full"
}
