$ErrorActionPreference = 'Stop'
try {
    Add-Type -AssemblyName System.Windows.Forms
    Add-Type -Path (Join-Path $PSScriptRoot 'DeployTray.cs') -ReferencedAssemblies System.Windows.Forms,System.Drawing,System.Web.Extensions
    [DeployTray]::Run($PSScriptRoot)
} catch {
    Add-Type -AssemblyName System.Windows.Forms
    [System.Windows.Forms.MessageBox]::Show($_.Exception.Message, 'DeployTray') | Out-Null
}
