param(
    [string]$ServerRoot = (Join-Path $PSScriptRoot '../../zpd-server'),
    [string]$Protoc = '',
    [string]$ProtobufPackage = (Join-Path $env:USERPROFILE '.nuget/packages/google.protobuf/3.35.0')
)
$ErrorActionPreference = 'Stop'
$clientRoot = Split-Path $PSScriptRoot -Parent
$protoRoot = Join-Path $ServerRoot 'common/proto'
if (!$Protoc) {
    $Protoc = Join-Path $ServerRoot 'out/build/windows-x64/vcpkg_installed/x64-windows/tools/protobuf/protoc.exe'
}
if (!(Test-Path -LiteralPath $Protoc)) { throw 'Build zpd-server first, or supply -Protoc with the protoc executable path.' }
$runtime = Join-Path $ProtobufPackage 'lib/netstandard2.0/Google.Protobuf.dll'
if (!(Test-Path -LiteralPath $runtime)) { throw 'Supply -ProtobufPackage pointing to the extracted Google.Protobuf 3.35.0 NuGet package.' }
$generated = Join-Path $clientRoot 'Assets/Scripts/Networking/Generated'
$plugins = Join-Path $clientRoot 'Assets/Plugins/Google.Protobuf'
New-Item -ItemType Directory -Force -Path $generated, $plugins | Out-Null
$sources = @('matchmaking.proto') | ForEach-Object { Join-Path $protoRoot $_ }
& $Protoc "--proto_path=$protoRoot" "--csharp_out=$generated" @sources
if ($LASTEXITCODE -ne 0) { throw 'Protobuf code generation failed.' }
Copy-Item -LiteralPath $runtime -Destination (Join-Path $plugins 'Google.Protobuf.dll')
Write-Host 'Generated server protocol C# messages and installed Google.Protobuf 3.35.0.'
