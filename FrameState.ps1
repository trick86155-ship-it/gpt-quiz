function Update-FrameState($State, [string]$Signature) {
    if ($State.Candidate -ne $Signature) {
        $State.Candidate = $Signature
        $State.Count = 1
    } else {
        $State.Count = [Math]::Min(3, $State.Count + 1)
    }
    return ($State.Count -eq 3 -and $State.Sent -ne $Signature)
}

function Get-QuizCaptureOffsets {
    0, 300, 650, 1100, 1700
}

function Get-QuizTrainingFolder([string]$Root, [Drawing.Rectangle]$Area) {
    Join-Path $Root ("area_{0}_{1}_{2}_{3}" -f $Area.X, $Area.Y, $Area.Width, $Area.Height)
}

function Save-QuizTrainingSample([string]$Root, [Drawing.Rectangle]$Area,
    [ValidateSet('normal', 'quiz-1', 'quiz-2', 'quiz-3')][string]$Label, [Drawing.Bitmap]$Image) {
    if ($Image.Size -ne $Area.Size) { throw 'The training image size does not match the capture area.' }
    $folder = Join-Path (Get-QuizTrainingFolder $Root $Area) $Label
    [void][IO.Directory]::CreateDirectory($folder)
    $name = [DateTime]::UtcNow.ToString("yyyyMMdd'T'HHmmssfff'Z'") + '-' + [Guid]::NewGuid().ToString('N')
    $draft = Join-Path $folder "$name.tmp"
    $file = Join-Path $folder "$name.png"
    try {
        $Image.Save($draft, [Drawing.Imaging.ImageFormat]::Png)
        [IO.File]::Move($draft, $file)
        return $file
    } finally {
        [IO.File]::Delete($draft)
    }
}

function Get-CodexSessionId([string]$Output) {
    foreach ($line in ($Output -split '\r?\n')) {
        if (-not $line.StartsWith('{')) { continue }
        try { $event = ConvertFrom-Json -InputObject $line -ErrorAction Stop }
        catch { continue }
        $id = [Guid]::Empty
        if ($event.type -ceq 'thread.started' -and
            [Guid]::TryParseExact([string]$event.thread_id, 'D', [ref]$id) -and $id -ne [Guid]::Empty) {
            return $id.ToString()
        }
    }
}

function Get-QuizCodexArguments([string]$SessionId, [string[]]$Images, [string]$Result) {
    $prefix = '-a never exec'
    $resumeId = ''
    if ($SessionId) {
        $prefix += ' resume'
        $resumeId = ' ' + [Guid]::ParseExact($SessionId, 'D').ToString()
    }
    $attachments = ($Images | ForEach-Object { '--image "' + $_ + '"' }) -join ' '
    return $prefix + ' --ignore-user-config -c sandbox_mode=read-only --skip-git-repo-check --json -m gpt-6.1-terra ' +
        $attachments + ' --output-last-message "' + $Result + '"' + $resumeId + ' -'
}

function Test-CodexSessionWriterConflict([string]$Detail) {
    return $Detail -match 'thread-store conflict[^\r\n]*already has an active writer'
}

function Set-QuizWriterRetry($Request, [datetime]$Now) {
    if ($Request.WriterRetries -ge 5) { return $false }
    $Request.WriterRetries++
    $Request.RetryAt = $Now.AddSeconds(3)
    return $true
}

function Test-QuizDropText([string]$Expected, [string]$Source, [string]$Destination) {
    $expectedText = $Expected -replace '[^가-힣a-zA-Z0-9]', ''
    $sourceText = $Source -replace '[^가-힣a-zA-Z0-9]', ''
    $targetText = $Destination -replace '[^가-힣a-zA-Z0-9]', ''
    return $expectedText.Length -ge 5 -and -not $sourceText.Contains($expectedText) -and $targetText.Contains($expectedText)
}

function Test-QuizStatementText([string]$Expected, [string]$Actual) {
    $a = ($Expected -replace '[^가-힣a-zA-Z0-9]', '').ToLowerInvariant()
    $b = ($Actual -replace '[^가-힣a-zA-Z0-9]', '').ToLowerInvariant()
    if ($a.Length -lt 5 -or $a.Length -ne $b.Length) { return $false }
    if ($a -ceq $b) { return $true }
    if ($a.Length -lt 10 -or ($a -replace '\D') -cne ($b -replace '\D')) { return $false }
    $limit = if ($a.Length -ge 30) { 3 } elseif ($a.Length -ge 20) { 2 } else { 1 }
    $differences = 0
    for ($i = 0; $i -lt $a.Length; $i++) {
        if ($a[$i] -cne $b[$i]) {
            $differences++
            if ($differences -gt $limit) { return $false }
        }
    }
    return $true
}

function ConvertTo-QuizScreenPoint($X, $Y, [Drawing.Rectangle]$Area) {
    if (($X -isnot [int] -and $X -isnot [long]) -or
        ($Y -isnot [int] -and $Y -isnot [long]) -or
        $X -lt 0 -or $X -ge $Area.Width -or $Y -lt 0 -or $Y -ge $Area.Height) {
        return $null
    }
    return [Drawing.Point]::new(($Area.Left + $X), ($Area.Top + $Y))
}

