param([Parameter(Mandatory=$true)][string]$Version, [string]$Notes = '루나 퀴즈 업데이트')

$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Version must be major.minor.patch.' }
Push-Location -LiteralPath $PSScriptRoot
try {
    & gh repo view trick86155-ship-it/gpt-quiz --json nameWithOwner | Out-Null
    if ($LASTEXITCODE) { throw 'GitHub repository access failed.' }
    $oldVersion = ([IO.File]::ReadAllText((Join-Path $PSScriptRoot 'VERSION.txt'))).Trim()
    if ([version]$Version -lt [version]$oldVersion) { throw 'Cannot publish an older version.' }
    & gh release view "v$Version" -R trick86155-ship-it/gpt-quiz 2>$null | Out-Null
    if ($LASTEXITCODE -eq 0) { throw 'This release already exists. Choose a new version.' }
    [IO.File]::WriteAllText((Join-Path $PSScriptRoot 'VERSION.txt'), $Version + [Environment]::NewLine)
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'Verify.ps1')
    if ($LASTEXITCODE) { throw 'Verification failed.' }
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'Verify-Update.ps1')
    if ($LASTEXITCODE) { throw 'Update verification failed.' }
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'Build-Luna.ps1')
    if ($LASTEXITCODE) { throw 'Build failed. Close LunaQuiz.exe before publishing.' }
    $checksum = Join-Path $PSScriptRoot 'LunaQuiz.exe.sha256'
    $notesFile = Join-Path ([IO.Path]::GetTempPath()) ('luna-release-' + [Guid]::NewGuid().ToString('N') + '.txt')
    try {
        $hash = (Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'LunaQuiz.exe') -Algorithm SHA256).Hash.ToLowerInvariant()
        [IO.File]::WriteAllText($checksum, "$hash  LunaQuiz.exe" + [Environment]::NewLine)
        [IO.File]::WriteAllText($notesFile, $Notes, [Text.UTF8Encoding]::new($false))
        & gh release create "v$Version" LunaQuiz.exe LunaQuiz.exe.sha256 -R trick86155-ship-it/gpt-quiz --draft --title "루나 퀴즈 v$Version" --notes-file $notesFile
        if ($LASTEXITCODE) { throw 'Draft release upload failed.' }
        $release = & gh release view "v$Version" -R trick86155-ship-it/gpt-quiz --json assets | ConvertFrom-Json
        if (@($release.assets | Where-Object name -eq 'LunaQuiz.exe').Count -ne 1 -or
            @($release.assets | Where-Object name -eq 'LunaQuiz.exe.sha256').Count -ne 1) { throw 'Release assets missing.' }
        & gh release edit "v$Version" -R trick86155-ship-it/gpt-quiz --draft=false --latest
        if ($LASTEXITCODE) { throw 'Release publication failed.' }
    } finally {
        if ($notesFile) { [IO.File]::Delete($notesFile) }
    }
} finally {
    Pop-Location
}
