# ======================================================================
# WINDOWS FAMILY MONITOR - UNINSTALLATION SCRIPT
# ======================================================================
# This script removes the Windows Family Monitor service
# Run as Administrator
# ======================================================================

# Check for Administrator privileges
if (-not ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Error "This script must be run as Administrator!"
    exit 1
}

Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host "  Windows Family Monitor - Uninstallation" -ForegroundColor Cyan
Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host ""

# Configuration
$serviceName = "WindowsNetworkHealthService"
$installDir = "$env:ProgramData\Microsoft\NetworkDiagnostics"

# Confirm uninstallation
$confirm = Read-Host "Are you sure you want to uninstall? (yes/no)"
if ($confirm -ne "yes") {
    Write-Host "Uninstallation cancelled." -ForegroundColor Yellow
    exit 0
}

# Stop service if running
try {
    $service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
    if ($service) {
        Write-Host "Stopping service..." -ForegroundColor Gray
        Stop-Service -Name $serviceName -Force
        Start-Sleep -Seconds 2
        
        Write-Host "Removing service..." -ForegroundColor Gray
        sc.exe delete $serviceName | Out-Null
        Start-Sleep -Seconds 2
        
        Write-Host "Service removed successfully." -ForegroundColor Green
    } else {
        Write-Host "Service not found." -ForegroundColor Yellow
    }
} catch {
    Write-Warning "Error removing service: $_"
}

# Ask about data removal
$removeData = Read-Host "Do you want to remove all data and logs? (yes/no)"
if ($removeData -eq "yes") {
    try {
        if (Test-Path $installDir) {
            Write-Host "Removing installation directory..." -ForegroundColor Gray
            
            # Remove hidden/system attributes first
            Get-ChildItem -Path $installDir -Recurse -Force | ForEach-Object {
                $_.Attributes = 'Normal'
            }
            Set-ItemProperty -Path $installDir -Name Attributes -Value 'Normal'
            
            Remove-Item -Path $installDir -Recurse -Force
            Write-Host "Data and logs removed successfully." -ForegroundColor Green
        }
    } catch {
        Write-Warning "Error removing data: $_"
        Write-Host "You may need to manually delete: $installDir" -ForegroundColor Yellow
    }
} else {
    Write-Host "Data and logs preserved at: $installDir" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "======================================================================" -ForegroundColor Green
Write-Host "  Uninstallation Completed" -ForegroundColor Green
Write-Host "======================================================================" -ForegroundColor Green
Write-Host ""
Write-Host "Press any key to exit..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
