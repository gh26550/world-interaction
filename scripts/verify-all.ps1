param([string]$Unity = 'C:\Program Files\Unity\Hub\Editor\6000.2.7f2\Editor\Unity.exe')
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'test-unity.ps1') -Unity $Unity -Mode EditMode
& (Join-Path $PSScriptRoot 'test-unity.ps1') -Unity $Unity -Mode PlayMode
& (Join-Path $PSScriptRoot 'build-quest.ps1') -Unity $Unity
Write-Output 'FINAL_UNITY_VALIDATION_PASSED'
