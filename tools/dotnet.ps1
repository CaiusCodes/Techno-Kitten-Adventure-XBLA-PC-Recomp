# Invoke the project-local SDK with development caches inside PC PORT.
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$env:DOTNET_ROOT = Join-Path $projectRoot '.tools/dotnet'
$env:DOTNET_CLI_HOME = Join-Path $projectRoot '.tools/cli-home'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_NOLOGO = '1'
$env:NUGET_PACKAGES = Join-Path $projectRoot '.tools/nuget/packages'
$env:NUGET_HTTP_CACHE_PATH = Join-Path $projectRoot '.tools/nuget/http-cache'
$env:NUGET_PLUGINS_CACHE_PATH = Join-Path $projectRoot '.tools/nuget/plugins-cache'
$sdk = Join-Path $env:DOTNET_ROOT 'dotnet.exe'
if (-not (Test-Path -LiteralPath $sdk)) { throw 'Local SDK missing. See docs/BUILD-V1.md.' }
& $sdk @args
exit $LASTEXITCODE
