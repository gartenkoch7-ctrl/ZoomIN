# Lossless screen capture for the offline smart inversion tests (agent tool, not shipped; see
# docs/kb/rendering-pipeline.md). Writes <name>.raw (int width, int height, BGRA rows) and
# <name>.png into TestResults\offline (gitignored – screen content never goes into the repo).
# The ZoomIN overlay is excluded from captures (WDA_EXCLUDEFROMCAPTURE), so this is the original.
# Must run in the user's session (Terminal panel); an agent's sandbox cannot copy the screen:
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\capture-offline.ps1 -Name x -X 0 -Y 0 -Width 800 -Height 600
param([string]$Name = 'capture', [int]$X = 0, [int]$Y = 0, [int]$Width = 0, [int]$Height = 0)

Add-Type -AssemblyName System.Drawing
Add-Type -Namespace Native -Name Dpi -MemberDefinition '[DllImport("user32.dll")] public static extern bool SetProcessDPIAware();'
[Native.Dpi]::SetProcessDPIAware() | Out-Null
Add-Type -AssemblyName System.Windows.Forms

$screen = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
if ($Width -le 0) { $Width = $screen.Width - $X }
if ($Height -le 0) { $Height = $screen.Height - $Y }

$bitmap = New-Object System.Drawing.Bitmap($Width, $Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.CopyFromScreen($X, $Y, 0, 0, $bitmap.Size)
$graphics.Dispose()

$folder = Join-Path (Split-Path $PSScriptRoot -Parent) 'TestResults\offline'
New-Item -ItemType Directory -Force $folder | Out-Null
$bitmap.Save((Join-Path $folder "$Name.png"), [System.Drawing.Imaging.ImageFormat]::Png)

$rect = New-Object System.Drawing.Rectangle(0, 0, $Width, $Height)
$data = $bitmap.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$bytes = New-Object byte[] ($Width * $Height * 4)
for ($row = 0; $row -lt $Height; $row++) {
    [System.Runtime.InteropServices.Marshal]::Copy([IntPtr]($data.Scan0.ToInt64() + ($row * $data.Stride)), $bytes, $row * $Width * 4, $Width * 4)
}
$bitmap.UnlockBits($data)
$bitmap.Dispose()

$stream = [System.IO.File]::Create((Join-Path $folder "$Name.raw"))
$writer = New-Object System.IO.BinaryWriter($stream)
$writer.Write([int]$Width)
$writer.Write([int]$Height)
$writer.Write($bytes)
$writer.Close()
"captured $Name $Width x $Height (screen $($screen.Width) x $($screen.Height))"
