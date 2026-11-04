<#
.SYNOPSIS
  Erzeugt das Testzertifikat und baut daraus das signierte MSIX-Testpaket der Windows-App (Issue #40).

.DESCRIPTION
  1. Legt (falls nötig) ein selbst gemachtes Codesignatur-Zertifikat "CN=Duofy Test" im Zertifikatspeicher des
     Benutzers an. Der private Schlüssel bleibt dort und ist nicht exportierbar.
  2. Exportiert nur den öffentlichen Teil als Mobile/Duofy-Test.cer.
  3. Baut das Paket mit dotnet publish und signiert es mit diesem Zertifikat.
  Es entsteht nie eine Datei mit dem privaten Schlüssel und es kommt kein Passwort vor.

.PARAMETER CertOnly
  Nur Zertifikat anlegen und .cer exportieren, nichts bauen.

.PARAMETER Renew
  Ein neues Zertifikat erzeugen, auch wenn schon ein gültiges da ist (Erneuerung, siehe docs/app-installieren.md).
#>
[CmdletBinding()]
param(
    [switch]$CertOnly,
    [switch]$Renew
)

$ErrorActionPreference = 'Stop'
$subject = 'CN=Duofy Test'
$repo = Split-Path -Parent $PSScriptRoot
$cerPath = Join-Path $PSScriptRoot 'Duofy-Test.cer'

$cert = $null
if (-not $Renew) {
    $cert = Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert |
        Where-Object { $_.Subject -eq $subject -and $_.NotAfter -gt (Get-Date).AddDays(7) } |
        Sort-Object NotAfter -Descending | Select-Object -First 1
}
if (-not $cert) {
    Write-Host "Erzeuge Testzertifikat $subject (ein Jahr gültig)"
    $cert = New-SelfSignedCertificate -Type Custom -Subject $subject -FriendlyName 'Duofy Test' `
        -KeyUsage DigitalSignature -KeyAlgorithm RSA -KeyLength 2048 -HashAlgorithm SHA256 `
        -KeyExportPolicy NonExportable -CertStoreLocation Cert:\CurrentUser\My -NotAfter (Get-Date).AddYears(1) `
        -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3', '2.5.29.19={text}')
}
Export-Certificate -Cert $cert -FilePath $cerPath | Out-Null
Write-Host "Fingerabdruck: $($cert.Thumbprint)"
Write-Host "Öffentliches Zertifikat: $cerPath"

if ($CertOnly) { return }

# Die Test-Identität steht nur während des Baus im Manifest. Im Repository bleiben die ERSETZEN-Platzhalter des
# Stores (ein Test prüft das); danach wird die Datei so wiederhergestellt, wie sie war.
$manifest = Join-Path $repo 'Mobile/Duofy/Platforms/Windows/Package.appxmanifest'
$original = [System.IO.File]::ReadAllBytes($manifest)
$text = [System.Text.Encoding]::UTF8.GetString($original)
$pattern = '<Identity Name="[^"]*" Publisher="[^"]*"'
if ($text -notmatch $pattern) { throw "Im Manifest wurde kein Identity-Element gefunden: $manifest" }
$patched = [regex]::Replace($text, $pattern, "<Identity Name=`"Duofy.Test`" Publisher=`"$subject`"")

$env:DUOFY_SIGN_THUMBPRINT = $cert.Thumbprint
Push-Location $repo
try {
    [System.IO.File]::WriteAllBytes($manifest, [System.Text.Encoding]::UTF8.GetBytes($patched))
    dotnet publish Mobile/Duofy -f net10.0-windows10.0.19041.0 -c Release `
        -p:DuofyStorePackage=true -p:RuntimeIdentifierOverride=win-x64
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish endete mit Code $LASTEXITCODE" }
}
finally {
    [System.IO.File]::WriteAllBytes($manifest, $original)
    Pop-Location
    Remove-Item Env:\DUOFY_SIGN_THUMBPRINT -ErrorAction SilentlyContinue
}
Get-ChildItem (Join-Path $repo 'Mobile/Duofy/AppPackages') -Recurse -Filter 'Duofy_*.msix' | ForEach-Object { Write-Host "Paket: $($_.FullName)" }
