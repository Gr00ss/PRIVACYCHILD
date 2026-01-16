# ======================================================================
# WINDOWS FAMILY MONITOR - INSTALLATION SCRIPT (Task Scheduler)
# ======================================================================
# This script installs the Windows Family Monitor as Task Scheduler task
# Run as Administrator
# ======================================================================

param(
    [Parameter(Mandatory=$false)]
    [string]$TelegramBotToken = "",
    
    [Parameter(Mandatory=$false)]
    [string]$TelegramUserId = "",
    
    [Parameter(Mandatory=$false)]
    [string]$ReportTime = "19:00"
)

# Check for Administrator privileges
if (-not ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Error "This script must be run as Administrator!"
    exit 1
}

Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host "  Windows Family Monitor - Installation (Task Scheduler Mode)" -ForegroundColor Cyan
Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host ""

# Configuration
$taskName = "WindowsNetworkHealthMonitor"
$installDir = "$env:ProgramData\Microsoft\NetworkDiagnostics"
$exeName = "NetworkHealthMonitor.exe"

# Get current directory
$sourceDir = $PSScriptRoot

# Collect required information if not provided
if ([string]::IsNullOrEmpty($TelegramBotToken)) {
    Write-Host "Step 1: Telegram Bot Configuration" -ForegroundColor Yellow
    Write-Host "To get a bot token:" -ForegroundColor Gray
    Write-Host "  1. Open Telegram and search for @BotFather" -ForegroundColor Gray
    Write-Host "  2. Send /newbot and follow instructions" -ForegroundColor Gray
    Write-Host "  3. Copy the token provided" -ForegroundColor Gray
    Write-Host ""
    $TelegramBotToken = Read-Host "Enter your Telegram Bot Token"
}

if ([string]::IsNullOrEmpty($TelegramUserId)) {
    Write-Host ""
    Write-Host "Step 2: Your Telegram User ID" -ForegroundColor Yellow
    Write-Host "To get your user ID:" -ForegroundColor Gray
    Write-Host "  1. Open Telegram and search for @userinfobot" -ForegroundColor Gray
    Write-Host "  2. Start the bot - it will show your ID" -ForegroundColor Gray
    Write-Host ""
    $TelegramUserId = Read-Host "Enter your Telegram User ID"
}

Write-Host ""
Write-Host "Step 3: Installation" -ForegroundColor Yellow

# Remove existing task if exists
try {
    $existingTask = Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
    if ($existingTask) {
        Write-Host "Stopping existing task..." -ForegroundColor Gray
        Stop-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
        
        Write-Host "Removing existing task..." -ForegroundColor Gray
        Unregister-ScheduledTask -TaskName $taskName -Confirm:$false
        Start-Sleep -Seconds 2
    }
} catch {
    # Task doesn't exist, continue
}

# Kill existing process if running
$process = Get-Process -Name "NetworkHealthMonitor" -ErrorAction SilentlyContinue
if ($process) {
    Write-Host "Stopping existing process..." -ForegroundColor Gray
    $process | Stop-Process -Force
    Start-Sleep -Seconds 2
}

# Create installation directory
Write-Host "Creating installation directory..." -ForegroundColor Gray
if (-not (Test-Path $installDir)) {
    New-Item -Path $installDir -ItemType Directory -Force | Out-Null
}

# Create subdirectories
$logsDir = Join-Path $installDir "Logs"
if (-not (Test-Path $logsDir)) {
    New-Item -Path $logsDir -ItemType Directory -Force | Out-Null
}

# Find the published executable
$publishExe = Join-Path $sourceDir "bin\Release\net8.0-windows\win-x64\publish\sample1.exe"
$sourceExe = $null

if (Test-Path $publishExe) {
    $sourceExe = $publishExe
    Write-Host "Found published executable: $publishExe" -ForegroundColor Gray
} else {
    $sourceExe = Get-ChildItem -Path $sourceDir -Recurse -Filter "sample1.exe" | Where-Object { $_.FullName -like "*\publish\*" } | Select-Object -First 1 -ExpandProperty FullName
    
    if (-not $sourceExe) {
        $sourceExe = Get-ChildItem -Path $sourceDir -Recurse -Filter "sample1.exe" | Select-Object -First 1 -ExpandProperty FullName
    }
}

if (-not $sourceExe -or -not (Test-Path $sourceExe)) {
    Write-Error "Could not find sample1.exe. Please build the project first with:"
    Write-Host "  dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true" -ForegroundColor Yellow
    exit 1
}

# Verify it's a self-contained exe
$exeSize = (Get-Item $sourceExe).Length
if ($exeSize -lt 10MB) {
    Write-Warning "Found exe is only $([math]::Round($exeSize/1MB, 2)) MB. Self-contained exe should be ~50MB."
    Write-Warning "Please rebuild with: dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true"
    $confirm = Read-Host "Continue anyway? (yes/no)"
    if ($confirm -ne "yes") {
        exit 1
    }
}

# Copy executable
Write-Host "Installing executable..." -ForegroundColor Gray
$destExe = Join-Path $installDir $exeName

# Remove old file if exists
if (Test-Path $destExe) {
    Set-ItemProperty -Path $destExe -Name Attributes -Value Normal -ErrorAction SilentlyContinue
    Remove-Item -Path $destExe -Force
}

