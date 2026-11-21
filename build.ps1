# Builds "FM27E0-CAgrowth v<version>.exe" with the .NET Framework compiler that ships with Windows (no SDK needed).
# The version comes from MainForm.Version in FmEditor.cs.
# This is a modified build of FM27€0 (Chinese UI, bilingual search, CA growth and person editing). ADDITIONAL-TERMS.txt forbids passing a modified
# version off as the original, so the file name and the window title both say it is modified.
$dir = [IO.Path]::GetFullPath((Split-Path -Parent $MyInvocation.MyCommand.Path))
$src = Join-Path $dir "FmEditor.cs"
$version = [regex]::Match((Get-Content -Raw -Encoding UTF8 $src), 'Version = "([^"]+)"').Groups[1].Value
$csc = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"
# built under a plain name first: the compiler stores it inside the exe as the original file name
$tmpDir = [IO.Path]::GetFullPath((Join-Path $dir "build.tmp"))
if (-not $tmpDir.StartsWith($dir.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Build output must stay inside the source directory.' }
New-Item -ItemType Directory -Force -Path $tmpDir | Out-Null
$tmp = Join-Path $tmpDir "FM27E0.exe"
# the Chinese UI text lives in the same file, so pin the compiler to UTF-8 instead of the machine's ANSI code page
$cscArgs = @('-codepage:65001', '-nologo', '-target:winexe', '-platform:x64', '-optimize', "-out:$tmp",
    "-win32icon:$(Join-Path $dir 'app.ico')", "-resource:$(Join-Path $dir 'logo.png'),FM27E0.logo.png",
    '-r:System.Windows.Forms.dll', '-r:System.Drawing.dll', '-r:System.Core.dll',
    "-resource:$(Join-Path $dir 'official-names.tsv'),FM27E0.official-names.tsv",
    "-resource:$(Join-Path $dir 'name-coverage.txt'),FM27E0.name-coverage.txt")
$cscArgs += "-resource:$(Join-Path $dir 'name-data/name-bank.gz'),FM27E0.name-bank.gz"
$sources = Get-ChildItem -LiteralPath $dir -Filter '*.cs' -File | Select-Object -ExpandProperty FullName
& $csc $cscArgs $sources
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
# clear the PE header build timestamp so the exe does not show when it was built
$bytes = [IO.File]::ReadAllBytes($tmp)
$pe = [BitConverter]::ToInt32($bytes, 0x3C)
[Array]::Clear($bytes, $pe + 8, 4)
[IO.File]::WriteAllBytes($tmp, $bytes)
$name = "FM27E0-CAgrowth v$version.exe"
$out = Join-Path $dir $name
if (Test-Path -LiteralPath $out) { Remove-Item -LiteralPath $out -Force }
Move-Item -LiteralPath $tmp -Destination $out -Force
try { [IO.Directory]::Delete($tmpDir, $false) } catch { Write-Warning 'The build folder contains other files and was kept.' }
Write-Output "Built $out"
