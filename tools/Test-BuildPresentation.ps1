param([ValidatePattern('^[a-zA-Z0-9_-]+$')][string]$Fixture = 'aspect-v103-check')
$ErrorActionPreference = 'Stop'
$project = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$root = Join-Path $project ('private/' + $Fixture)
if (Test-Path -LiteralPath (Join-Path $root 'Techno Kitten Adventure XBLA Recomp/Game')) {
    $root = Join-Path $root 'Techno Kitten Adventure XBLA Recomp'
}
$game = Join-Path $root 'Game'
$exe = Join-Path $game 'Techno Kitten Adventure.exe'
$hostExe = Join-Path $game 'resources/game/Techno Kitten Adventure.exe'
$splitRuntime = Test-Path -LiteralPath $hostExe
Add-Type -AssemblyName System.Drawing
if (-not ('TkaWindowIcon' -as [type])) {
    Add-Type @'
using System;
using System.Text;
using System.Runtime.InteropServices;
public static class TkaWindowIcon {
    [DllImport("user32.dll", EntryPoint="SendMessageW")] public static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    delegate bool Visitor(IntPtr window, IntPtr data);
    [DllImport("user32.dll")] static extern bool EnumWindows(Visitor callback, IntPtr data);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32.dll", EntryPoint="GetWindowTextW", CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr window, StringBuilder text, int capacity);
    public static IntPtr Find(int process) {
        IntPtr result=IntPtr.Zero;
        EnumWindows((window,data) => {
            GetWindowThreadProcessId(window,out uint owner);
            if(owner != process) return true;
            var title=new StringBuilder(256); GetWindowText(window,title,title.Capacity);
            if(title.ToString()=="Techno Kitten Adventure!") { result=window; return false; }
            return true;
        },IntPtr.Zero);
        return result;
    }
}
'@
}
$start = [Diagnostics.ProcessStartInfo]::new($(if ($splitRuntime) { $hostExe } else { $exe }))
$start.UseShellExecute = $false; $start.CreateNoWindow = $true; $start.WorkingDirectory = $project
if ($splitRuntime) {
    $start.ArgumentList.Add('--game-root'); $start.ArgumentList.Add($game)
}
foreach ($arg in @((Join-Path $game 'Helicopter.dll'), (Join-Path $game 'Content'), '9', '--fixed60', '--scripted')) { $start.ArgumentList.Add($arg) }
$began = Get-Date
$process = [Diagnostics.Process]::Start($start)
try {
    $window = [IntPtr]::Zero
    for ($i = 0; $i -lt 20 -and $window -eq [IntPtr]::Zero; $i++) {
        Start-Sleep -Milliseconds 250
        $window = [TkaWindowIcon]::Find($process.Id)
    }
    if ($window -eq [IntPtr]::Zero) { throw 'Game window not found.' }
    $handle = [IntPtr]::Zero
    # MonoGame shows a window during graphics setup, before host branding is applied.
    for ($i = 0; $i -lt 16 -and $handle -eq [IntPtr]::Zero; $i++) {
        Start-Sleep -Milliseconds 250
        $window = [TkaWindowIcon]::Find($process.Id)
        $handle = [TkaWindowIcon]::SendMessage($window, 0x7F, [IntPtr]::new(1), [IntPtr]::Zero) # WM_GETICON / ICON_BIG
    }
    if ($handle -eq [IntPtr]::Zero) { throw 'Game window has no explicit icon.' }
    $actual = [Drawing.Icon]::FromHandle($handle).ToBitmap()
    $expectedIcon = [Drawing.Icon]::ExtractAssociatedIcon($exe)
    $expected = $expectedIcon.ToBitmap()
    try {
        if ($actual.Size -ne $expected.Size) { throw 'Window icon size differs from the embedded game icon.' }
        for ($y = 0; $y -lt $actual.Height; $y++) {
            for ($x = 0; $x -lt $actual.Width; $x++) {
                if ($actual.GetPixel($x,$y).ToArgb() -ne $expected.GetPixel($x,$y).ToArgb()) { throw 'Live window icon does not match the game EXE.' }
            }
        }
        $actual.Save((Join-Path $game 'logs/live-game-icon.png'))
    } finally { $actual.Dispose(); $expected.Dispose(); $expectedIcon.Dispose() }
    if (-not $process.WaitForExit(20000)) { $process.Kill(); throw 'Game presentation check timed out.' }
    if ($process.ExitCode -ne 0) { throw 'Game presentation check failed.' }
} finally { if (-not $process.HasExited) { $process.Kill() }; $process.Dispose() }
$captureLogs = if ($splitRuntime) { Join-Path $game 'resources/game/logs' } else { Join-Path $game 'logs' }
$frames = Get-ChildItem -LiteralPath $captureLogs -Directory -Filter 'frames-*' | Where-Object LastWriteTime -ge $began | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $frames) { throw 'No new captured frames.' }
$results = @()
foreach ($file in Get-ChildItem -LiteralPath $frames.FullName -Filter '*.png' | Sort-Object Name) {
    $bitmap = [Drawing.Bitmap]::FromFile($file.FullName)
    try {
        # The pixel version label's cyan underline at logical 720p coordinates.
        $stem = $true
        foreach ($y in @(693,694)) {
            $color = $bitmap.GetPixel(26,$y)
            $stem = $stem -and $color.R -eq 69 -and $color.G -eq 233 -and $color.B -eq 245
        }
        $shouldShow = $file.Name -eq '01-OPENING.png'
        if ($stem -ne $shouldShow) { throw ('Title-only badge signature failed: ' + $file.Name) }
        $results += @{ frame = $file.Name; title_badge = $stem; passed = $true }
    } finally { $bitmap.Dispose() }
}
if ($results.Count -ne 3) { throw 'Expected splash, title and post-title frames.' }
@{ live_window_icon_matches_exe = $true; frames = $results } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $project ('out/' + $Fixture + '-presentation.json'))
Write-Output 'PASS: live window icon matches game EXE; version badge is title-only.'