Copy-Item -Path $sourceExe -Destination $destExe -Force

# Update appsettings.json
Write-Host "Configuring service..." -ForegroundColor Gray
$configPath = Join-Path $installDir "appsettings.json"
$configSource = Join-Path $sourceDir "appsettings.json"

if (Test-Path $configSource) {
    $config = Get-Content $configSource -Raw | ConvertFrom-Json
} else {
    $config = @{
        Logging = @{
            LogLevel = @{
                Default = "Information"
                Microsoft = "Warning"
            }
        }
        Telegram = @{
            BotToken = ""
            AuthorizedUsers = @()
            ReportTime = "19:00"
            MaxFailedAttempts = 5
        }
        Monitoring = @{
            ProcessCheckIntervalMs = 1000
            NetworkCheckIntervalMs = 5000
            DataSaveIntervalMs = 300000
            MaxCpuPercent = 2.0
            MaxMemoryMB = 50
        }
        Database = @{
            FilePath = "activity.db"
            DataRetentionDays = 7
        }
        Security = @{
            EncryptionKey = ""
            EnableStealth = $true
        }
        SystemProcesses = @("explorer", "svchost", "dllhost", "conhost", "dwm", "taskhostw", "runtimebroker")
        SystemDomains = @("microsoft.com", "windowsupdate.com", "ntp.org", "akamai.net", "google.com", "gstatic.com")
    }
}

# Update configuration
$config.Telegram.BotToken = $TelegramBotToken
$config.Telegram.AuthorizedUsers = @([long]$TelegramUserId)
$config.Telegram.ReportTime = $ReportTime

# Generate encryption key if not set
if ([string]::IsNullOrEmpty($config.Security.EncryptionKey) -or $config.Security.EncryptionKey -eq "GENERATE_ON_INSTALL") {
    $config.Security.EncryptionKey = [guid]::NewGuid().ToString("N") + [guid]::NewGuid().ToString("N")
}

# Save configuration
$config | ConvertTo-Json -Depth 10 | Set-Content $configPath -Force

# Create Scheduled Task
Write-Host "Creating Scheduled Task..." -ForegroundColor Gray

# Get current user
$currentUser = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name

# Create action
$action = New-ScheduledTaskAction -Execute $destExe -WorkingDirectory $installDir

# Create trigger - at logon
$trigger = New-ScheduledTaskTrigger -AtLogOn -User $currentUser

# Create settings
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable -RestartCount 3 -RestartInterval (New-TimeSpan -Minutes 1) -ExecutionTimeLimit (New-TimeSpan -Hours 0)

# Create principal (run with highest privileges)
$principal = New-ScheduledTaskPrincipal -UserId $currentUser -LogonType Interactive -RunLevel Highest

# Register task
Register-ScheduledTask -TaskName $taskName -Action $action -Trigger $trigger -Settings $settings -Principal $principal -Description "Monitors system health and application usage" -Force | Out-Null

# Start the task
Write-Host "Starting task..." -ForegroundColor Gray
Start-ScheduledTask -TaskName $taskName

# Wait a moment and check status
Start-Sleep -Seconds 3

$task = Get-ScheduledTask -TaskName $taskName
$taskInfo = Get-ScheduledTaskInfo -TaskName $taskName

if ($task.State -eq "Running" -or $taskInfo.LastTaskResult -eq 0) {
    Write-Host ""
    Write-Host "======================================================================" -ForegroundColor Green
    Write-Host "  Installation Completed Successfully!" -ForegroundColor Green
    Write-Host "======================================================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Task Information:" -ForegroundColor Cyan
    Write-Host "  Name: $taskName" -ForegroundColor Gray
    Write-Host "  Status: $($task.State)" -ForegroundColor Green
    Write-Host "  Install Location: $installDir" -ForegroundColor Gray
    Write-Host "  Runs at: User Logon" -ForegroundColor Gray
    Write-Host ""
    Write-Host "Telegram Bot:" -ForegroundColor Cyan
    Write-Host "  Open Telegram and send /start to your bot to begin" -ForegroundColor Gray
    Write-Host "  Use /apps to see application usage" -ForegroundColor Gray
    Write-Host "  Use /network to see website usage" -ForegroundColor Gray
    Write-Host "  Daily reports will be sent at $ReportTime" -ForegroundColor Gray
    Write-Host ""
    Write-Host "Logs Location:" -ForegroundColor Cyan
    Write-Host "  $logsDir" -ForegroundColor Gray
    Write-Host ""
    Write-Host "NOTE: The monitor will start automatically when you log in." -ForegroundColor Yellow
    Write-Host ""
} else {
    Write-Host ""
    Write-Host "======================================================================" -ForegroundColor Red
    Write-Host "  Installation May Have Issues!" -ForegroundColor Red
    Write-Host "======================================================================" -ForegroundColor Red
    Write-Host ""
    Write-Host "Task Status: $($task.State)" -ForegroundColor Yellow
    Write-Host "Last Result: $($taskInfo.LastTaskResult)" -ForegroundColor Yellow
    Write-Host "Please check the logs at: $logsDir" -ForegroundColor Yellow
    Write-Host ""
}

Write-Host "Press any key to exit..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
