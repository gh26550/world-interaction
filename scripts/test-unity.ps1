param(
    [string]$Unity = 'C:\Program Files\Unity\Hub\Editor\6000.2.7f2\Editor\Unity.exe',
    [ValidateSet('EditMode','PlayMode')][string]$Mode = 'EditMode'
)
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'UnityProject'
$resultDir = Join-Path $repoRoot 'results'
New-Item -ItemType Directory -Force -Path $resultDir | Out-Null
$resultFile = Join-Path $resultDir ($Mode.ToLowerInvariant() + '.xml')
$log = Join-Path $resultDir ($Mode.ToLowerInvariant() + '.log')
$argsList = @('-batchmode','-projectPath',('"' + $project + '"'),'-runTests','-testPlatform',$Mode,'-testResults',('"' + $resultFile + '"'),'-logFile',('"' + $log + '"'))
if ($Mode -eq 'EditMode') { $argsList += '-nographics' }
$process = Start-Process -FilePath $Unity -ArgumentList $argsList -Wait -PassThru -WindowStyle Hidden
if ($process.ExitCode -ne 0) { throw "Unity failed. See $log" }
[xml]$report = Get-Content -LiteralPath $resultFile
$report.'test-run' | Select-Object total,passed,failed,result
if ($report.'test-run'.result -ne 'Passed') { throw "Tests failed. See $resultFile" }
