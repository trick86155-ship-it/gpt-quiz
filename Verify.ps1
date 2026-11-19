$ErrorActionPreference = 'Stop'
$state = @{ Candidate = ''; Count = 0; Sent = '' }
function Assert-Next($frame, $expected) {
    $actual = Update-FrameState $state $frame
    if ($actual -ne $expected) { throw "Frame ${frame}: expected $expected, received $actual" }
}
. (Join-Path $PSScriptRoot 'FrameState.ps1')
Assert-Next 'first' $false
Assert-Next 'first' $false
Assert-Next 'first' $true
$state.Sent = 'first'
Assert-Next 'first' $false
Assert-Next 'transient' $false
Assert-Next 'second' $false
Assert-Next 'second' $false
Assert-Next 'second' $true
'Frame state checks passed.'

Add-Type -AssemblyName System.Drawing
$area = [Drawing.Rectangle]::new(-640, 100, 640, 480)
$click = Read-QuizAction '{"answer":"2","action":"click","x":25,"y":40}' $area
if ($click.Action -cne 'click' -or $click.Start.X -ne -615 -or $click.Start.Y -ne 140) {
    throw 'Click must translate frame pixels to desktop coordinates.'
}
$drag = Read-QuizAction '{"answer":"O","action":"drag","x":25,"y":40,"toX":320,"toY":240}' $area
if ($drag.Action -cne 'drag' -or $drag.End.X -ne -320 -or $drag.End.Y -ne 340) {
    throw 'Drag must translate both endpoints.'
}
foreach ($bad in @(
    '{"answer":"2","action":"click","x":640,"y":40}',
    '{"answer":"2","action":"click","x":25.1,"y":40}',
    '{"answer":"2","action":"click","x":"25","y":40}',
    '{"answer":"2","action":"click","x":25}',
    '{"answer":"O","action":"drag","x":25,"y":40,"toX":320,"toY":480}',
    '{"answer":"O","action":"drag","x":25,"y":40,"toX":25,"toY":40}',
    '{"answer":"2","action":"doubleClick","x":25,"y":40}',
    '{"answer":"2","action":["none","click"],"x":25,"y":40}',
    'not JSON'
)) {
    if ($null -ne (Read-QuizAction $bad $area)) { throw "Unexpected action: $bad" }
}
$display = Read-QuizAction '{"answer":"2","action":"none"}' $area
if ($display.Action -cne 'none' -or $display.Answer -ne '2') { throw 'Display-only answers must remain available.' }
'Quiz action checks passed.'
if ($null -ne (Read-QuizAction '{"answer":"O","action":"drag","optionCount":1,"drags":[{"box":"O","x":25,"y":40,"toX":510,"toY":240}]}' $area)) {
    throw 'A drag into the opposite box must be rejected.'
}
if (-not (Test-QuizStatementText '팔기는 움직이지 않고 있습니다' '딸기는 움직이지 않고 있습니다') -or
    (Test-QuizStatementText '움직이는 숫자는 3보다 작습니다' '움직이는 숫자는 8보다 작습니다') -or
    (Test-QuizStatementText '사과는 움직입니다' '사과는 움직이지 않습니다')) {
    throw 'OCR matching must tolerate a minor spelling difference without changing numbers or meaning.'
}
if (-not (Test-QuizDropText '오렌지는 움직이고 있습니다' '' 'O 오렌지는 움직이고 있습니다') -or
    (Test-QuizDropText '오렌지는 움직이고 있습니다' '오렌지는 움직이고 있습니다' '오렌지는 움직이고 있습니다') -or
    (Test-QuizDropText '오렌지는 움직이고 있습니다' '' '키위는 회전합니다') -or
    (Test-QuizDropText '' '' '')) {
    throw 'Drop verification must require the source text to leave its row and appear in the target.'
}

$all = Read-QuizAction '{"answer":"1 O, 2 X, 3 O","action":"drag","optionCount":3,"drags":[{"x":25,"y":40,"toX":320,"toY":240},{"x":25,"y":80,"toX":510,"toY":240},{"x":25,"y":120,"toX":320,"toY":240}]}' $area
if ($all.Steps.Count -ne 3 -or $all.Steps[1].End.X -ne -130 -or $all.Steps[2].Start.Y -ne 220) {
    throw 'All O/X drag paths must be retained and translated in order.'
}
foreach ($bad in @(
    '{"answer":"O","action":"drag","optionCount":2,"drags":[{"x":25,"y":40,"toX":320,"toY":240}]}',
    '{"answer":"O","action":"drag","optionCount":2,"drags":[{"x":25,"y":40,"toX":320,"toY":240},{"x":25,"y":40,"toX":510,"toY":240}]}',
    '{"answer":"O","action":"drag","optionCount":2,"drags":[{"x":25,"y":40,"toX":320,"toY":240},{"x":25,"y":80,"toX":640,"toY":240}]}'
)) {
    if ($null -ne (Read-QuizAction $bad $area)) { throw 'Invalid batch must be rejected in full.' }
}

