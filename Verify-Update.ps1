$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Net.Http, System.Web.Extensions
if (-not ('QuizUpdater' -as [type])) {
    Add-Type -Path (Join-Path $PSScriptRoot 'QuizUpdater.cs') -ReferencedAssemblies System.Net.Http, System.Web.Extensions
}
$updater = [QuizUpdater]::new('1.0.0')
function Get-TestRelease([string]$Tag, [string]$HostPrefix = 'https://github.com/trick86155-ship-it/gpt-quiz/releases/download') {
    @{ tag_name=$Tag; draft=$false; prerelease=$false; assets=@(
        @{ name='LunaQuiz.exe'; size=123; browser_download_url="$HostPrefix/$Tag/LunaQuiz.exe" },
        @{ name='LunaQuiz.exe.sha256'; size=80; browser_download_url="$HostPrefix/$Tag/LunaQuiz.exe.sha256" }
    ) } | ConvertTo-Json -Depth 5
}
$updater.AcceptRelease((Get-TestRelease 'v1.0.1'))
if (-not $updater.Available) { throw 'A newer complete release must enable the update.' }
$updater.AcceptRelease((Get-TestRelease 'v1.0.0'))
if ($updater.Available) { throw 'Current version must not enable an update.' }
$updater.AcceptRelease((Get-TestRelease 'v0.9.9'))
if ($updater.Available) { throw 'Downgrades must not enable an update.' }
$updater.AcceptRelease((Get-TestRelease 'v1.0.1' 'https://example.com/releases'))
if ($updater.Available) { throw 'Assets outside the configured repository must be rejected.' }
$partial = Get-TestRelease 'v1.0.1' | ConvertFrom-Json
$partial.assets = @($partial.assets[0])
$updater.AcceptRelease(($partial | ConvertTo-Json -Depth 5))
if ($updater.Available) { throw 'Missing checksum must disable update.' }
if (-not [QuizUpdater]::SameVersion('1.0.0.0','1.0.0') -or [QuizUpdater]::SameVersion('1.0.1.0','1.0.0')) {
    throw 'Windows file version comparison failed.'
}
'Release selection, newer-only button, checksum presence, asset origin and version checks passed.'

$root = Join-Path ([IO.Path]::GetTempPath()) ('luna-update-fixture-' + [Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($root)
$files = @('old.cs','new.cs','test.cs','worker.exe','LunaQuiz.exe','.LunaQuiz-update-fixture.download','restarted.txt')
$compiler = Join-Path ([Runtime.InteropServices.RuntimeEnvironment]::GetRuntimeDirectory()) 'csc.exe'
try {
    $old = '[assembly:System.Reflection.AssemblyFileVersion("1.0.0.0")] class Old {static void Main(){}}'
    $new = '[assembly:System.Reflection.AssemblyFileVersion("1.0.1.0")] class New {static void Main(){System.IO.File.WriteAllText(System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory,"restarted.txt"),"restarted");}}'
    [IO.File]::WriteAllText((Join-Path $root 'old.cs'), $old)
    [IO.File]::WriteAllText((Join-Path $root 'new.cs'), $new)
    [IO.File]::WriteAllText((Join-Path $root 'test.cs'), 'class Test {static int Main(string[] args){try{UpdateInstall.Install(args);return 0;}catch(System.Exception e){System.Console.Error.WriteLine(e);return 1;}}}')
    & $compiler /nologo /target:exe "/out:$(Join-Path $root 'LunaQuiz.exe')" (Join-Path $root 'old.cs')
    if ($LASTEXITCODE) { throw 'Fixture compiler failed.' }
    & $compiler /nologo /target:exe "/out:$(Join-Path $root '.LunaQuiz-update-fixture.download')" (Join-Path $root 'new.cs')
    if ($LASTEXITCODE) { throw 'Replacement fixture compiler failed.' }
    & $compiler /nologo /target:exe "/out:$(Join-Path $root 'worker.exe')" /reference:System.Windows.Forms.dll (Join-Path $root 'test.cs') (Join-Path $PSScriptRoot 'UpdateInstall.cs')
    if ($LASTEXITCODE) { throw 'Installer fixture compiler failed.' }
    $stage = Join-Path $root '.LunaQuiz-update-fixture.download'
    $hash = [QuizUpdater]::FileHash($stage)
    & (Join-Path $root 'worker.exe') --install-update (Join-Path $root 'LunaQuiz.exe') $stage $hash 2147483647 2147483646 1.0.1
    if ($LASTEXITCODE) { throw 'Test installer failed.' }
    $marker = Join-Path $root 'restarted.txt'
    foreach ($attempt in 1..30) {
        if ([IO.File]::Exists($marker)) { break }
        Start-Sleep -Milliseconds 100
    }
    if (-not [IO.File]::Exists($marker) -or [IO.File]::Exists($stage)) { throw 'Restart or replacement was not observed.' }
    if ([QuizUpdater]::FileHash((Join-Path $root 'LunaQuiz.exe')) -cne $hash) { throw 'Installed binary differs from verified download.' }
    'Installer fixture replaced the old executable and restarted the new version.'
} finally {
    foreach ($name in $files) { [IO.File]::Delete((Join-Path $root $name)) }
    [IO.Directory]::Delete($root)
}
