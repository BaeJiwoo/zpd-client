param([string]$Unity = 'C:\Program Files\Unity\Hub\Editor\6000.3.11f1\Editor\Unity.exe', [switch]$Capture)
$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$validationRoot = Join-Path $projectRoot 'Temp/SoloDefenseValidation'
New-Item -ItemType Directory -Force "$validationRoot/Assets", "$validationRoot/Assets/Editor", "$validationRoot/Packages", "$validationRoot/ProjectSettings" | Out-Null
Copy-Item "$projectRoot/Assets/*" "$validationRoot/Assets" -Recurse -Force
Copy-Item "$projectRoot/ProjectSettings/*" "$validationRoot/ProjectSettings" -Force
Copy-Item "$PSScriptRoot/DefenseGameplayChecks.cs" "$validationRoot/Assets/Editor/DefenseGameplayChecks.cs" -Force
$manifest = Get-Content "$projectRoot/Packages/manifest.json" -Raw | ConvertFrom-Json
foreach ($package in Get-ChildItem "$projectRoot/Library/PackageCache" -Directory) {
    $packageJson = Join-Path $package.FullName 'package.json'
    if (Test-Path $packageJson) {
        $info = Get-Content $packageJson -Raw | ConvertFrom-Json
        $manifest.dependencies | Add-Member -MemberType NoteProperty -Name $info.name -Value ('file:' + $package.FullName.Replace('\','/')) -Force
    }
}
$manifest | ConvertTo-Json -Depth 10 | Set-Content "$validationRoot/Packages/manifest.json"
'RUNNING' | Set-Content "$validationRoot/validation-result.txt"
$graphics = if ($Capture) { '-force-d3d11' } else { '-nographics' }
$arguments = '-batchmode {1} -projectPath "{0}" -executeMethod Zpd.Defense.Editor.DefenseGameplayChecks.Run -logFile "{0}/validation.log"' -f $validationRoot, $graphics
$process = Start-Process -FilePath $Unity -ArgumentList $arguments -WindowStyle Hidden -PassThru
Write-Output "Validation PID: $($process.Id)"
Write-Output "Log: $validationRoot/validation.log"
Write-Output "Result: $validationRoot/validation-result.txt"
Write-Output 'The isolated scene is upgraded before testing. Copy it back only after a passing result.'
