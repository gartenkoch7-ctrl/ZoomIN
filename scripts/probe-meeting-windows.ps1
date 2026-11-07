#Requires -Version 5.1
<#
.SYNOPSIS
    Lists the top-level windows of Teams (class, title, rectangle, styles, topmost, cloaked) and
    the UI Automation names of its visible windows. Read-only: no hooks, no injection (ADR-0018).
.DESCRIPTION
    Run once without sharing and once each while sharing a screen and a window in a meeting; the
    differences show the share frame and toolbar. Results go to docs/kb/freigabe-filter.md.
.EXAMPLE
    ./scripts/probe-meeting-windows.ps1 -Out probe-screen.txt
#>
param([string]$Out = (Join-Path (Get-Location) "meeting-windows.txt"))
Add-Type -TypeDefinition @"
using System; using System.Text; using System.Runtime.InteropServices; using System.Collections.Generic;
public static class P {
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc p, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr parent, EnumProc p, IntPtr l);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetClassName(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll", EntryPoint="GetWindowLongPtrW")] public static extern IntPtr GetWindowLongPtr(IntPtr h, int i);
  [DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr h, uint cmd);
  [DllImport("user32.dll")] public static extern bool GetWindowDisplayAffinity(IntPtr h, out uint a);
  [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int a, out int v, int s);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
  public static List<IntPtr> Top() { var l = new List<IntPtr>(); EnumWindows((h, x) => { l.Add(h); return true; }, IntPtr.Zero); return l; }
  public static List<IntPtr> Kids(IntPtr p) { var l = new List<IntPtr>(); EnumChildWindows(p, (h, x) => { l.Add(h); return true; }, IntPtr.Zero); return l; }
}
"@
$teams = Get-Process | Where-Object { $_.ProcessName -match '^(ms-teams|Teams|msedgewebview2)$' }
$pids = @{}; foreach ($p in $teams) { $pids[[uint32]$p.Id] = $p.ProcessName }
$lines = New-Object System.Collections.Generic.List[string]
$t = Get-Process ms-teams -ErrorAction SilentlyContinue | Select-Object -First 1
if ($t) { $lines.Add("Teams: " + $t.MainModule.FileVersionInfo.ProductVersion + " " + $t.Path) }
$lines.Add("Time: " + (Get-Date -Format o))
function Describe($h, $indent) {
  $pid0 = 0; [void][P]::GetWindowThreadProcessId($h, [ref]$pid0)
  $cls = New-Object System.Text.StringBuilder 256; [void][P]::GetClassName($h, $cls, 256)
  $tit = New-Object System.Text.StringBuilder 512; [void][P]::GetWindowText($h, $tit, 512)
  $r = New-Object P+RECT; [void][P]::GetWindowRect($h, [ref]$r)
  $style = [P]::GetWindowLongPtr($h, -16).ToInt64(); $ex = [P]::GetWindowLongPtr($h, -20).ToInt64()
  $owner = [P]::GetWindow($h, 4)
  $cloak = 0; [void][P]::DwmGetWindowAttribute($h, 14, [ref]$cloak, 4)
  $aff = 0; [void][P]::GetWindowDisplayAffinity($h, [ref]$aff)
  $topmost = ($ex -band 0x8) -ne 0
  "{0}0x{1:X} pid={2}({3}) vis={4} cloak={5} cls='{6}' title='{7}' rect=({8},{9},{10},{11}) {12}x{13} style=0x{14:X} ex=0x{15:X} topmost={16} owner=0x{17:X} affinity=0x{18:X}" -f `
    $indent, $h.ToInt64(), $pid0, $pids[[uint32]$pid0], [P]::IsWindowVisible($h), $cloak, $cls, $tit, $r.L, $r.T, $r.R, $r.B, ($r.R-$r.L), ($r.B-$r.T), $style, $ex, $topmost, $owner.ToInt64(), $aff
}
$z = 0
foreach ($h in [P]::Top()) {
  $z++
  $pid0 = 0; [void][P]::GetWindowThreadProcessId($h, [ref]$pid0)
  if (-not $pids.ContainsKey([uint32]$pid0)) { continue }
  $lines.Add("z=$z " + (Describe $h ""))
  if ([P]::IsWindowVisible($h)) { foreach ($c in [P]::Kids($h)) { $lines.Add((Describe $c "    ")) } }
}
# UIA names of visible Teams top-level windows (read-only)
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
foreach ($h in [P]::Top()) {
  $pid0 = 0; [void][P]::GetWindowThreadProcessId($h, [ref]$pid0)
  if (-not $pids.ContainsKey([uint32]$pid0) -or -not [P]::IsWindowVisible($h)) { continue }
  try {
    $e = [System.Windows.Automation.AutomationElement]::FromHandle($h)
    $lines.Add(("UIA 0x{0:X}: name='{1}' type={2} automationId='{3}' class='{4}'" -f $h.ToInt64(), $e.Current.Name, $e.Current.ControlType.ProgrammaticName, $e.Current.AutomationId, $e.Current.ClassName))
    $kids = $e.FindAll([System.Windows.Automation.TreeScope]::Children, [System.Windows.Automation.Condition]::TrueCondition)
    foreach ($k in $kids) { $lines.Add(("    child name='{0}' type={1} id='{2}' class='{3}'" -f $k.Current.Name, $k.Current.ControlType.ProgrammaticName, $k.Current.AutomationId, $k.Current.ClassName)) }
  } catch { $lines.Add("UIA error: $_") }
}
$lines | Out-File -FilePath $Out -Encoding utf8
$lines
