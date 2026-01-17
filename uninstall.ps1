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
$taskName = "WindowsNetworkHealthMonitor"
$installDir = "$env:ProgramData\Microsoft\NetworkDiagnostics"

# Confirm uninstallation
$confirm = Read-Host "Are you sure you want to uninstall? (yes/no)"
if ($confirm -ne "yes") {
    Write-Host "Uninstallation cancelled." -ForegroundColor Yellow
    exit 0
}

# Stop and remove Task Scheduler task
try {
    $task = Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
    if ($task) {
        Write-Host "Stopping scheduled task..." -ForegroundColor Gray
        Stop-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 2
        
        Write-Host "Removing scheduled task..." -ForegroundColor Gray
        Unregister-ScheduledTask -TaskName $taskName -Confirm:$false
        Start-Sleep -Seconds 2
        
        Write-Host "Task removed successfully." -ForegroundColor Green
    }
} catch {
    Write-Warning "Error removing task: $_"
}

# Stop service if running (legacy)
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
    }
} catch {
    Write-Warning "Error removing service: $_"
}

# Stop any running processes
Write-Host "Stopping running processes..." -ForegroundColor Gray
Get-Process | Where-Object { 
    try { $_.Path -like "*NetworkDiagnostics*" } catch { $false }
} | ForEach-Object {
    Write-Host "  Stopping process: $($_.ProcessName) (ID: $($_.Id))" -ForegroundColor Gray
    Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
}
Start-Sleep -Seconds 3

# Ask about data removal
$removeData = Read-Host "Do you want to remove all data and logs? (yes/no)"
if ($removeData -eq "yes") {
    try {
        if (Test-Path $installDir) {
            Write-Host "Removing installation directory..." -ForegroundColor Gray
            
            # Remove hidden/system attributes first
            Get-ChildItem -Path $installDir -Recurse -Force -ErrorAction SilentlyContinue | ForEach-Object {
                try {
                    $_.Attributes = 'Normal'
                } catch {
                    # Ignore attribute errors
                }
            }
            
            try {
                Set-ItemProperty -Path $installDir -Name Attributes -Value 'Normal' -ErrorAction SilentlyContinue
            } catch {
                # Ignore attribute errors
            }
            
            # Try to remove files with retry logic
            $maxRetries = 3
            $retryCount = 0
            $removed = $false
            
            while (-not $removed -and $retryCount -lt $maxRetries) {
                try {
                    Remove-Item -Path $installDir -Recurse -Force -ErrorAction Stop
                    $removed = $true
                    Write-Host "Data and logs removed successfully." -ForegroundColor Green
                } catch {
                    $retryCount++
                    if ($retryCount -lt $maxRetries) {
                        Write-Host "  Retry $retryCount/$maxRetries..." -ForegroundColor Gray
                        Start-Sleep -Seconds 2
                    } else {
                        Write-Warning "Some files could not be removed (still in use)"
                        Write-Host "Attempting to remove accessible files..." -ForegroundColor Gray
                        
                        # Remove files that can be removed
                        Get-ChildItem -Path $installDir -Recurse -Force -ErrorAction SilentlyContinue | ForEach-Object {
                            try {
                                Remove-Item -Path $_.FullName -Force -ErrorAction Stop
                            } catch {
                                Write-Verbose "  Skipped: $($_.Name)"
                            }
                        }
                        
                        # Try to remove empty directories
                        try {
                            Remove-Item -Path $installDir -Recurse -Force -ErrorAction SilentlyContinue
                        } catch {
                            # Ignore final error
                        }
                        
                        if (Test-Path $installDir) {
                            Write-Host "Some files remain at: $installDir" -ForegroundColor Yellow
                            Write-Host "Please restart your computer and run this script again to remove remaining files." -ForegroundColor Yellow
                        } else {
                            Write-Host "Data and logs removed successfully." -ForegroundColor Green
                        }
                    }
                }
            }
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
