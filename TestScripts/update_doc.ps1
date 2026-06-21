$file = 'e:\AutoScript\FlowAuto\TestScripts\测试方法与文档.md'
$content = [System.IO.File]::ReadAllText($file, [System.Text.Encoding]::UTF8)

# Replace old file references
$content = $content -replace 'FlowAuto_TestSuite\.html', 'FlowAuto_TestSuite_v2.html'

# Replace table of contents old links
$content = $content -replace '场景2: 图片显隐 — WaitCondition', '场景2: HSV 运动检测 — ColorMotion'
$content = $content -replace '场景3: 文本识别 — OCR / OCRContain', '场景3: 细杆方向检测 — ColorMotion DirectionDetect'
$content = $content -replace '场景4: 颜色检测 — ColorCal', '场景4: 颜色计算 — ColorCal'
$content = $content -replace '场景5: 方向检测 — ColorMotion DirectionDetect', '场景5: 状态变化 — ColorMotion StateChange'

[System.IO.File]::WriteAllText($file, $content, [System.Text.Encoding]::UTF8)
Write-Host 'Basic replacements done'
