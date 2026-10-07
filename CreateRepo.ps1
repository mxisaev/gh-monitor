$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
foreach ($name in @('git', 'gh')) {
    if (-not (Get-Command $name -ErrorAction SilentlyContinue)) { throw "Install $name first" }
}
gh auth status
if ($LASTEXITCODE -ne 0) { throw 'Run gh auth login first' }
if (Test-Path '.git') { throw 'This folder is already a Git repository. No changes made.' }
git init -b main
if ($LASTEXITCODE -ne 0) { throw 'git init failed' }
git add DeployTray.cs Start.ps1 Start.vbs README.txt .gitignore CreateRepo.ps1
git -c user.name='Maxim Isaev' -c user.email='mxisaev@users.noreply.github.com' commit -m 'Add Windows deploy tray monitor using GitHub CLI'
if ($LASTEXITCODE -ne 0) { throw 'git commit failed' }
gh repo create deploy-tray --private --source . --remote origin --push --description 'Windows tray monitor for GitHub Actions using gh; three-letter run titles'
if ($LASTEXITCODE -ne 0) { throw 'Repository creation or push failed; inspect output before retrying' }
Write-Host 'Repository created and sources pushed.'
