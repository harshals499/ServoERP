$ErrorActionPreference = 'Stop'
# Passwords arrive on stdin, never in arguments, files or logs. Render one page locally; no OCR/classification.
$request = [Console]::In.ReadToEnd() | ConvertFrom-Json
Add-Type -AssemblyName System.Runtime.WindowsRuntime
$null = [Windows.Storage.StorageFile,Windows.Storage,ContentType=WindowsRuntime]
$null = [Windows.Data.Pdf.PdfDocument,Windows.Data.Pdf,ContentType=WindowsRuntime]
$null = [Windows.Storage.Streams.InMemoryRandomAccessStream,Windows.Storage.Streams,ContentType=WindowsRuntime]
$asTask = [System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object { $_.Name -eq 'AsTask' -and $_.IsGenericMethod -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1' } | Select-Object -First 1
function AwaitResult($Operation, [Type]$ResultType) {
    $asTask.MakeGenericMethod($ResultType).Invoke($null,@($Operation)).GetAwaiter().GetResult()
}
function AwaitAction($Operation) {
    $method = [System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object { $_.Name -eq 'AsTask' -and -not $_.IsGenericMethod -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncAction' } | Select-Object -First 1
    $null = $method.Invoke($null,@($Operation)).GetAwaiter().GetResult()
}
try {
    $file = AwaitResult ([Windows.Storage.StorageFile]::GetFileFromPathAsync([string]$request.Path)) ([Windows.Storage.StorageFile])
    try {
        $pdf = AwaitResult ([Windows.Data.Pdf.PdfDocument]::LoadFromFileAsync($file,[string]$request.Password)) ([Windows.Data.Pdf.PdfDocument])
    } catch {
        $code = $_.Exception.ToString()
        if ($code -match '8007052B|80070056|password') { @{Error='Password required or incorrect'} | ConvertTo-Json -Compress; exit 0 }
        throw
    }
    if ($pdf.PageCount -lt 1 -or [int]$request.Page -lt 1 -or [int]$request.Page -gt $pdf.PageCount) { throw 'Invalid page' }
    $page = $pdf.GetPage([uint32]([int]$request.Page - 1))
    $stream = New-Object Windows.Storage.Streams.InMemoryRandomAccessStream
    try {
        $options = New-Object Windows.Data.Pdf.PdfPageRenderOptions
        $scale = [Math]::Min(1800.0 / [Math]::Max(1,$page.Size.Width),2400.0 / [Math]::Max(1,$page.Size.Height))
        $options.DestinationWidth = [uint32][Math]::Max(1,[Math]::Round($page.Size.Width * $scale))
        $options.DestinationHeight = [uint32][Math]::Max(1,[Math]::Round($page.Size.Height * $scale))
        AwaitAction ($page.RenderToStreamAsync($stream,$options))
        $stream.Seek(0)
        $reader = [System.IO.WindowsRuntimeStreamExtensions]::AsStreamForRead($stream)
        $target = [IO.File]::Create([string]$request.Output)
        try { $reader.CopyTo($target) } finally { $target.Dispose() }
        @{Pages=[int]$pdf.PageCount; Image=[string]$request.Output} | ConvertTo-Json -Compress
    } finally { $stream.Dispose(); $page.Dispose() }
} catch {
    # Do not expose source content, password or raw exceptions.
    @{Error='PDF could not be opened. Retry, enter a password or skip this file.'} | ConvertTo-Json -Compress
}
