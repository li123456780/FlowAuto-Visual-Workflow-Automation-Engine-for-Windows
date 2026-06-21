# FlowAuto 测试 GIF 录制脚本
# 用法: 在测试页面打开后运行此脚本
# 会每隔 300ms 截取一次屏幕，共截取 30 帧 (~9秒)

param(
    [int]$FrameCount = 30,
    [int]$IntervalMs = 300,
    [string]$OutputDir = "d:\AutoScript\FlowAuto\TestScripts\gifs\frames"
)

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

# 创建输出目录
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  FlowAuto GIF Frame Capture" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Frames: $FrameCount | Interval: ${IntervalMs}ms | Output: $OutputDir" -ForegroundColor Yellow
Write-Host ""
Write-Host "Please make sure the FlowAuto Test Suite page is visible!" -ForegroundColor Green
Write-Host "Starting in 3 seconds..." -ForegroundColor Yellow

Start-Sleep -Seconds 3

$primary = [System.Windows.Forms.Screen]::PrimaryScreen
$bounds = $primary.Bounds

Write-Host "Screen: $($bounds.Width)x$($bounds.Height)" -ForegroundColor Gray

for ($i = 0; $i -lt $FrameCount; $i++) {
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    
    $bmp = New-Object System.Drawing.Bitmap($bounds.Width, $bounds.Height)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($bounds.X, $bounds.Y, 0, 0, $bounds.Size)
    
    $filename = "$OutputDir\frame_$($i.ToString('D4')).png"
    $bmp.Save($filename, [System.Drawing.Imaging.ImageFormat]::Png)
    
    $g.Dispose()
    $bmp.Dispose()
    
    $elapsed = $sw.ElapsedMilliseconds
    $remaining = [Math]::Max(0, $IntervalMs - $elapsed)
    
    Write-Host "[$($i+1)/$FrameCount] Captured: $filename (${elapsed}ms)" -ForegroundColor DarkGray
    
    if ($i -lt $FrameCount - 1) {
        Start-Sleep -Milliseconds $remaining
    }
}

Write-Host ""
Write-Host "✅ All frames captured!" -ForegroundColor Green
Write-Host "📁 Location: $OutputDir" -ForegroundColor Green
Write-Host ""
Write-Host "To create GIF, use one of:" -ForegroundColor Yellow
Write-Host "  1. ScreenToGif (Recommended): https://www.screentogif.com/" -ForegroundColor White
Write-Host "  2. FFmpeg: ffmpeg -framerate 10 -i frame_%04d.png -vf ""fps=10,scale=800:-1:flags=lanczos,split[s0][s1];[s0]palettegen[p];[s1][p]paletteuse"" output.gif" -ForegroundColor White
Write-Host "  3. Online: https://ezgif.com/maker" -ForegroundColor White
