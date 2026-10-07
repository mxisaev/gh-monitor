$ErrorActionPreference = 'Stop'
try {
    if (-not (Get-Command gh.exe -ErrorAction SilentlyContinue)) {
        throw 'Install GitHub CLI: winget install --id GitHub.cli --exact'
    }
    & gh.exe auth status
    if ($LASTEXITCODE -ne 0) { throw 'Sign in first: gh auth login' }
    Add-Type -AssemblyName System.Windows.Forms
    Add-Type -Path (Join-Path $PSScriptRoot 'DeployTray.cs') -ReferencedAssemblies System.Windows.Forms,System.Drawing,System.Web.Extensions
    if ($env:GH_MONITOR_MODE -eq 'check') {
        Write-Host 'DeployTray is ready. Start it with .\gh-monitor.cmd'
        exit 0
    }
    [DeployTray]::Run($PSScriptRoot)
} catch {
    if ($env:GH_MONITOR_MODE -in @('check', 'console')) {
        [Console]::Error.WriteLine($_.Exception.Message)
        exit 1
    }
    Add-Type -AssemblyName System.Windows.Forms
    [System.Windows.Forms.MessageBox]::Show($_.Exception.Message, 'DeployTray') | Out-Null
    exit 1
}
