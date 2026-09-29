<#
  Builds installer\output\EmpifisJsonSetup-<version>.exe:
  publishes the service (Till profile) and ReceiptTester (single-file x86), then compiles EmpifisSetup.iss.

  .\installer\build-installer.ps1 [-ReceiptTesterRepo C:\ReceiptTester] [-Iscc <path to ISCC.exe>]
#>
param(
    [string]$ReceiptTesterRepo = "C:\ReceiptTester",
    [string]$Iscc
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent

function Invoke-Checked([string]$FilePath, [string[]]$Arguments) {
    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$FilePath failed with exit code $LASTEXITCODE" }
}

$version = ([xml](Get-Content "$root\empifisJsonService2.csproj")).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
if (-not $version) { throw "No <Version> in empifisJsonService2.csproj" }

$serviceDir = Join-Path $root "bin\publish\till"
$testerDir = Join-Path $PSScriptRoot "build\ReceiptTester"
foreach ($dir in $serviceDir, $testerDir) {
    if (Test-Path $dir) { Remove-Item $dir -Recurse -Force }
}

Write-Host "Publishing the service $version..."
Invoke-Checked dotnet @("publish", "$root\empifisJsonService2.csproj", "-p:PublishProfile=Till", "-nologo")

Write-Host "Publishing ReceiptTester from $ReceiptTesterRepo..."
Invoke-Checked dotnet @("publish", "$ReceiptTesterRepo\src\ReceiptTester", "-c", "Release", "-r", "win-x86", "-o", $testerDir, "-nologo")

$manual = Get-ChildItem $serviceDir -Filter "empifisJSON_*.docx" | Select-Object -First 1
if (-not $manual) { throw "No empifisJSON_*.docx in $serviceDir" }

if (-not $Iscc) {
    $Iscc = @("$env:ProgramFiles\Inno Setup 7\ISCC.exe", "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe") |
        Where-Object { Test-Path $_ } | Select-Object -First 1
}
if (-not $Iscc) { throw "Inno Setup (ISCC.exe) not found; pass -Iscc" }

Write-Host "Compiling the installer with $Iscc..."
Invoke-Checked $Iscc @("/Q", "/DAppVersion=$version", "/DServiceDir=$serviceDir", "/DTesterExe=$testerDir\ReceiptTester.exe",
    "/DManualFile=$($manual.Name)", "$PSScriptRoot\EmpifisSetup.iss")

$setup = Join-Path $PSScriptRoot "output\EmpifisJsonSetup-$version.exe"
Write-Host ("Done: {0} ({1:n1} MB)" -f $setup, ((Get-Item $setup).Length / 1MB))
