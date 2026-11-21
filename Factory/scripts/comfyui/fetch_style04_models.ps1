# FACTORIA / COMFY-STYLE-04 - download manifest for DreamShaper XL stack.
#
# Downloads (in priority order) into the EXISTING portable ComfyUI model folders.
# Every file is verified by SHA-256 published in the HuggingFace LFS metadata;
# transfer itself is delegated to fetch_checkpoint_parallel.ps1 (parallel ranges +
# resume + per-part verification) because HF throttles a single stream to <100 KB/s here.
#
# Usage:
#   powershell -ExecutionPolicy Bypass -File scripts\comfyui\fetch_style04_models.ps1
#   powershell -ExecutionPolicy Bypass -File scripts\comfyui\fetch_style04_models.ps1 -Only dreamshaper
param(
    [string]$Install = 'D:\programs\ComfyUI',
    [string]$Project = 'D:\Projects\CubeGame Factory',
    [int]$Connections = 12,
    [string]$Only = ''
)

$ErrorActionPreference = 'Stop'
$models = Join-Path $Install 'ComfyUI\models'
$fetcher = Join-Path $Project 'scripts\comfyui\fetch_checkpoint_parallel.ps1'
$logDir = Join-Path $Project 'logs'
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
$stamp = Get-Date -Format 'yyyy-MM-dd'
$log = Join-Path $logDir "COMFY-STYLE-04_download_$stamp.log"

$manifest = @(
    [pscustomobject]@{
        id    = 'dreamshaper'
        url   = 'https://huggingface.co/Lykon/dreamshaper-xl-v2-turbo/resolve/main/DreamShaperXL_Turbo_v2_1.safetensors'
        file  = 'DreamShaperXL_Turbo_v2_1.safetensors'
        dir   = 'checkpoints'
        size  = 6939220250
        sha   = '4496b36d48bfd7cfe4e5dbce3485db567bcefa2bef7238d290dbd45612125083'
        why   = 'main SDXL checkpoint (Lykon, official HF, openrail++)'
    },
    [pscustomobject]@{
        id    = 'voxelxl_lora'
        url   = 'https://huggingface.co/Fictiverse/Voxel_XL_Lora/resolve/main/VoxelXL_v1.safetensors'
        file  = 'VoxelXL_v1.safetensors'
        dir   = 'loras'
        size  = 912554516
        sha   = '7d9a5f11e1b38d97f75d2b84bfb5bb3bf95cd0e5f2500b002d13374eb4f88b5c'
        why   = 'SDXL LoRA "voxel style" (openrail), volumetric voxel look - experimental (small, unblocks PIXEL_STYLE early)'
    },
    [pscustomobject]@{
        id    = 'ipadapter_sdxl'
        url   = 'https://huggingface.co/h94/IP-Adapter/resolve/main/sdxl_models/ip-adapter-plus_sdxl_vit-h.safetensors'
        file  = 'ip-adapter-plus_sdxl_vit-h.safetensors'
        dir   = 'ipadapter'
        size  = 847517512
        sha   = '3f5062b8400c94b7159665b21ba5c62acdcd7682262743d7f2aefedef00e6581'
        why   = 'IPAdapter style reference for SDXL (apache-2.0)'
    },
    [pscustomobject]@{
        id    = 'clipvit_h'
        url   = 'https://huggingface.co/h94/IP-Adapter/resolve/main/models/image_encoder/model.safetensors'
        file  = 'CLIP-ViT-H-14-laion2B-s32B-b79K.safetensors'
        dir   = 'clip_vision'
        size  = 2528373448
        sha   = '6ca9667da1ca9e0b0f75e46bb030f7e011f44f86cbfb8d5a36590fcd7507b030'
        why   = 'CLIP Vision ViT-H encoder required by IPAdapter (SD1.5 and SDXL vit-h)'
    },
    [pscustomobject]@{
        id    = 'controlnet_union'
        url   = 'https://huggingface.co/xinsir/controlnet-union-sdxl-1.0/resolve/main/diffusion_pytorch_model.safetensors'
        file  = 'controlnet-union-sdxl-1.0.safetensors'
        dir   = 'controlnet'
        size  = 2512030408
        sha   = 'a9e13fd61f3193887791c8a0dd07a07202174dc47d5ddaea94ea1344f07c7467'
        why   = 'one union ControlNet for SDXL (canny/openpose/depth) instead of 3 x 2.5 GB files'
    }
)

"[$(Get-Date -Format s)] STYLE04 download start" | Tee-Object -FilePath $log -Append
$results = @()
foreach ($m in $manifest) {
    if ($Only -and $m.id -ne $Only) { continue }
    $target = Join-Path (Join-Path $models $m.dir) $m.file
    $line = "[$(Get-Date -Format s)] === $($m.id): $($m.why)"
    $line | Tee-Object -FilePath $log -Append
    if ((Test-Path $target) -and (Get-Item $target).Length -eq $m.size) {
        $sha = (Get-FileHash $target -Algorithm SHA256).Hash.ToLower()
        if ($sha -eq $m.sha) { "[$(Get-Date -Format s)] $($m.id) ALREADY_COMPLETE sha256 OK" | Tee-Object -FilePath $log -Append; $results += "$($m.id)=COMPLETE"; continue }
    }
    & powershell -NoProfile -ExecutionPolicy Bypass -File $fetcher -Url $m.url -Target $target `
        -ExpectedSha256 $m.sha -TotalBytes $m.size -Connections $Connections 2>&1 |
        Tee-Object -FilePath $log -Append | Select-Object -Last 3
    $code = $LASTEXITCODE
    if ($code -eq 0) { $results += "$($m.id)=OK" } elseif ($code -eq 7) { $results += "$($m.id)=INCOMPLETE"; "[$(Get-Date -Format s)] $($m.id) incomplete - rerun the same command to continue" | Tee-Object -FilePath $log -Append; break } else { $results += "$($m.id)=FAIL($code)" }
    "[$(Get-Date -Format s)] $($m.id) exit=$code" | Tee-Object -FilePath $log -Append
}
"[$(Get-Date -Format s)] SUMMARY $($results -join ' ')" | Tee-Object -FilePath $log -Append
