param(
    [string]$OutputPath = (Join-Path $PSScriptRoot '..\assets\app.ico')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$output = [System.IO.Path]::GetFullPath($OutputPath)
$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (-not $output.StartsWith($projectRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'The icon output must stay inside the project directory.'
}

$outputDirectory = [System.IO.Path]::GetDirectoryName($output)
[System.IO.Directory]::CreateDirectory($outputDirectory) | Out-Null
$sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
$images = [System.Collections.Generic.List[byte[]]]::new()

foreach ($size in $sizes) {
    $bitmap = [System.Drawing.Bitmap]::new($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $margin = [Math]::Max(1, [Math]::Round($size * 0.07))
        $ringMargin = [Math]::Max(2, [Math]::Round($size * 0.17))
        $stroke = [Math]::Max(2, [Math]::Round($size * 0.12))
        $background = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 35, 38, 45))
        $track = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255, 75, 80, 90), $stroke)
        $progress = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255, 32, 180, 110), $stroke)
        try {
            $progress.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
            $progress.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
            $graphics.FillEllipse($background, $margin, $margin, $size - (2 * $margin), $size - (2 * $margin))
            $diameter = $size - (2 * $ringMargin)
            $graphics.DrawEllipse($track, $ringMargin, $ringMargin, $diameter, $diameter)
            $graphics.DrawArc($progress, $ringMargin, $ringMargin, $diameter, $diameter, -90, 241)
        }
        finally {
            $background.Dispose()
            $track.Dispose()
            $progress.Dispose()
        }

        $stream = [System.IO.MemoryStream]::new()
        try {
            $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
            $images.Add($stream.ToArray())
        }
        finally {
            $stream.Dispose()
        }
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

$file = [System.IO.FileStream]::new($output, [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write)
$writer = [System.IO.BinaryWriter]::new($file)
try {
    $writer.Write([uint16]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]$images.Count)
    $offset = 6 + (16 * $images.Count)

    for ($index = 0; $index -lt $images.Count; $index++) {
        $size = $sizes[$index]
        $writer.Write([byte]$(if ($size -eq 256) { 0 } else { $size }))
        $writer.Write([byte]$(if ($size -eq 256) { 0 } else { $size }))
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]32)
        $writer.Write([uint32]$images[$index].Length)
        $writer.Write([uint32]$offset)
        $offset += $images[$index].Length
    }

    foreach ($image in $images) {
        $writer.Write($image)
    }
}
finally {
    $writer.Dispose()
    $file.Dispose()
}

Write-Output "Generated $output"
