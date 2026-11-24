#requires -Version 5.1
<#
.SYNOPSIS
Builds, verifies, and packages a new FM27E0-CAgrowth version locally.
.PARAMETER Version
New X.Y.Z version. DryRun alone previews the next patch version.
.PARAMETER DryRun
Checks the repository and shows the plan without writing, building, or deleting files.
.PARAMETER SkipLocalUpdate
Leaves the local editor and its runtime data in place.
.PARAMETER NoGitCommit
Builds and packages a version from an isolated source tree. Skips the release
commit and tag and leaves the maintained checkout at its current version.
#>
[CmdletBinding()]
param(
    [string]$Version,
    [switch]$DryRun,
    [switch]$SkipLocalUpdate,
    [switch]$NoGitCommit
)

$ErrorActionPreference = 'Stop'
$gitExe = (Get-Command git.exe -ErrorAction Stop).Source
$utf8 = [Text.UTF8Encoding]::new($false)
Add-Type -AssemblyName System.IO.Compression.FileSystem

function Invoke-ReleaseGit([string]$Directory,[string[]]$Arguments) {
    $safe = $Directory.Replace('\','/')
    # Use the same text bytes in archives regardless of the host Git line-ending settings.
    & $gitExe --no-optional-locks -c "safe.directory=$safe" -c core.quotePath=false -c core.longpaths=true -c core.autocrlf=false -c core.eol=lf -C $Directory @Arguments
    if ($LASTEXITCODE -ne 0) { throw "Git $($Arguments[0]) failed with exit code $LASTEXITCODE." }
}

function Get-CheckedItem([string]$Path,[string]$Parent,[switch]$Directory) {
    $item = Get-Item -LiteralPath $Path -Force
    $resolved = (Resolve-Path -LiteralPath $Path).ProviderPath
    if ($resolved -ne $item.FullName -or (Split-Path -Parent $resolved) -ne $Parent -or
        $item.PSIsContainer -ne $Directory.IsPresent -or ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw "Unexpected path or linked item: $Path"
    }
    return $item
}

function Get-CheckedDirectory([string]$Path,[string]$Parent) {
    if (-not (Test-Path -LiteralPath $Path)) {
        [void][IO.Directory]::CreateDirectory($Path)
    }
    return Get-CheckedItem $Path $Parent -Directory
}

function Get-SafeFiles([string]$Directory) {
    foreach ($item in (Get-ChildItem -LiteralPath $Directory -Force)) {
        if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) {
            throw "Linked items are outside the release scope: $($item.FullName)"
        }
        if ($item.PSIsContainer) { Get-SafeFiles $item.FullName }
        else { $item }
    }
}

function Get-Sha256([IO.Stream]$Stream) {
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try { return [BitConverter]::ToString($algorithm.ComputeHash($Stream)).Replace('-','').ToLowerInvariant() }
    finally { $algorithm.Dispose(); $Stream.Dispose() }
}

