$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$output = Join-Path $PSScriptRoot 'RegressionTests.exe'
$nativeDir = Join-Path $PSScriptRoot 'third-party'
[void][IO.Directory]::CreateDirectory($nativeDir)
Copy-Item -LiteralPath (Join-Path $project 'third-party/libzstd.dll') -Destination $nativeDir -Force
$sources = @(Get-ChildItem -LiteralPath $project -Filter '*.cs' -File | Select-Object -ExpandProperty FullName)
$sources += @(Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs' -File | Select-Object -ExpandProperty FullName)
& $compiler -nologo -codepage:65001 -platform:x64 "-out:$output" -main:Fm24Editor.RegressionTests -r:System.Windows.Forms.dll -r:System.Drawing.dll -r:System.Core.dll "-resource:$(Join-Path $project 'official-names.tsv'),FM27E0.official-names.tsv" "-resource:$(Join-Path $project 'name-coverage.txt'),FM27E0.name-coverage.txt" "-resource:$(Join-Path $project 'name-data/name-bank.gz'),FM27E0.name-bank.gz" $sources
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& $output
exit $LASTEXITCODE
