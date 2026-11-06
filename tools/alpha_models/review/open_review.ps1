# open_review.ps1 — opens the S05B visual review project in the Unity 6000.3.27f2 editor with the review
# scene loaded. -Play also enters PlayMode right away. Run setup_review.ps1 once before.
param(
  [string]$Project = 'D:\Projects\CubeGame\.scratch\review\unity\S05B_VisualReview',
  [string]$Unity   = 'D:\Unity\Hub\Editor\6000.3.27f2\Editor\Unity.exe',
  [switch]$Play
)
$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath "$Project\Assets\Review\S05B_VisualReview.unity")) { throw "Review scene not found — run tools\alpha_models\review\setup_review.ps1 first" }
$a = @('-projectPath', $Project, '-executeMethod', 'CubeGame.AlphaTransfer.Review.Editor.ReviewSceneBuilder.OpenScene')
if ($Play) { $a += '-reviewPlay' }
Start-Process -FilePath $Unity -ArgumentList $a
