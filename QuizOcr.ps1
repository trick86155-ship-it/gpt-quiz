function Initialize-QuizOcr {
    try {
        Add-Type -AssemblyName System.Runtime.WindowsRuntime
        $script:OcrEngineType = [Windows.Media.Ocr.OcrEngine,Windows.Foundation,ContentType=WindowsRuntime]
        $script:OcrResultType = [Windows.Media.Ocr.OcrResult,Windows.Foundation,ContentType=WindowsRuntime]
        $script:BitmapDecoderType = [Windows.Graphics.Imaging.BitmapDecoder,Windows.Foundation,ContentType=WindowsRuntime]
        $script:SoftwareBitmapType = [Windows.Graphics.Imaging.SoftwareBitmap,Windows.Foundation,ContentType=WindowsRuntime]
        $script:AsWinRtTask = [System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object {
            $_.Name -eq 'AsTask' -and $_.IsGenericMethod -and
            $_.GetGenericArguments().Count -eq 1 -and $_.GetParameters().Count -eq 1
        } | Select-Object -First 1
        $script:OcrEngine = $script:OcrEngineType::TryCreateFromUserProfileLanguages()
    } catch {
        $script:OcrEngine = $null
    }
}

function Resolve-QuizOcrOperation($Operation, [type]$Type) {
    $task = $script:AsWinRtTask.MakeGenericMethod($Type).Invoke($null, @($Operation))
    return $task.GetAwaiter().GetResult()
}

function Read-QuizScreenText([Drawing.Bitmap]$Image, [switch]$WithBounds) {
    if ($null -eq $script:OcrEngine) { return }
    $stream = New-Object IO.MemoryStream
    try {
        $Image.Save($stream, [Drawing.Imaging.ImageFormat]::Png)
        $stream.Position = 0
        $inputStream = [System.IO.WindowsRuntimeStreamExtensions]::AsRandomAccessStream($stream)
        try {
            $decoder = Resolve-QuizOcrOperation ($script:BitmapDecoderType::CreateAsync($inputStream)) $script:BitmapDecoderType
            $format = [Windows.Graphics.Imaging.BitmapPixelFormat,Windows.Foundation,ContentType=WindowsRuntime]::Bgra8
            $alpha = [Windows.Graphics.Imaging.BitmapAlphaMode,Windows.Foundation,ContentType=WindowsRuntime]::Ignore
            $bitmap = Resolve-QuizOcrOperation ($decoder.GetSoftwareBitmapAsync($format, $alpha)) $script:SoftwareBitmapType
            try {
                $ocr = Resolve-QuizOcrOperation ($script:OcrEngine.RecognizeAsync($bitmap)) $script:OcrResultType
                foreach ($line in $ocr.Lines) {
                    if (-not $WithBounds) { $line.Text; continue }
                    $bounds = [Drawing.Rectangle]::Empty
                    foreach ($word in $line.Words) {
                        $rect = $word.BoundingRect
                        $wordBounds = [Drawing.Rectangle]::FromLTRB([int][Math]::Floor($rect.X),
                            [int][Math]::Floor($rect.Y), [int][Math]::Ceiling($rect.X + $rect.Width),
                            [int][Math]::Ceiling($rect.Y + $rect.Height))
                        $bounds = if ($bounds.IsEmpty) { $wordBounds } else { [Drawing.Rectangle]::Union($bounds, $wordBounds) }
                    }
                    if (-not $bounds.IsEmpty) { @{ Text = $line.Text; Bounds = $bounds } }
                }
            } finally { $bitmap.Dispose() }
        } finally { $inputStream.Dispose() }
    } finally { $stream.Dispose() }
}

function Get-VideoQuizKey([string[]]$Lines) {
    $questions = foreach ($line in $Lines) {
        $clean = $line.Trim()
        if ($clean -match '^(?:Q\s*\.?\s*\d+|문제\s*\d+|\d+\s*번\s*문제)\s*[.:)．]?' -and $clean.Length -ge 5) {
            $clean
        } elseif ($clean -match '(?:문장|보기|[OX]\s*/\s*[OX]|[OX]\s*칸)' -and
                  $clean -match '(?:드래그|끌어|옮기|옮겨)') {
            $clean
        } elseif ($clean -match '(?:무엇|무슨|어떤|어느|누구|몇\s*[개명번]|얼마|어디|찾아보세요|골라보세요|고르세요|맞혀\s*보세요|맞춰\s*보세요|선택해\s*주세요|what\b|which\b|who\b|where\b|how many\b)' -and
                  $clean -match '(?:[?？]|주세요|보세요|고르세요|인지\s*선택)') {
            $clean
        }
    }
    if (-not $questions) { return '' }
    $options = $Lines | Where-Object { $_ -match '^\s*(?:[①-⑩]|[1-9A-Da-d][.)：:]\s*|[1-9]\s+\S)' }
    # Ignore spacing variations from OCR and exclude countdowns and subtitles.
    return ((@($questions) + @($options)) -join '|') -replace '[\s\u200B]+',''
}
