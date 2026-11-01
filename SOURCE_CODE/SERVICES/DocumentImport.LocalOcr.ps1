param([Parameter(Mandatory=$true)][string]$InputPath,[Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
# Local Windows Runtime only. Uploaded text is never evaluated or executed.
Add-Type -AssemblyName System.Runtime.WindowsRuntime
$null = [Windows.Storage.StorageFile,Windows.Storage,ContentType=WindowsRuntime]
$null = [Windows.Storage.Streams.InMemoryRandomAccessStream,Windows.Storage.Streams,ContentType=WindowsRuntime]
$null = [Windows.Graphics.Imaging.BitmapDecoder,Windows.Graphics.Imaging,ContentType=WindowsRuntime]
$null = [Windows.Media.Ocr.OcrEngine,Windows.Foundation,ContentType=WindowsRuntime]
$null = [Windows.Data.Pdf.PdfDocument,Windows.Data.Pdf,ContentType=WindowsRuntime]
$asTask = [System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object { $_.Name -eq 'AsTask' -and $_.IsGenericMethod -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1' } | Select-Object -First 1
function AwaitResult($Operation, [Type]$ResultType) {
    $task = $asTask.MakeGenericMethod($ResultType).Invoke($null,@($Operation))
    $task.GetAwaiter().GetResult()
}
function AwaitAction($Operation) {
    $method=[System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object { $_.Name -eq 'AsTask' -and -not $_.IsGenericMethod -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncAction' } | Select-Object -First 1
    $task=$method.Invoke($null,@($Operation))
    $null=$task.GetAwaiter().GetResult()
}
$engine = [Windows.Media.Ocr.OcrEngine]::TryCreateFromUserProfileLanguages()
$file = AwaitResult ([Windows.Storage.StorageFile]::GetFileFromPathAsync($InputPath)) ([Windows.Storage.StorageFile])
$isPdf = [IO.Path]::GetExtension($InputPath) -ieq '.pdf'
$pdf = $null
if ($isPdf) {
    $pdf = AwaitResult ([Windows.Data.Pdf.PdfDocument]::LoadFromFileAsync($file)) ([Windows.Data.Pdf.PdfDocument])
    if ($pdf.PageCount -gt 40) { throw 'Page limit exceeded' }
    $count = $pdf.PageCount
} else { $count = 1 }
$pages = @()
for ($i=0; $i -lt $count; $i++) {
    $page = $null
    $stream = $null
    $bitmap = $null
    try {
        if ($isPdf) {
            $page = $pdf.GetPage($i)
            $stream = New-Object Windows.Storage.Streams.InMemoryRandomAccessStream
            $options = New-Object Windows.Data.Pdf.PdfPageRenderOptions
            $options.DestinationWidth = 1800
            AwaitAction ($page.RenderToStreamAsync($stream,$options))
            $stream.Seek(0)
        } else {
            $stream = AwaitResult ($file.OpenAsync([Windows.Storage.FileAccessMode]::Read)) ([Windows.Storage.Streams.IRandomAccessStream])
        }
        $decoder = AwaitResult ([Windows.Graphics.Imaging.BitmapDecoder]::CreateAsync($stream)) ([Windows.Graphics.Imaging.BitmapDecoder])
        if ($decoder.PixelWidth -gt 10000 -or $decoder.PixelHeight -gt 10000 -or ([long]$decoder.PixelWidth*$decoder.PixelHeight) -gt 40000000) { throw 'Image dimensions exceed import limits' }
        $bitmap = AwaitResult ($decoder.GetSoftwareBitmapAsync()) ([Windows.Graphics.Imaging.SoftwareBitmap])
        $text = ''
        if ($engine -and $bitmap.PixelWidth -le [Windows.Media.Ocr.OcrEngine]::MaxImageDimension -and $bitmap.PixelHeight -le [Windows.Media.Ocr.OcrEngine]::MaxImageDimension) {
            $result = AwaitResult ($engine.RecognizeAsync($bitmap)) ([Windows.Media.Ocr.OcrResult])
            $text = ($result.Lines | ForEach-Object { $_.Text }) -join "`n"
        }
        $imagePath = Join-Path $OutputDirectory ('page-'+($i+1)+'.png')
        if ($isPdf) {
            $stream.Seek(0)
            $netStream = [System.IO.WindowsRuntimeStreamExtensions]::AsStreamForRead($stream)
            $target = [IO.File]::Create($imagePath)
            try { $netStream.CopyTo($target) } finally { $target.Dispose() }
        } else { $imagePath = $InputPath }
        $pages += @{Page=$i+1; Text=$text; ImagePath=$imagePath}
    } finally {
        if ($bitmap) { $bitmap.Dispose() }
        if ($stream) { $stream.Dispose() }
        if ($page) { $page.Dispose() }
    }
}
ConvertTo-Json -InputObject @($pages) -Depth 4 -Compress | Set-Content -LiteralPath (Join-Path $OutputDirectory 'pages.json') -Encoding UTF8
