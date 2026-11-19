param([string]$Output = 'LunaQuiz.exe')

$ErrorActionPreference = 'Stop'
$compiler = Join-Path ([Runtime.InteropServices.RuntimeEnvironment]::GetRuntimeDirectory()) 'csc.exe'
Push-Location -LiteralPath $PSScriptRoot
try {
    $version = ([IO.File]::ReadAllText((Join-Path $PSScriptRoot 'VERSION.txt'))).Trim()
    if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'VERSION.txt must contain major.minor.patch.' }
    $metadata = Join-Path ([IO.Path]::GetTempPath()) ('Luna-version-' + [Guid]::NewGuid().ToString('N') + '.cs')
    [IO.File]::WriteAllText($metadata, ('[assembly:System.Reflection.AssemblyVersion("{0}.0")] [assembly:System.Reflection.AssemblyFileVersion("{0}.0")] [assembly:System.Reflection.AssemblyTitle("Luna Quiz")]' -f $version))
    $resources = 'LunaQuiz.ps1', 'FrameState.ps1', 'QuizOcr.ps1', 'MapleTheme.cs', 'QuizHeader.cs', 'QuizUpdater.cs', 'UpdateOverlay.cs', 'VERSION.txt', 'Maplestory Bold.ttf', 'LICENSE-Maplestory.txt'
    $resourceArgs = $resources | ForEach-Object { "/resource:$_,$_" }
    & $compiler /nologo /target:winexe /optimize+ /codepage:65001 "/out:$Output" /win32icon:LunaQuiz.ico /win32manifest:LunaQuiz.manifest /reference:System.Windows.Forms.dll $resourceArgs Launcher.cs UpdateInstall.cs $metadata
    if ($LASTEXITCODE -ne 0) { throw "Compile failed with exit code $LASTEXITCODE." }
} finally {
    if ($metadata) { [IO.File]::Delete($metadata) }
    Pop-Location
}
