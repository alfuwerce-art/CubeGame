# alpha_window.ps1 — window control for the LOCAL research copy of Alpha (.scratch only).
# Dot-source:  . tools/alpha_models/runtime/alpha_window.ps1
# Functions:   Start-Alpha, Get-AlphaWindow, Focus-Alpha, Save-AlphaShot, Click-Alpha, Key-Alpha,
#              Hold-Alpha, Mouse-Alpha
# Coordinates for Click-Alpha are CLIENT coordinates (0,0 = top-left of the game picture).
# Nothing here writes to the game folder except the Save backup made by Start-Alpha.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
if (-not ("AlphaWin" -as [type])) {
Add-Type @"
using System;
using System.Text;
using System.Runtime.InteropServices;
public class AlphaWin {
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr h, ref POINT p);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int c);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint a, uint b, bool f);
  [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint a, uint b, uint c, UIntPtr e);
  [DllImport("user32.dll")] public static extern uint MapVirtualKey(uint c, uint t);
  [DllImport("user32.dll", SetLastError=true)] public static extern uint SendInput(uint n, INPUT[] i, int s);
  public struct RECT { public int L,T,R,B; }
  public struct POINT { public int X,Y; }
  [StructLayout(LayoutKind.Sequential)] public struct MOUSEINPUT { public int dx; public int dy; public uint d; public uint f; public uint t; public IntPtr e; }
  [StructLayout(LayoutKind.Sequential)] public struct KEYBDINPUT { public ushort wVk; public ushort wScan; public uint dwFlags; public uint t; public IntPtr e; }
  [StructLayout(LayoutKind.Explicit)] public struct INPUTUNION { [FieldOffset(0)] public MOUSEINPUT mi; [FieldOffset(0)] public KEYBDINPUT ki; }
  [StructLayout(LayoutKind.Sequential)] public struct INPUT { public uint type; public INPUTUNION u; }
  public static IntPtr Find(uint pid) {
    IntPtr best = IntPtr.Zero; int bestW = 0;
    EnumWindows((h,l)=>{ uint p; GetWindowThreadProcessId(h,out p);
      if (p==pid && IsWindowVisible(h)) { RECT r; GetWindowRect(h,out r); int w=r.R-r.L; if (w>bestW){bestW=w;best=h;} }
      return true; }, IntPtr.Zero);
    return bestW > 600 ? best : IntPtr.Zero;
  }
  public static bool Focus(IntPtr t) {
    IntPtr fg = GetForegroundWindow(); uint pid; uint th = GetWindowThreadProcessId(fg, out pid);
    uint my = GetCurrentThreadId();
    AttachThreadInput(my, th, true); bool ok = SetForegroundWindow(t); AttachThreadInput(my, th, false);
    return GetForegroundWindow() == t;
  }
  public static uint Key(ushort vk, bool up) {
    INPUT[] inp = new INPUT[1]; inp[0].type = 1;
    inp[0].u.ki.wScan = (ushort)MapVirtualKey(vk, 0);
    inp[0].u.ki.dwFlags = 0x0008u | (up ? 0x0002u : 0u);
    return SendInput(1, inp, Marshal.SizeOf(typeof(INPUT)));
  }
  public static uint MouseBtn(uint flag) {
    INPUT[] inp = new INPUT[1]; inp[0].type = 0; inp[0].u.mi.f = flag;
    return SendInput(1, inp, Marshal.SizeOf(typeof(INPUT)));
  }
}
"@
}
$script:AlphaGame = 'D:\Projects\CubeGame\.scratch\alphatest2'

function Start-Alpha([string]$Game = $script:AlphaGame, [string]$BackupRoot = 'D:\Projects\CubeGame\.scratch\s05a2\save_backups') {
  if (-not (Get-Process Cube -ErrorAction SilentlyContinue)) {
    $save = Join-Path $Game 'Save'
    if (Test-Path $save) {
      $dst = Join-Path $BackupRoot ('save_' + (Get-Date -Format 'yyyyMMdd_HHmmss'))
      New-Item -ItemType Directory -Force -Path $dst | Out-Null
      Copy-Item -Recurse $save $dst
    }
    Start-Process -FilePath (Join-Path $Game 'Cube.exe') -WorkingDirectory $Game | Out-Null
  }
  $deadline = (Get-Date).AddSeconds(60)
  while ((Get-Date) -lt $deadline) {
    $p = Get-Process Cube -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($p) { $h = [AlphaWin]::Find([uint32]$p.Id); if ($h -ne [IntPtr]::Zero) { return $p.Id } }
    Start-Sleep -Milliseconds 500
  }
  throw 'Alpha window did not appear within 60 s'
}
function Get-AlphaWindow {
  $p = Get-Process Cube -ErrorAction SilentlyContinue | Select-Object -First 1
  if (-not $p) { throw 'Cube not running' }
  $h = [AlphaWin]::Find([uint32]$p.Id)
  if ($h -eq [IntPtr]::Zero) { throw 'Cube window not found' }
  $h
}
function Focus-Alpha { $h = Get-AlphaWindow; [void][AlphaWin]::ShowWindow($h, 9); for ($i=0;$i -lt 5;$i++){ [AlphaWin]::Key(0x12,$false)|Out-Null; [AlphaWin]::Key(0x12,$true)|Out-Null; if ([AlphaWin]::Focus($h)) { return $true }; Start-Sleep -Milliseconds 200 }; return $false }
function Get-AlphaClient { $h = Get-AlphaWindow; $r = New-Object AlphaWin+RECT; [void][AlphaWin]::GetClientRect($h,[ref]$r); $p = New-Object AlphaWin+POINT; [void][AlphaWin]::ClientToScreen($h,[ref]$p); [pscustomobject]@{ X=$p.X; Y=$p.Y; W=$r.R; H=$r.B } }
function Save-AlphaShot([string]$Path, [double]$Scale = 0.5) {
  $c = Get-AlphaClient
  $bmp = New-Object System.Drawing.Bitmap $c.W, $c.H
  $g = [System.Drawing.Graphics]::FromImage($bmp); $g.CopyFromScreen($c.X, $c.Y, 0, 0, $bmp.Size); $g.Dispose()
  $w = [int]($c.W*$Scale); $hh = [int]($c.H*$Scale)
  $small = New-Object System.Drawing.Bitmap $bmp, $w, $hh
  New-Item -ItemType Directory -Force -Path (Split-Path $Path) | Out-Null
  $small.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png); $small.Dispose(); $bmp.Dispose()
  "SHOT $Path client=$($c.W)x$($c.H) scale=$Scale"
}
function Click-Alpha([int]$X, [int]$Y) { $c = Get-AlphaClient; [AlphaWin]::SetCursorPos($c.X+$X, $c.Y+$Y) | Out-Null; Start-Sleep -Milliseconds 150; [AlphaWin]::mouse_event(2,0,0,0,[UIntPtr]::Zero); Start-Sleep -Milliseconds 80; [AlphaWin]::mouse_event(4,0,0,0,[UIntPtr]::Zero) }
function Key-Alpha([int]$Vk, [int]$HoldMs = 80) { [AlphaWin]::Key($Vk,$false) | Out-Null; Start-Sleep -Milliseconds $HoldMs; [AlphaWin]::Key($Vk,$true) | Out-Null }
function Mouse-Alpha([string]$Button = 'left', [int]$HoldMs = 80) { $d = @{left=2;right=8}[$Button]; [AlphaWin]::MouseBtn($d) | Out-Null; Start-Sleep -Milliseconds $HoldMs; [AlphaWin]::MouseBtn($d*2) | Out-Null }
