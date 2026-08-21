param(
    [string]$InstallPath = "D:\HomeMind\tools\beat-synced-edit"
)

$ErrorActionPreference = "Stop"
$repository = "https://github.com/ZiadAbdelkarim/beat-synced-edit.git"

if (-not (Get-Command git -ErrorAction SilentlyContinue)) { throw "git is required." }
if (-not (Get-Command uv -ErrorAction SilentlyContinue)) { throw "uv is required." }

if (Test-Path (Join-Path $InstallPath ".git")) {
    git -C $InstallPath pull --ff-only
    if ($LASTEXITCODE -ne 0) { throw "Failed to update beat-synced-edit." }
}
elseif (Test-Path $InstallPath) {
    $requiredScripts = "beat_map.py", "clip_tag.py", "plan_edit.py"
    $missingScripts = $requiredScripts | Where-Object { -not (Test-Path (Join-Path $InstallPath $_)) }
    if ($missingScripts) {
        throw "Install path exists but is neither a Git checkout nor a complete beat-synced-edit install: $InstallPath"
    }
    Write-Host "Using existing beat-synced-edit files at $InstallPath"
}
else {
    git clone --depth 1 $repository $InstallPath
    if ($LASTEXITCODE -ne 0) { throw "Failed to clone beat-synced-edit." }
}

Push-Location $InstallPath
try {
    uv run --with librosa --with opencv-python --with scenedetect --with scipy --with numpy python beat_map.py --help
    uv run --with librosa --with opencv-python --with scenedetect --with scipy --with numpy python clip_tag.py --help
    uv run --with librosa --with opencv-python --with scenedetect --with scipy --with numpy python plan_edit.py --help
}
finally {
    Pop-Location
}
