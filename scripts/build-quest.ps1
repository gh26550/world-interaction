param([string]$Unity = 'C:\Program Files\Unity\Hub\Editor\6000.2.7f2\Editor\Unity.exe')
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'UnityProject'
$resultDir = Join-Path $repoRoot 'results'
New-Item -ItemType Directory -Force -Path $resultDir | Out-Null
$log = Join-Path $resultDir 'quest-build.log'
$process = Start-Process -FilePath $Unity -ArgumentList @('-batchmode','-projectPath',('"' + $project + '"'),'-executeMethod','WorldInteraction.Editor.ProjectSetup.BuildQuest','-quit','-logFile',('"' + $log + '"')) -Wait -PassThru -WindowStyle Hidden
if ($process.ExitCode -ne 0) { throw "Build failed. See $log" }
Get-Item -LiteralPath (Join-Path $project 'Builds/Android/WorldInteraction.apk')
