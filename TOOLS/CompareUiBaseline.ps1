param(
    [Parameter(Mandatory = $true)]
    [string]$BaselinePath,

    [Parameter(Mandatory = $true)]
    [string]$CurrentPath,

    [string]$DiffPath = "",
    [double]$MaxDiffRatio = 0.015
)

$ErrorActionPreference = "Stop"

function Write-FailJson {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Reason,

        [double]$DiffRatio = 1
    )

    [pscustomobject]@{
        Result = "FAIL"
        Reason = $Reason
        DiffRatio = $DiffRatio
    } | ConvertTo-Json -Compress
}

$exitCode = 0

try {
    Add-Type -AssemblyName System.Drawing

    if (-not ("ServoErp.UiQa.BaselineComparison" -as [type])) {
        Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @"
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace ServoErp.UiQa
{
    public sealed class BaselineComparisonResult
    {
        public string Result { get; set; }
        public string Reason { get; set; }
        public double DiffRatio { get; set; }
        public double MaxDiffRatio { get; set; }
        public int ChangedPixels { get; set; }
        public int TotalPixels { get; set; }
        public int BaselineWidth { get; set; }
        public int BaselineHeight { get; set; }
        public int CurrentWidth { get; set; }
        public int CurrentHeight { get; set; }
    }

    public static class BaselineComparison
    {
        private const int DifferenceThreshold = 30;

        public static BaselineComparisonResult Compare(string baselinePath, string currentPath, string diffPath, double maxDiffRatio)
        {
            using (Bitmap baseline = Load32bppBitmap(baselinePath))
            using (Bitmap current = Load32bppBitmap(currentPath))
            {
                if (baseline.Width != current.Width || baseline.Height != current.Height)
                {
                    return new BaselineComparisonResult
                    {
                        Result = "FAIL",
                        Reason = "Dimension mismatch",
                        BaselineWidth = baseline.Width,
                        BaselineHeight = baseline.Height,
                        CurrentWidth = current.Width,
                        CurrentHeight = current.Height,
                        DiffRatio = 1,
                        MaxDiffRatio = maxDiffRatio,
                        ChangedPixels = baseline.Width * baseline.Height,
                        TotalPixels = baseline.Width * baseline.Height
                    };
                }

                PixelBuffer baselinePixels = CopyPixels(baseline);
                PixelBuffer currentPixels = CopyPixels(current);
                bool writeDiff = !string.IsNullOrWhiteSpace(diffPath);
                byte[] diffBytes = writeDiff ? new byte[currentPixels.Bytes.Length] : null;
                int changed = ComparePixels(baselinePixels, currentPixels, baseline.Width, baseline.Height, writeDiff, diffBytes);

                if (writeDiff)
                {
                    SaveDiff(diffPath, diffBytes, baseline.Width, baseline.Height);
                }

                int total = baseline.Width * baseline.Height;
                double ratio = changed / (double)total;
                bool passed = ratio <= maxDiffRatio;

                return new BaselineComparisonResult
                {
                    Result = passed ? "PASS" : "FAIL",
                    Reason = passed ? "Within threshold" : "Pixel difference above threshold",
                    DiffRatio = ratio,
                    MaxDiffRatio = maxDiffRatio,
                    ChangedPixels = changed,
                    TotalPixels = total
                };
            }
        }

        private static Bitmap Load32bppBitmap(string path)
        {
            Bitmap source = null;
            Bitmap clone = null;
            Graphics graphics = null;

            try
            {
                source = (Bitmap)Image.FromFile(path);
                clone = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
                graphics = Graphics.FromImage(clone);
                graphics.DrawImage(source, 0, 0, source.Width, source.Height);
                return clone;
            }
            catch
            {
                if (clone != null)
                {
                    clone.Dispose();
                }

                throw;
            }
            finally
            {
                if (graphics != null)
                {
                    graphics.Dispose();
                }

                if (source != null)
                {
                    source.Dispose();
                }
            }
        }

        private static PixelBuffer CopyPixels(Bitmap bitmap)
        {
            Rectangle rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            BitmapData data = null;

            try
            {
                data = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                int stride = Math.Abs(data.Stride);
                byte[] bytes = new byte[stride * bitmap.Height];
                Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);

                return new PixelBuffer
                {
                    Bytes = bytes,
                    Stride = stride
                };
            }
            finally
            {
                if (data != null)
                {
                    bitmap.UnlockBits(data);
                }
            }
        }

        private static int ComparePixels(PixelBuffer baseline, PixelBuffer current, int width, int height, bool writeDiff, byte[] diffBytes)
        {
            int changed = 0;

            for (int y = 0; y < height; y++)
            {
                int rowOffset = y * baseline.Stride;

                for (int x = 0; x < width; x++)
                {
                    int offset = rowOffset + (x * 4);
                    int blueDistance = Math.Abs(baseline.Bytes[offset] - current.Bytes[offset]);
                    int greenDistance = Math.Abs(baseline.Bytes[offset + 1] - current.Bytes[offset + 1]);
                    int redDistance = Math.Abs(baseline.Bytes[offset + 2] - current.Bytes[offset + 2]);
                    int distance = redDistance + greenDistance + blueDistance;

                    if (distance > DifferenceThreshold)
                    {
                        changed++;

                        if (writeDiff)
                        {
                            diffBytes[offset] = 0;
                            diffBytes[offset + 1] = 0;
                            diffBytes[offset + 2] = 255;
                            diffBytes[offset + 3] = 255;
                        }
                    }
                    else if (writeDiff)
                    {
                        byte gray = (byte)((current.Bytes[offset] + current.Bytes[offset + 1] + current.Bytes[offset + 2]) / 3);
                        diffBytes[offset] = gray;
                        diffBytes[offset + 1] = gray;
                        diffBytes[offset + 2] = gray;
                        diffBytes[offset + 3] = 255;
                    }
                }
            }

            return changed;
        }

        private static void SaveDiff(string diffPath, byte[] diffBytes, int width, int height)
        {
            string directory = Path.GetDirectoryName(diffPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using (Bitmap diff = new Bitmap(width, height, PixelFormat.Format32bppArgb))
            {
                Rectangle rect = new Rectangle(0, 0, width, height);
                BitmapData data = null;

                try
                {
                    data = diff.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                    int length = Math.Abs(data.Stride) * height;
                    Marshal.Copy(diffBytes, 0, data.Scan0, length);
                }
                finally
                {
                    if (data != null)
                    {
                        diff.UnlockBits(data);
                    }
                }

                diff.Save(diffPath, ImageFormat.Png);
            }
        }

        private sealed class PixelBuffer
        {
            public byte[] Bytes { get; set; }
            public int Stride { get; set; }
        }
    }
}
"@
    }

    if (-not (Test-Path -LiteralPath $BaselinePath)) {
        Write-FailJson -Reason "Baseline not found: $BaselinePath"
        $exitCode = 1
    }
    elseif (-not (Test-Path -LiteralPath $CurrentPath)) {
        Write-FailJson -Reason "Current image not found: $CurrentPath"
        $exitCode = 1
    }
    else {
        $result = [ServoErp.UiQa.BaselineComparison]::Compare($BaselinePath, $CurrentPath, $DiffPath, $MaxDiffRatio)

        if ($result.Result -eq "FAIL" -and $result.Reason -eq "Dimension mismatch") {
            [pscustomobject]@{
                Result = $result.Result
                Reason = $result.Reason
                BaselineWidth = $result.BaselineWidth
                BaselineHeight = $result.BaselineHeight
                CurrentWidth = $result.CurrentWidth
                CurrentHeight = $result.CurrentHeight
                DiffRatio = $result.DiffRatio
            } | ConvertTo-Json -Compress
        }
        else {
            [pscustomobject]@{
                Result = $result.Result
                Reason = $result.Reason
                DiffRatio = $result.DiffRatio
                MaxDiffRatio = $result.MaxDiffRatio
                ChangedPixels = $result.ChangedPixels
                TotalPixels = $result.TotalPixels
            } | ConvertTo-Json -Compress
        }

        if ($result.Result -eq "FAIL") {
            $exitCode = 1
        }
    }
}
catch {
    Write-FailJson -Reason ("Comparison failed: " + $_.Exception.Message)
    $exitCode = 1
}

exit $exitCode
