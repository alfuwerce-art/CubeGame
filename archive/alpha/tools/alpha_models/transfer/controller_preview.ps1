# Real PlayMode frames -> local GIF/contact sheet. Windows built-ins only, no Pillow/install.
# powershell.exe -NoProfile -STA -File tools/alpha_models/transfer/controller_preview.ps1
param(
  [string]$Project='D:\Projects\CubeGame\.scratch\s05a2\unity\AlphaTransfer',
  [string]$Out='D:\Projects\CubeGame\.scratch\animsys\visuals',
  [ValidateSet('controller','native')][string]$Source='controller'
)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..'))
$scratch=[IO.Path]::GetFullPath((Join-Path $repo '.scratch'))+[IO.Path]::DirectorySeparatorChar
$sourceDir=[IO.Path]::GetFullPath((Join-Path $Project 'TransferOutput'))
$Out=[IO.Path]::GetFullPath($Out)
if(-not $sourceDir.StartsWith($scratch,[StringComparison]::OrdinalIgnoreCase) -or -not $Out.StartsWith($scratch,[StringComparison]::OrdinalIgnoreCase)){throw 'Preview paths must stay inside .scratch'}
New-Item -ItemType Directory -Path $Out -Force | Out-Null
Add-Type -AssemblyName System.Drawing,PresentationCore,WindowsBase
$rows=@(Get-Content -LiteralPath (Join-Path $sourceDir ($Source+'_frames.tsv')) | ForEach-Object { ,($_ -split "`t") })
if($rows.Count -eq 0){throw 'No real controller frames'}
$font=[Drawing.Font]::new('Segoe UI',12)
$brush=[Drawing.SolidBrush]::new([Drawing.Color]::White)
$encoder=[Windows.Media.Imaging.GifBitmapEncoder]::new()
$images=[Collections.Generic.List[Drawing.Bitmap]]::new()
try {
  for($i=0;$i -lt $rows.Count;$i++){
    $row=$rows[$i];$file=$row[0];$phase=$row[1]
    if([IO.Path]::GetFileName($file) -ne $file){throw 'Invalid frame path'}
    $original=[Drawing.Image]::FromFile((Join-Path (Join-Path $sourceDir ($Source+'_frames')) $file))
    $bitmap=[Drawing.Bitmap]::new(480,534)
    $graphics=[Drawing.Graphics]::FromImage($bitmap)
    $graphics.Clear([Drawing.Color]::FromArgb(25,30,40))
    $graphics.DrawImage($original,0,54,480,480)
    $graphics.DrawString("$phase / $($row[2]) / $($row[3]) ms",$font,$brush,8,4)
    $status=if($phase -eq 'Locomotion'){'RECONSTRUCTED_FROM_EVIDENCE'}elseif($phase -eq 'Action' -or $phase -eq 'Recovery'){'EMULATED_ALPHA_CODE / transitions explicit'}else{'UNITY_TRANSITION'}
    $graphics.DrawString($status,$font,$brush,8,28)
    $graphics.Dispose();$original.Dispose();$images.Add($bitmap)
    $stream=[IO.MemoryStream]::new();$bitmap.Save($stream,[Drawing.Imaging.ImageFormat]::Png);$stream.Position=0
    $image=[Windows.Media.Imaging.BitmapImage]::new();$image.BeginInit()
    $image.CacheOption=[Windows.Media.Imaging.BitmapCacheOption]::OnLoad;$image.StreamSource=$stream;$image.EndInit();$image.Freeze();$stream.Dispose()
    $metadata=[Windows.Media.Imaging.BitmapMetadata]::new('gif')
    $metadata.SetQuery('/grctlext/Delay',[UInt16]$(if($Source -eq 'native'){10}else{7}))
    if($i -eq 0){
      $metadata.SetQuery('/appext/application',[Text.Encoding]::ASCII.GetBytes('NETSCAPE2.0'))
      $metadata.SetQuery('/appext/data',[byte[]]@(3,1,0,0))
    }
    $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($image,$null,$metadata,$null))
  }
  $gif=Join-Path $Out ($Source+'_preview.gif')
  $gifStream=[IO.File]::Create($gif);try{$encoder.Save($gifStream)}finally{$gifStream.Dispose()}
  $chosen=[Collections.Generic.List[int]]::new();$chosen.Add(0);$chosen.Add([Math]::Min(25,$rows.Count-1))
  foreach($phase in @('Stopping','EnteringAction')){
    for($i=0;$i -lt $rows.Count;$i++){if($rows[$i][1] -eq $phase){$chosen.Add($i);break}}
  }
  foreach($targetMs in @(600,800,1000)){
    $best=-1;$distance=[double]::PositiveInfinity
    for($i=0;$i -lt $rows.Count;$i++){
      if($rows[$i][1] -ne 'Action'){continue}
      $ms=[double]::Parse($rows[$i][3].Replace(',','.'),[Globalization.CultureInfo]::InvariantCulture)
      if([Math]::Abs($ms-$targetMs) -lt $distance){$best=$i;$distance=[Math]::Abs($ms-$targetMs)}
    }
    if($best -ge 0){$chosen.Add($best)}
  }
  foreach($phase in @('Recovery','ExitingAction')){
    for($i=0;$i -lt $rows.Count;$i++){if($rows[$i][1] -eq $phase){$chosen.Add($i);break}}
  }
  if($Source -eq 'native'){
    $chosen.Clear()
    foreach($stage in @(0,2,3,5,7,8)){
      $matches=@(0..($rows.Count-1)|Where-Object{$rows[$_][2] -like "*stage=$stage *"})
      if($matches.Count){
        $chosen.Add($matches[0])
        if($stage -eq 8){$hidden=@($matches|Where-Object{$rows[$_][2] -like '*weapon=hidden*'});if($hidden.Count){$chosen.Add($hidden[[int]($hidden.Count/2)])}}
        elseif($stage -eq 2){$chosen.Add($matches[[Math]::Min(8,$matches.Count-1)])}
        else{$chosen.Add($matches[[int]($matches.Count/2)])}
        $chosen.Add($matches[-1])
      }
    }
  }else{$chosen.Add($rows.Count-1)}
  $sheet=[Drawing.Bitmap]::new(720,267*[int][Math]::Ceiling($chosen.Count/3.0));$g=[Drawing.Graphics]::FromImage($sheet)
  $g.Clear([Drawing.Color]::FromArgb(25,30,40))
  for($i=0;$i -lt $chosen.Count;$i++){$g.DrawImage($images[$chosen[$i]],($i%3)*240,[int][Math]::Floor($i/3.0)*267,240,267)}
  $sheet.Save((Join-Path $Out ($Source+'_contact_sheet.png')),[Drawing.Imaging.ImageFormat]::Png);$g.Dispose();$sheet.Dispose()
  Write-Output "PREVIEW_RESULT=PASS real_frames=$($rows.Count) gif=$gif"
} finally {
  foreach($img in $images){$img.Dispose()};$font.Dispose();$brush.Dispose()
}
