param([string]$Unity = 'C:/Program Files/Unity/Hub/Editor/6000.3.11f1/Editor/Unity.exe', [switch]$Capture, [switch]$Pointer, [switch]$Api, [switch]$Login, [switch]$Networking)
$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$validationRoot = Join-Path $projectRoot 'Temp/LobbyValidation'
New-Item -ItemType Directory -Force "$validationRoot/Assets/Editor", "$validationRoot/Packages", "$validationRoot/ProjectSettings" | Out-Null
Copy-Item "$projectRoot/Assets/*" "$validationRoot/Assets" -Recurse -Force
Copy-Item "$projectRoot/ProjectSettings/*" "$validationRoot/ProjectSettings" -Force
Copy-Item "$PSScriptRoot/LobbyChecks.cs" "$validationRoot/Assets/Editor/LobbyChecks.cs" -Force
Copy-Item "$PSScriptRoot/LobbyPointerChecks.cs" "$validationRoot/Assets/Editor/LobbyPointerChecks.cs" -Force
Copy-Item "$PSScriptRoot/LobbyApiChecks.cs" "$validationRoot/Assets/Editor/LobbyApiChecks.cs" -Force
Copy-Item "$PSScriptRoot/LoginChecks.cs" "$validationRoot/Assets/Editor/LoginChecks.cs" -Force
Copy-Item "$PSScriptRoot/NetworkingChecks.cs" "$validationRoot/Assets/Editor/NetworkingChecks.cs" -Force
Copy-Item "$PSScriptRoot/AuthTestServer.cs" "$validationRoot/Assets/Editor/AuthTestServer.cs" -Force
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
$method = if ($Networking) { "NetworkingChecks.Run" } elseif ($Login) { "LoginChecks.Run" } elseif ($Api) { "LobbyApiChecks.Run" } elseif ($Pointer) { "LobbyPointerChecks.Run" } else { "LobbyChecks.Run" }
$arguments = '-batchmode {1} -projectPath "{0}" -executeMethod {2} -logFile "{0}/validation.log"' -f $validationRoot, $graphics, $method
$process = Start-Process -FilePath $Unity -ArgumentList $arguments -WindowStyle Hidden -PassThru
Write-Output "Validation PID: $($process.Id)"
Write-Output "Log and results: $validationRoot"