$id = '0199a213-81c0-7800-8aa1-bbab2a035a53'
$sessionEvent = '{"type":"thread.started","thread_id":"' + $id + '"}'
if ((Get-CodexSessionId ("noise`n{invalid`n" + $sessionEvent)) -cne $id) { throw 'Session event was not read.' }
if (Get-CodexSessionId '{"type":"thread.started","thread_id":"invalid"}') { throw 'Invalid session ID was accepted.' }
$imagePath = 'C:\Quiz Images\frame-1.png'
$resultPath = 'C:\Quiz Images\answer.txt'
$fresh = Get-QuizCodexArguments '' @($imagePath) $resultPath
$resume = Get-QuizCodexArguments $id @($imagePath) $resultPath
if (-not $fresh.StartsWith('-a never exec --') -or $fresh.Contains('--ephemeral') -or
    -not $fresh.Contains('--image "' + $imagePath + '"') -or -not $fresh.Contains('--output-last-message "' + $resultPath + '"')) {
    throw 'First quiz must create a saved session and quote paths.'
}
if (-not $resume.StartsWith('-a never exec resume ') -or -not $resume.EndsWith("$id -") -or $resume.Contains('--color')) {
    throw 'Later quizzes must resume the exact session.'
}
'Codex session checks passed.'
if (-not (Test-CodexSessionWriterConflict "Error: thread-store conflict: thread $id already has an active writer") -or
    (Test-CodexSessionWriterConflict 'Error: unexpected argument --color')) {
    throw 'Only an active session writer conflict should trigger the session-in-use message.'
}
$retry = @{ WriterRetries = 0; RetryAt = $null }
$now = [DateTime]::UtcNow
foreach ($attempt in 1..5) {
    if (-not (Set-QuizWriterRetry $retry $now) -or $retry.WriterRetries -ne $attempt -or
        $retry.RetryAt -ne $now.AddSeconds(3)) { throw 'Writer retry scheduling changed.' }
    $now = $retry.RetryAt
}
if (Set-QuizWriterRetry $retry $now) { throw 'The retry limit was ignored.' }

$trainingRoot = Join-Path ([IO.Path]::GetTempPath()) ('luna-training-test-' + [Guid]::NewGuid().ToString('N'))
$trainingArea = [Drawing.Rectangle]::new(-20, 10, 32, 24)
$otherArea = [Drawing.Rectangle]::new(-19, 10, 32, 24)
$sample = [Drawing.Bitmap]::new(32, 24)
$savedSamples = @()
try {
    $sample.SetPixel(5, 4, [Drawing.Color]::Gold)
    foreach ($label in 'normal', 'quiz-1', 'quiz-2', 'quiz-3', 'quiz-3') {
        $file = Save-QuizTrainingSample $trainingRoot $trainingArea $label $sample
        $savedSamples += $file
        if ((Split-Path (Split-Path $file -Parent) -Leaf) -cne $label) { throw 'Wrong training label directory.' }
        $decoded = [Drawing.Bitmap]::new($file)
        try {
            if ($decoded.Size -ne $trainingArea.Size -or $decoded.GetPixel(5, 4).ToArgb() -ne [Drawing.Color]::Gold.ToArgb()) {
                throw 'Training sample must preserve the original pixels.'
            }
        } finally { $decoded.Dispose() }
    }
    if (@($savedSamples | Select-Object -Unique).Count -ne 5) { throw 'Training captures were overwritten.' }
    if ((Get-QuizTrainingFolder $trainingRoot $trainingArea) -eq (Get-QuizTrainingFolder $trainingRoot $otherArea)) {
        throw 'Different capture areas must use separate data folders.'
    }
} finally {
    $sample.Dispose()
    foreach ($file in $savedSamples) { [IO.File]::Delete($file) }
    if ([IO.Directory]::Exists($trainingRoot)) {
        $areaFolder = Split-Path (Split-Path $savedSamples[0] -Parent) -Parent
        foreach ($folder in [IO.Directory]::GetDirectories($areaFolder)) { [IO.Directory]::Delete($folder) }
        [IO.Directory]::Delete($areaFolder)
        [IO.Directory]::Delete($trainingRoot)
    }
}
'Training capture checks passed.'