function Read-QuizAction([string]$Reply, [Drawing.Rectangle]$Area) {
    if (-not $Reply.StartsWith('{') -or -not $Reply.EndsWith('}')) { return $null }
    try { $data = ConvertFrom-Json -InputObject $Reply -ErrorAction Stop }
    catch { return $null }
    if ($data.answer -isnot [string] -or [string]::IsNullOrWhiteSpace($data.answer) -or
        $data.action -isnot [string] -or $data.action -cnotin @('none', 'click', 'drag')) {
        return $null
    }
    $action = @{ Answer = $data.answer; Action = $data.action; Start = $null; End = $null; Steps = @() }
    if ($data.action -ceq 'none') { return $action }
    $drag = $data.action -ceq 'drag'
    $entries = @($data)
    if ($drag -and ($data.PSObject.Properties['drags'] -or $data.PSObject.Properties['optionCount'])) {
        if ($data.drags -isnot [array] -or ($data.optionCount -isnot [int] -and $data.optionCount -isnot [long]) -or
            $data.optionCount -lt 1 -or $data.optionCount -gt 16 -or $data.optionCount -ne $data.drags.Count) {
            return $null
        }
        $entries = $data.drags
    }
    $sources = New-Object 'Collections.Generic.HashSet[Drawing.Point]'
    foreach ($entry in $entries) {
        $start = ConvertTo-QuizScreenPoint $entry.x $entry.y $Area
        if ($null -eq $start -or -not $sources.Add($start)) { return $null }
        $end = $start
        if ($drag) {
            $end = ConvertTo-QuizScreenPoint $entry.toX $entry.toY $Area
            if ($null -eq $end -or $end.Equals($start)) { return $null }
            if ($entry.PSObject.Properties['box']) {
                $expectedBox = if ($end.X -lt ($Area.Left + [int]($Area.Width / 2))) { 'O' } else { 'X' }
                if ($entry.box -isnot [string] -or $entry.box -cne $expectedBox) { return $null }
            }
        }
        $action.Steps += @{ Start = $start; End = $end }
    }
    $action.Start = $action.Steps[0].Start
    if ($drag) { $action.End = $action.Steps[0].End }
    return $action
}

function Get-PresetFile([string]$Directory, [ValidateRange(1,3)][int]$Slot) {
    Join-Path $Directory "preset-$Slot.json"
}

function ConvertTo-PresetRect($Area) {
    if ($null -eq $Area) { return $null }
    return @{ X = $Area.X; Y = $Area.Y; Width = $Area.Width; Height = $Area.Height }
}

function ConvertFrom-PresetRect($Area, [Drawing.Rectangle]$Desktop) {
    if ($null -eq $Area) { return $null }
    $rect = [Drawing.Rectangle]::new([int]$Area.X, [int]$Area.Y, [int]$Area.Width, [int]$Area.Height)
    if ($rect.Width -lt 24 -or $rect.Height -lt 24 -or
        $rect.X -lt $Desktop.Left -or $rect.Y -lt $Desktop.Top -or
        ([long]$rect.X + $rect.Width) -gt $Desktop.Right -or
        ([long]$rect.Y + $rect.Height) -gt $Desktop.Bottom) {
        throw '저장된 영역이 현재 모니터 범위를 벗어났습니다.'
    }
    return $rect
}

function Save-AreaPreset([string]$Directory, [int]$Slot, $Anchor, $Quiz) {
    if ($null -eq $Anchor -and $null -eq $Quiz) {
        throw '영역을 하나 이상 지정한 뒤 저장하세요.'
    }
    $path = Get-PresetFile $Directory $Slot
    $temp = Join-Path $Directory ("draft-" + [Guid]::NewGuid().ToString('N') + ".json")
    $payload = @{
        Version = 1
        Anchor = ConvertTo-PresetRect $Anchor
        Quiz = ConvertTo-PresetRect $Quiz
    }
    [void][IO.Directory]::CreateDirectory($Directory)
    try {
        [IO.File]::WriteAllText($temp, ($payload | ConvertTo-Json -Depth 3))
        if ([IO.File]::Exists($path)) { [IO.File]::Replace($temp, $path, [NullString]::Value) }
        else { [IO.File]::Move($temp, $path) }
    } finally {
        [IO.File]::Delete($temp)
    }
}

function Read-AreaPreset([string]$Directory, [int]$Slot, [Drawing.Rectangle]$Desktop) {
    $path = Get-PresetFile $Directory $Slot
    if (-not [IO.File]::Exists($path)) { throw '이 슬롯에 저장된 프리셋이 없습니다.' }
    try { $payload = [IO.File]::ReadAllText($path) | ConvertFrom-Json -ErrorAction Stop }
    catch { throw '프리셋 파일을 읽을 수 없습니다. 영역을 다시 지정해 저장하세요.' }
    if ($payload.Version -ne 1) { throw '지원하지 않는 프리셋 형식입니다.' }
    $preset = @{
        Anchor = ConvertFrom-PresetRect $payload.Anchor $Desktop
        Quiz = ConvertFrom-PresetRect $payload.Quiz $Desktop
    }
    if ($null -eq $preset.Anchor -and $null -eq $preset.Quiz) {
        throw '이 프리셋에는 저장된 영역이 없습니다.'
    }
    return $preset
}
