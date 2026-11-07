param(
    [string]$Runtime = 'win-x64',
    [string]$Framework = 'net10.0',
    [string]$Tpk,
    [string]$Dataset,
    [int]$Repeats = 5
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$project = Join-Path $repo 'tests/UnityAsset.NET.AotSmoke'
foreach ($mode in @('jit', 'nodedata', 'aot')) {
    $output = Join-Path $repo "artifacts/benchmarks/$mode"
    $fallback = if ($mode -eq 'jit') { 'false' } else { 'true' }
    $native = if ($mode -eq 'aot') { 'true' } else { 'false' }
    & dotnet publish $project -c Release -r $Runtime -m:1 "-p:UnityAssetTargetFramework=$Framework" "-p:UnityAssetNodeData=$fallback" "-p:PublishAot=$native" -o $output
    if ($LASTEXITCODE -ne 0) { throw "Publish failed: $mode" }
    $runArgs = @('--benchmark')
    if ($Tpk) { $runArgs += @('--tpk', $Tpk) }
    if ($Dataset) { $runArgs += @('--dataset', $Dataset) }
    $exe = Join-Path $output ('UnityAsset.NET.AotSmoke' + $(if ($Runtime.StartsWith('win-')) { '.exe' } else { '' }))
    $results = for ($i = 0; $i -lt $Repeats; $i++) {
        $result = & $exe @runArgs
        if ($LASTEXITCODE -ne 0) { throw "Smoke/benchmark failed: $mode" }
        $result
    }
    $results | Set-Content (Join-Path $output 'results.txt')
    $results
}
