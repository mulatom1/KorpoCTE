$logDir = "C:\deploy\logs"

New-Item -ItemType Directory -Force -Path $logDir | Out-Null

Start-Transcript `
    -Path "$logDir\deploy.log" `
    -Append

Write-Host "=== START DEPLOY ==="
Write-Host "Date: $(Get-Date)"
Write-Host "User: $([System.Security.Principal.WindowsIdentity]::GetCurrent().Name)"
Write-Host "Working directory: $(Get-Location)"


$ErrorActionPreference = "Stop"

$source = "C:\deploy\staging\tomsoft1"
$target = "C:\inetpub\wwwroot\tomsoft1"
$backupRoot = "C:\deploy\backups"
$ready = Join-Path $source "READY"
$offline = Join-Path $target "app_offline.htm"

# Nie uruchamiaj niczego bez kompletnej paczki
if (!(Test-Path $ready)) { exit 0 }

$version = (Get-Content $ready -Raw).Trim()
$versionFile = Join-Path $source "wwwroot\version.txt"

if ($version -notmatch '^[0-9a-fA-F]{40}$') {
    throw "Invalid deployment version"
}
if (!(Test-Path $versionFile)) {
    throw "Missing version.txt"
}
if ((Get-Content $versionFile -Raw).Trim() -ne $version) {
    throw "Package version mismatch"
}
if (!(Test-Path (Join-Path $source "web.config"))) {
    throw "Missing web.config"
}

New-Item -ItemType Directory -Force $backupRoot | Out-Null
New-Item -ItemType Directory -Force $target | Out-Null

$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$backup = Join-Path $backupRoot $stamp
New-Item -ItemType Directory -Force $backup | Out-Null

# Kopia aktualnej aplikacji
& robocopy $target $backup /E /R:2 /W:2 `
  /XF app_offline.htm | Out-Null

if ($LASTEXITCODE -ge 8) {
    throw "Backup failed"
}

try {
    # Zwolnienie plikow DLL przez IIS
    Set-Content $offline "Application update in progress"
    Start-Sleep -Seconds 10

    # Kopiowanie nowej wersji
    # /E zachowuje pliki istniejace tylko na serwerze
    & robocopy $source $target /E /R:3 /W:3 `
      /XF READY appsettings.json appsettings.*.json app_offline.htm `
      /XD Logs uploads TMP | Out-Null

    if ($LASTEXITCODE -ge 8) {
        throw "Deployment copy failed"
    }
}
catch {
    Write-Error "Deployment failed: $_"

    # Przywrocenie poprzednich plikow
    & robocopy $backup $target /E /R:3 /W:3 `
      /XF app_offline.htm | Out-Null

    throw
}
finally {
    Remove-Item $offline -Force -ErrorAction SilentlyContinue
}

# Potwierdz, ze pliki nowej wersji sa w IIS
$deployedVersion = Join-Path $target "wwwroot\version.txt"

if (!(Test-Path $deployedVersion) -or
    (Get-Content $deployedVersion -Raw).Trim() -ne $version) {
    throw "Version verification failed"
}

# Znacznik READY usuwamy po kopiowaniu
Remove-Item $ready -Force

Write-Host "Deployment files installed: $version"


Write-Host "=== END DEPLOY ==="
Stop-Transcript