function Read-ZipHashes([string]$Path,[string]$Prefix = '') {
    $hashes = @{}
    $zip = [IO.Compression.ZipFile]::OpenRead($Path)
    try {
        foreach ($entry in $zip.Entries) {
            if (-not $entry.Name) { continue }
            $name = $entry.FullName.Replace('\','/')
            if (-not $name.StartsWith($Prefix,[StringComparison]::Ordinal)) { throw "Unexpected ZIP path: $name" }
            $relative = $name.Substring($Prefix.Length)
            if ($relative -match '(^/|(^|/)\.\.(/|$)|:)' -or $hashes.ContainsKey($relative)) {
                throw "Invalid or repeated ZIP path: $relative"
            }
            $hashes[$relative] = Get-Sha256 ($entry.Open())
        }
    } finally { $zip.Dispose() }
    return $hashes
}

function Write-Checksums([string]$Path,[hashtable]$Hashes) {
    $lines = @(foreach ($name in ($Hashes.Keys | Sort-Object)) { "$($Hashes[$name])  $name" })
    [IO.File]::WriteAllText($Path,($lines -join "`n")+"`n",$utf8)
}

function Read-Checksums([string]$Path) {
    $hashes = @{}
    foreach ($line in [IO.File]::ReadAllLines($Path,$utf8)) {
        $parsed = [regex]::Match($line,'^([a-f0-9]{64})  (.+)$')
        if (-not $parsed.Success) { throw "Invalid checksum manifest: $Path" }
        $name = $parsed.Groups[2].Value
        if ($hashes.ContainsKey($name)) { throw "Repeated checksum path: $name" }
        $hashes[$name] = $parsed.Groups[1].Value
    }
    return $hashes
}

function Assert-MatchingHashes([hashtable]$Expected,[hashtable]$Actual,[string]$Label) {
    if ($Expected.Count -ne $Actual.Count) { throw "$Label has an unexpected file count." }
    foreach ($name in $Expected.Keys) {
        if ($Actual[$name] -ne $Expected[$name]) { throw "$Label differs at $name." }
    }
}

function Get-PackageHashes([string]$Directory,[string]$ExeName) {
    $actual = @{}
    foreach ($file in (Get-SafeFiles $Directory)) {
        $name = $file.FullName.Substring($Directory.Length+1).Replace('\','/')
        if ($name -eq 'SHA256SUMS.txt') { continue }
        if ($file.Length -le 0 -or $name -match '(?i)(^|/)(names|settings\.txt|\.git)(/|$)|\.(log|fm|pdb)$') {
            throw "Unexpected runtime or empty file in a public package: $name"
        }
        $actual[$name] = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    }
    if (-not $actual.ContainsKey($ExeName)) { throw "Package executable missing: $ExeName" }
    return $actual
}

$privacyPatterns = [ordered]@{
    'absolute filesystem path' = '(?i)(?<![a-z0-9])(?:[a-z]:[\\/]+[\p{L}\p{N}_.~-]|/(?:Users|home)/[\p{L}\p{N}_.~-]+/)|FM27E0-CAgrowth v\d+\.\d+\.\d+-测试包'
    'email requiring review' = '(?i)\b[A-Z0-9._%+-]+@(?!(?:[A-Z0-9.-]+\.)?(?:localhost|example\.(?:com|net|org)|users\.noreply\.github\.com)\b)[A-Z0-9.-]+\.[A-Z]{2,24}\b'
    'credential requiring review' = '-----BEGIN (?:[A-Z0-9]+ )?PRIVATE KEY-----|\bgh[pousr]_[0-9A-Za-z_]{20,}\b|\bgithub_pat_[0-9A-Za-z_]{20,}\b|\b(?:AKIA|ASIA)[0-9A-Z]{16}\b|\bsk-(?:proj-)?[0-9A-Za-z_-]{20,}\b|\bxox[abprs]-[0-9A-Za-z-]{12,}|\bAIza[0-9A-Za-z_-]{35}\b|\beyJ[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}\b|(?i:authorization\s*[:=]\s*bearer\s+[0-9A-Za-z._~-]{16,})'
    'secret assignment requiring review' = '(?i)\b(?:password|passwd|client[ _-]?secret|api[ _-]?key|access[ _-]?token|auth[ _-]?token)\b\s*[:=]\s*["''][^"''\r\n]{8,}["'']|[?&](?:access_token|api_key|password|client_secret)=[^\s"''&#]{7,}'
}
$binaryPathPattern = '(?i)[A-Z]:[\\/]+(?:Users|Documents and Settings|Desktop)[\\/]+|/(?:Users|home)/[A-Z0-9_.~-]+/|FM27E0-CAgrowth v\d+\.\d+\.\d+-测试包'

function Assert-SourcePrivacy([string]$Directory,[string[]]$Paths) {
    $findings = @()
    foreach ($path in $Paths) {
        if ($path -match '(?i)(^/|(^|/)\.\.(/|$)|:|(^|/)(\.git|names|\.release-cache)(/|$)|(^|/)(settings\.txt|\.env[^/]*?)$|\.(exe|pdb|log|zip|fm|fmf|user|suo)$)' -or
            ($path -match '(?i)\.dll$' -and $path -ne 'third-party/libzstd.dll')) {
            $findings += "$path (runtime or generated file)"
            continue
        }
        $file = Get-Item -LiteralPath (Join-Path $Directory $path) -Force
        if ($file.PSIsContainer -or ($file.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            $findings += "$path (linked or unsupported file)"
            continue
        }
        $rules = $privacyPatterns
        if ($file.Extension -eq '.gz') {
            $gzip = [IO.Compression.GZipStream]::new($file.OpenRead(),[IO.Compression.CompressionMode]::Decompress)
            $reader = [IO.StreamReader]::new($gzip,$utf8)
            try { $content = $reader.ReadToEnd() } finally { $reader.Dispose() }
        } elseif ($file.Extension -in @('.dll','.ico','.png')) {
            $bytes = [IO.File]::ReadAllBytes($file.FullName)
            $content = [Text.Encoding]::GetEncoding(28591).GetString($bytes) + [Environment]::NewLine +
                [Text.Encoding]::Unicode.GetString($bytes,0,$bytes.Length-($bytes.Length%2))
            $rules = @{
                'local profile path in binary' = $binaryPathPattern
                'credential requiring review' = $privacyPatterns['credential requiring review']
            }
        } else {
            $content = [IO.File]::ReadAllText($file.FullName,$utf8)
        }
        foreach ($rule in $rules.Keys) {
            if ($content -match $rules[$rule]) { $findings += "$path ($rule)" }
        }
    }
    if ($findings.Count -gt 0) {
        throw ("Privacy scan requires review. Matched values are omitted." + [Environment]::NewLine +
            ($findings -join [Environment]::NewLine))
    }
}

function Get-ProjectVersion([string]$Directory) {
    $code = [IO.File]::ReadAllText((Join-Path $Directory 'FmEditor.cs'),$utf8)
    $versionLine = [regex]::Matches($code,'(?m)^\s*public const string Version = "(\d+\.\d+\.\d+)";')
    if ($versionLine.Count -ne 1) { throw 'Could not identify the application version in FmEditor.cs.' }
    return $versionLine[0].Groups[1].Value
}

function Set-Text([string]$Directory,[string]$Relative,[scriptblock]$Change) {
    $file = Join-Path $Directory $Relative
    $content = [IO.File]::ReadAllText($file,$utf8)
    $encoding = if ($Relative -like '论坛首楼文案-*.txt') { [Text.UTF8Encoding]::new($true) } else { $utf8 }
    [IO.File]::WriteAllText($file,(& $Change $content),$encoding)
}

function Get-ManagedReleaseVersion($Item) {
    $pattern = if ($Item.PSIsContainer) {
        '^FM27E0-CAgrowth v(?<version>\d{1,5}\.\d{1,5}\.\d{1,5})-(?:发布|测试)包$'
    } else {
        '^FM27E0-CAgrowth(?: v(?<version>\d{1,5}\.\d{1,5}\.\d{1,5})-完整版发布包|-v(?<version>\d{1,5}\.\d{1,5}\.\d{1,5})(?:-source)?)\.zip$'
    }
    if ($Item.Name -match $pattern) { return [version]$Matches.version }
}

function Get-CleanupPlan([string]$LocalDirectory,[string]$ReleaseDirectory,[version]$NewVersion,[switch]$KeepLocal) {
    if (-not $KeepLocal -and (Test-Path -LiteralPath $LocalDirectory)) {
        foreach ($file in (Get-ChildItem -LiteralPath $LocalDirectory -File -Force)) {
            if ($file.Name -notmatch '^FM27E0-CAgrowth v(?<version>\d{1,5}\.\d{1,5}\.\d{1,5})\.exe$' -or
                [version]$Matches.version -ge $NewVersion) { continue }
            if ($file.Attributes -band [IO.FileAttributes]::ReparsePoint) {
                Write-Warning "Keeping the linked local EXE $($file.Name)."
                continue
            }
            [pscustomobject]@{Kind='LocalExe';Item=$file;Parent=$LocalDirectory}
        }
    }
    if (Test-Path -LiteralPath $ReleaseDirectory) {
        foreach ($item in (Get-ChildItem -LiteralPath $ReleaseDirectory -Force)) {
            $oldVersion = Get-ManagedReleaseVersion $item
            if ($null -eq $oldVersion -or $oldVersion -ge $NewVersion) { continue }
            try {
                if ($item.PSIsContainer) {
                    [void](Get-CheckedItem $item.FullName $ReleaseDirectory -Directory)
                    $actual = Get-PackageHashes $item.FullName "FM27E0-CAgrowth v$oldVersion.exe"
                    Assert-MatchingHashes (Read-Checksums (Join-Path $item.FullName 'SHA256SUMS.txt')) $actual $item.Name
                } else {
                    [void](Get-CheckedItem $item.FullName $ReleaseDirectory)
                    $prefix = if ($item.Name -like '*-source.zip') { '' } else { "FM27E0-CAgrowth v$oldVersion-发布包/" }
                    $member = if ($prefix) { "FM27E0-CAgrowth v$oldVersion.exe" } else { 'FmEditor.cs' }
                    if (-not (Read-ZipHashes $item.FullName $prefix).ContainsKey($member)) {
                        throw 'Expected project entry missing from ZIP.'
                    }
                }
            } catch {
                Write-Warning "Keeping $($item.Name). $($_.Exception.Message)"
                continue
            }
            [pscustomobject]@{Kind='OldRelease';Item=$item;Parent=$ReleaseDirectory}
        }
    }
}

function Set-ProjectVersion([string]$Directory,[string]$OldVersion,[string]$NewVersion) {
    Set-Text $Directory 'FmEditor.cs' {
        param($text)
        $text.Replace(('public const string Version = "{0}";' -f $OldVersion),
            ('public const string Version = "{0}";' -f $NewVersion))
    }
    Set-Text $Directory 'README.md' {
        param($text)
        $text = $text.Replace("# FM27E0 汉化改版 v$OldVersion 完整版","# FM27E0 汉化改版 v$NewVersion 完整版")
        $text = $text.Replace("FM27E0-CAgrowth v$OldVersion.exe","FM27E0-CAgrowth v$NewVersion.exe")
        $text.Replace("使用说明-$OldVersion.md","使用说明-$NewVersion.md")
    }
    Set-Text $Directory 'docs/sharing-portability.md' {
        param($text) $text.Replace("v$OldVersion 的 EXE","v$NewVersion 的 EXE")
    }
    foreach ($name in @('使用说明-{0}.md','论坛首楼文案-{0}.txt')) {
        $oldName = $name -f $OldVersion
        $oldPath = Join-Path $Directory $oldName
        if ($name -like '论坛首楼文案-*' -and -not (Test-Path -LiteralPath $oldPath)) { continue }
        $newName = $name -f $NewVersion
        [IO.File]::Move($oldPath,(Join-Path $Directory $newName))
        Set-Text $Directory $newName {
            param($text)
            $lines = $text -split '\r?\n',2
            $content = $lines[0].Replace("v$OldVersion","v$NewVersion") + [Environment]::NewLine + $lines[1]
            $content.Replace("FM27E0-CAgrowth v$OldVersion.exe","FM27E0-CAgrowth v$NewVersion.exe")
        }
    }
    if ((Get-ProjectVersion $Directory) -ne $NewVersion) { throw 'The version marker was not updated.' }
}

function Set-ReleaseNotes([string]$Directory,[string]$NewVersion,[int]$Assertions) {
    $date = [DateTime]::Now.ToString('yyyy-MM-dd',[Globalization.CultureInfo]::InvariantCulture)
    $count = $Assertions.ToString('N0',[Globalization.CultureInfo]::InvariantCulture)
    $eol = [Environment]::NewLine
    $heading = "## v$NewVersion (modified release, $date)"
    Set-Text $Directory 'CHANGELOG.md' {
        param($text)
        $unreleased = [regex]::new('(?im)^## (?:Unreleased|\[Unreleased\])[ \t]*(?=\r?$)')
        if ($unreleased.IsMatch($text)) { return $unreleased.Replace($text,$heading,1) }
        return [regex]::Replace($text,'\A# Changelog\r?\n\r?\n',
            "# Changelog$eol$eol$heading$eol$eol- Built the Windows editor and passed $count offline assertions.$eol$eol")
    }
    if (Test-Path -LiteralPath (Join-Path $Directory 'VALIDATION.md')) {
        Set-Text $Directory 'VALIDATION.md' {
            param($text)
            "# v$NewVersion 验证范围$eol$eol$date，构建通过，完整离线回归通过 $count 项断言。$eol$eol---$eol$eol" +
                [regex]::Replace($text,'\A# (?!#) ', '## ')
        }
    }
    Set-Text $Directory 'README.md' {
        param($text)
        [regex]::Replace($text,'(?m)^[0-9,]+ 项离线断言通过',"$count 项离线断言通过")
    }
    Set-Text $Directory "使用说明-$NewVersion.md" {
        param($text)
        [regex]::Replace($text,'(?m)^[0-9,]+ 项离线断言通过',"$count 项离线断言通过")
    }
    $forumName = "论坛首楼文案-$NewVersion.txt"
    if (Test-Path -LiteralPath (Join-Path $Directory $forumName)) {
        Set-Text $Directory $forumName {
            param($text)
            [regex]::Replace($text,'(?m)^本版通过 [0-9,]+ 项离线断言',"本版通过 $count 项离线断言")
        }
    }
}

function Assert-ReleaseExe([string]$Path,[string]$ExpectedVersion) {
    $exe = Get-Item -LiteralPath $Path
    if ($exe.Length -le 0 -or $exe.VersionInfo.ProductVersion -ne $ExpectedVersion) {
        throw "Unexpected EXE build: $Path"
    }
    $bytes = [IO.File]::ReadAllBytes($exe.FullName)
    if ($bytes.Length -lt 64 -or $bytes[0] -ne 77 -or $bytes[1] -ne 90) { throw 'Invalid Windows executable.' }
    $pe = [BitConverter]::ToInt32($bytes,60)
    $debug = $pe+24+112+48
    if ($pe -lt 64 -or $debug+8 -gt $bytes.Length -or
        [BitConverter]::ToUInt32($bytes,$pe) -ne 17744 -or [BitConverter]::ToUInt16($bytes,$pe+24) -ne 523 -or
        [BitConverter]::ToUInt32($bytes,$debug) -ne 0 -or [BitConverter]::ToUInt32($bytes,$debug+4) -ne 0) {
        throw 'The EXE is not a PE32+ build with an empty debug directory.'
    }
    $ansi = [Text.Encoding]::GetEncoding(28591).GetString($bytes)
    $wide = [Text.Encoding]::Unicode.GetString($bytes,0,$bytes.Length-($bytes.Length%2))
    if ($ansi -match $binaryPathPattern -or $wide -match $binaryPathPattern) {
        throw 'The EXE contains an absolute local path.'
    }
}

function Assert-CleanCheckout([string]$Directory,[string]$Commit) {
    if ((Invoke-ReleaseGit $Directory @('rev-parse','HEAD')) -ne $Commit -or
        @(Invoke-ReleaseGit $Directory @('status','--porcelain=v1','--untracked-files=all')).Count -ne 0) {
        throw 'Release requires a clean checkout. Save your work before retrying.'
    }
}

$source = (Get-Item -LiteralPath (Split-Path -Parent $PSScriptRoot)).FullName
$root = Split-Path -Parent $source
[void](Get-CheckedItem $source $root -Directory)
$local = Join-Path $root 'FM27E0-CAgrowth-local'
$releases = Join-Path $root 'FM27E0-CAgrowth-releases'
if ($source -in @($local,$releases) -or
    (Invoke-ReleaseGit $source @('rev-parse','--show-toplevel')).Replace('/','\') -ne $source) {
    throw 'The release script must run from the maintained Git checkout.'
}
$folders = if ($SkipLocalUpdate) { @($releases) } else { @($local,$releases) }
foreach ($folder in $folders) {
    if (Test-Path -LiteralPath $folder) { [void](Get-CheckedItem $folder $root -Directory) }
}

$head = Invoke-ReleaseGit $source @('rev-parse','HEAD')
Assert-CleanCheckout $source $head
$current = Get-ProjectVersion $source
if (-not $Version) {
    if (-not $DryRun) { throw 'Use -Version X.Y.Z when publishing a release.' }
    $active = [version]$current
    $Version = '{0}.{1}.{2}' -f $active.Major,$active.Minor,($active.Build+1)
}
if ($Version -notmatch '^(?:0|[1-9]\d{0,4})\.(?:0|[1-9]\d{0,4})\.(?:0|[1-9]\d{0,4})$' -or
    @($Version.Split('.') | Where-Object {[int]$_ -gt 65534}).Count -gt 0 -or [version]$Version -le [version]$current) {
    throw "Use an X.Y.Z version greater than $current and valid for the Windows assembly."
}
$tag = "v$Version"
if (@(Invoke-ReleaseGit $source @('tag','--list',$tag)).Count -ne 0) { throw "Tag already exists: $tag" }
$tracked = @(Invoke-ReleaseGit $source @('ls-tree','-r','--name-only',$head))
Assert-SourcePrivacy $source $tracked

$packageName = "FM27E0-CAgrowth $tag-发布包"
$shareName = "FM27E0-CAgrowth-$tag.zip"
$exeName = "FM27E0-CAgrowth $tag.exe"
$guideName = "使用说明-$Version.md"
$forumName = "论坛首楼文案-$Version.txt"
$packageTarget = Join-Path $releases $packageName
$shareTarget = Join-Path $releases $shareName
$localTarget = Join-Path $local $exeName
$sourceMember = "对应源码/FM27E0-CAgrowth-$tag-source.zip"
$renamedOld = @("使用说明-$current.md")
$renamedNew = @($guideName)
$forumTarget = $null
if ("论坛首楼文案-$current.txt" -in $tracked) {
    $renamedOld += "论坛首楼文案-$current.txt"
    $renamedNew += $forumName
    $forumTarget = Join-Path $root $forumName
}
$destinations = @($packageTarget,$shareTarget)
if ($forumTarget) { $destinations += $forumTarget }
if (-not $SkipLocalUpdate) { $destinations += $localTarget }
foreach ($target in $destinations) {
    if (Test-Path -LiteralPath $target) { throw "Destination already exists: $target" }
}
if (Test-Path -LiteralPath $releases) {
    foreach ($item in (Get-ChildItem -LiteralPath $releases -Force)) {
        $publishedVersion = Get-ManagedReleaseVersion $item
        if ($null -ne $publishedVersion -and $publishedVersion -gt [version]$Version) {
            throw "A newer managed release already exists: $($item.Name)"
        }
    }
}

$cleanup = @(Get-CleanupPlan $local $releases ([version]$Version) -KeepLocal:$SkipLocalUpdate)
Write-Output "Source $source"
Write-Output "Version v$current -> $tag"
Write-Output "Tracked privacy scan passed ($($tracked.Count) files)."
Write-Output "Release $packageTarget"
Write-Output "Share ZIP $shareTarget"
Write-Output "Source ZIP $(Join-Path $packageTarget $sourceMember)"
if ($forumTarget) { Write-Output "Separate forum text $forumTarget" }
if ($SkipLocalUpdate) { Write-Output 'Local update skipped.' } else { Write-Output "Local update $localTarget" }
if ($NoGitCommit) { Write-Output 'Git commit and tag skipped.' } else { Write-Output "Git will commit and tag $tag." }
foreach ($entry in $cleanup) { Write-Output "After verification remove $($entry.Item.FullName)" }
Write-Output 'Only older managed names in the local and releases directories are eligible. Personal runtime files are kept.'
if ($DryRun) {
    Write-Output 'Dry run complete. No build, writes, moves, deletions, commits or tags.'
    return
}

$cache = Get-CheckedDirectory (Join-Path $source '.release-cache') $source
$lockPath = Join-Path $cache.FullName 'release.lock'
if (Test-Path -LiteralPath $lockPath) { [void](Get-CheckedItem $lockPath $cache.FullName) }
$lock = [IO.File]::Open($lockPath,[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
$stage = $null
try {
    Assert-CleanCheckout $source $head
    $stage = Get-CheckedDirectory (Join-Path $cache.FullName ([Guid]::NewGuid().ToString('N'))) $cache.FullName
    $snapshot = (Get-CheckedDirectory (Join-Path $stage.FullName 'source') $stage.FullName).FullName
    $baseZip = Join-Path $stage.FullName 'base.zip'
    Invoke-ReleaseGit $source @('archive','--format=zip',"--output=$baseZip",$head)
    [IO.Compression.ZipFile]::ExtractToDirectory($baseZip,$snapshot)
    Set-ProjectVersion $snapshot $current $Version

    Push-Location -LiteralPath $snapshot
    try {
        & .\build.ps1
        if ($LASTEXITCODE -ne 0) { throw 'build.ps1 failed.' }
        $testOutput = @(& .\tests\run.ps1)
        $testExit = $LASTEXITCODE
        $testOutput
        if ($testExit -ne 0) { throw 'tests\run.ps1 failed.' }
    } finally { Pop-Location }
    $passed = [regex]::Match(($testOutput -join [Environment]::NewLine),'(?m)^PASS:\s*(\d+)\s+assertions\b')
    if (-not $passed.Success -or [int]$passed.Groups[1].Value -lt 34999) {
        throw 'Release requires at least 34,999 passing offline assertions.'
    }
    $buildExe = Join-Path $snapshot $exeName
    Assert-ReleaseExe $buildExe $Version
    Set-ReleaseNotes $snapshot $Version ([int]$passed.Groups[1].Value)

    $snapshotFiles = @($tracked | Where-Object {$_ -notin $renamedOld}) + $renamedNew
    Assert-SourcePrivacy $snapshot $snapshotFiles
    Invoke-ReleaseGit $snapshot @('init','--quiet')
    Invoke-ReleaseGit $snapshot (@('add','--')+$snapshotFiles)
    $tree = Invoke-ReleaseGit $snapshot @('write-tree')
    $checksumZip = Join-Path $stage.FullName 'checksums.zip'
    Invoke-ReleaseGit $snapshot @('archive','--format=zip',"--output=$checksumZip",$tree)
    $sourceHashes = Read-ZipHashes $checksumZip
    $sourceHashes.Remove('SHA256SUMS.txt')
    $sourceManifest = Join-Path $snapshot 'SHA256SUMS.txt'
    Write-Checksums $sourceManifest $sourceHashes
    Invoke-ReleaseGit $snapshot @('add','--','SHA256SUMS.txt')
    $releaseTree = Invoke-ReleaseGit $snapshot @('write-tree')

    $playerNotices = @('NAME-FALLBACK-NOTICE.txt','姓名库覆盖说明.md',
        'docs/name-translations.md','docs/role-presets.md')
    # Keep source attribution and the release script's sharing guide in the source archive.
    $sourceNotices = $playerNotices + @('docs/current-features.md','docs/growth-squads-support.md',
        'docs/registration-evidence.md','docs/xbox-pc-and-roster.md','docs/staff-transfer.md',
        'docs/sharing-portability.md')
    $exportFiles = @($snapshotFiles | Where-Object {
        $_ -ne 'VALIDATION.md' -and $_ -notlike '论坛首楼文案-*.txt' -and
        ($_ -notlike 'docs/*' -or $_ -in $sourceNotices)
    })
    $exportHashes = @{}
    foreach ($path in $exportFiles) {
        if ($path -ne 'SHA256SUMS.txt') { $exportHashes[$path] = $sourceHashes[$path] }
    }
    # The tagged checkout keeps its own manifest for all tracked files.
    Write-Checksums $sourceManifest $exportHashes
    $exportHashes['SHA256SUMS.txt'] = (Get-FileHash -LiteralPath $sourceManifest).Hash.ToLowerInvariant()
    Invoke-ReleaseGit $snapshot @('add','--','SHA256SUMS.txt')
    $exportTree = Invoke-ReleaseGit $snapshot @('write-tree')

    $package = (Get-CheckedDirectory (Join-Path $stage.FullName $packageName) $stage.FullName).FullName
    $sourceZip = Join-Path $package $sourceMember
    [void][IO.Directory]::CreateDirectory((Split-Path -Parent $sourceZip))
    Invoke-ReleaseGit $snapshot (@('archive','--format=zip',"--output=$sourceZip",$exportTree,'--')+$exportFiles)
    Assert-MatchingHashes $exportHashes (Read-ZipHashes $sourceZip) 'Source ZIP'
    Write-Checksums $sourceManifest $sourceHashes

    $publicFiles = @('ADDITIONAL-TERMS.txt','CHANGELOG.md','LICENSE',$guideName) + $playerNotices +
        @($snapshotFiles | Where-Object {$_ -like 'third-party/*'})
    foreach ($path in $publicFiles) {
        $destination = Join-Path $package $path
        [void][IO.Directory]::CreateDirectory((Split-Path -Parent $destination))
        Copy-Item -LiteralPath (Join-Path $snapshot $path) -Destination $destination
    }
    Copy-Item -LiteralPath $buildExe -Destination (Join-Path $package $exeName)
    $packageHashes = Get-PackageHashes $package $exeName
    Write-Checksums (Join-Path $package 'SHA256SUMS.txt') $packageHashes
    $packageHashes['SHA256SUMS.txt'] = (Get-FileHash -LiteralPath (Join-Path $package 'SHA256SUMS.txt')).Hash.ToLowerInvariant()
    $share = Join-Path $stage.FullName $shareName
    [IO.Compression.ZipFile]::CreateFromDirectory($package,$share,[IO.Compression.CompressionLevel]::Optimal,$true)
    Assert-MatchingHashes $packageHashes (Read-ZipHashes $share "$packageName/") 'Share ZIP'
    Write-Output 'New EXE, documentation and both ZIPs verified.'

    Assert-CleanCheckout $source $head
    $publish = Get-CheckedDirectory $releases $root
    [void](Get-CheckedItem $package $stage.FullName -Directory)
    [void](Get-CheckedItem $share $stage.FullName)
    foreach ($target in $destinations) {
        if (Test-Path -LiteralPath $target) { throw "Release destination changed during build: $target" }
    }
    Move-Item -LiteralPath $package -Destination (Join-Path $publish.FullName $packageName)
    Move-Item -LiteralPath $share -Destination (Join-Path $publish.FullName $shareName)
    if ($forumTarget) {
        [IO.File]::Copy((Join-Path $snapshot $forumName),$forumTarget)
    }
    if (-not $SkipLocalUpdate) {
        $install = Get-CheckedDirectory $local $root
        if (Test-Path -LiteralPath $localTarget) { throw 'Local EXE destination changed during build.' }
        Copy-Item -LiteralPath $buildExe -Destination $localTarget
        Assert-ReleaseExe $localTarget $Version
        $localLibrary = Get-CheckedDirectory (Join-Path $install.FullName 'third-party') $install.FullName
        foreach ($dependency in (Get-ChildItem -LiteralPath (Join-Path $snapshot 'third-party') -File)) {
            $target = Join-Path $localLibrary.FullName $dependency.Name
            if (-not (Test-Path -LiteralPath $target)) { Copy-Item -LiteralPath $dependency.FullName -Destination $target }
        }
    }

    foreach ($entry in @(Get-CleanupPlan $local $releases ([version]$Version) -KeepLocal:$SkipLocalUpdate)) {
        try {
            $old = Get-CheckedItem $entry.Item.FullName $entry.Parent -Directory:$entry.Item.PSIsContainer
            if ($old.PSIsContainer) { Remove-Item -LiteralPath $old.FullName -Recurse -Force }
            else { Remove-Item -LiteralPath $old.FullName -Force }
            Write-Output "Removed $($old.FullName)"
        } catch {
            Write-Warning "Keeping $($entry.Item.FullName). $($_.Exception.Message)"
        }
    }

    if (-not $NoGitCommit) {
        Assert-CleanCheckout $source $head
        $updates = @(@('FmEditor.cs','README.md','docs/sharing-portability.md','CHANGELOG.md','VALIDATION.md','SHA256SUMS.txt') |
            Where-Object {$_ -in $snapshotFiles}) + $renamedNew
        foreach ($path in $updates) {
            Copy-Item -LiteralPath (Join-Path $snapshot $path) -Destination (Join-Path $source $path) -Force
        }
        foreach ($path in $renamedOld) {
            $oldDoc = Get-CheckedItem (Join-Path $source $path) $source
            Remove-Item -LiteralPath $oldDoc.FullName
        }
        Invoke-ReleaseGit $source (@('add','-A','--')+$updates+$renamedOld)
        if ((Invoke-ReleaseGit $source @('write-tree')) -ne $releaseTree) {
            throw 'Staged sources differ from the checked archive. Review the changes before committing.'
        }
        $identity = @(Invoke-ReleaseGit $source @('log','-1','--format=%cn%n%ce',$head))
        Invoke-ReleaseGit $source @('-c',"user.name=$($identity[0])",'-c',"user.email=$($identity[1])",
            'commit','-m',"release: bump version to $tag")
        $commit = Invoke-ReleaseGit $source @('rev-parse','HEAD')
        Invoke-ReleaseGit $source @('-c','tag.gpgSign=false','tag',$tag,$commit)
        Assert-CleanCheckout $source $commit
        Write-Output "Release commit $commit ($tag)."
    }
    Write-Output "Published locally in $packageTarget"
} finally {
    try {
        if ($null -ne $stage) {
            $verifiedStage = Get-CheckedItem $stage.FullName $cache.FullName -Directory
            [void]@(Get-SafeFiles $verifiedStage.FullName)
            Remove-Item -LiteralPath $verifiedStage.FullName -Recurse -Force
        }
    } finally { $lock.Dispose() }
}
