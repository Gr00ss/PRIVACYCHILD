# ======================================================================
# DEPLOYMENT PACKAGE PREPARATION SCRIPT
# ======================================================================
# This script prepares a deployment package for installation on other PCs
# ======================================================================

param(
    [Parameter(Mandatory=$false)]
    [string]$OutputPath = ".\Deploy"
)

Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host "  Preparing Deployment Package" -ForegroundColor Cyan
Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host ""

# Get current directory
$sourceDir = $PSScriptRoot

# Check if project is built
$publishExe = Join-Path $sourceDir "bin\Release\net8.0-windows\win-x64\publish\sample1.exe"

if (-not (Test-Path $publishExe)) {
    Write-Host "Executable not found. Building project..." -ForegroundColor Yellow
    
    # Check if .NET SDK is installed
    $dotnetInstalled = $null -ne (Get-Command dotnet -ErrorAction SilentlyContinue)
    
    if (-not $dotnetInstalled) {
        Write-Error ".NET SDK is not installed. Please install .NET 8.0 SDK first."
        Write-Host "Download from: https://dotnet.microsoft.com/download/dotnet/8.0" -ForegroundColor Yellow
        exit 1
    }
    
    $projectFile = Join-Path $sourceDir "sample1.csproj"
    if (-not (Test-Path $projectFile)) {
        Write-Error "Project file not found: sample1.csproj"
        exit 1
    }
    
    Write-Host "Running: dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true" -ForegroundColor Gray
    & dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true $projectFile
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed!"
        exit 1
    }
    
    Write-Host "Build completed successfully!" -ForegroundColor Green
    Write-Host ""
}

# Verify exe exists now
if (-not (Test-Path $publishExe)) {
    Write-Error "Build completed but executable not found at: $publishExe"
    exit 1
}

# Get exe size
$exeSize = (Get-Item $publishExe).Length
$exeSizeMB = [math]::Round($exeSize/1MB, 2)
Write-Host "Executable size: $exeSizeMB MB" -ForegroundColor Gray

if ($exeSize -lt 10MB) {
    Write-Warning "Executable is only $exeSizeMB MB. Self-contained exe should be ~50MB."
    Write-Warning "The deployment may require .NET Runtime on target PCs."
    Write-Host ""
}

# Create deployment directory
Write-Host "Creating deployment package at: $OutputPath" -ForegroundColor Yellow
if (Test-Path $OutputPath) {
    Write-Host "Cleaning existing deployment folder..." -ForegroundColor Gray
    Remove-Item -Path $OutputPath -Recurse -Force
}
New-Item -Path $OutputPath -ItemType Directory -Force | Out-Null

# Copy files
Write-Host "Copying files..." -ForegroundColor Gray

# Copy executable
Copy-Item -Path $publishExe -Destination (Join-Path $OutputPath "sample1.exe") -Force
Write-Host "  OK sample1.exe" -ForegroundColor Green

# Copy appsettings.json - use template version to avoid exposing tokens
$configTemplate = Join-Path $sourceDir "appsettings.template.json"
$configSource = Join-Path $sourceDir "appsettings.json"

if (Test-Path $configTemplate) {
    Copy-Item -Path $configTemplate -Destination (Join-Path $OutputPath "appsettings.json") -Force
    Write-Host "  OK appsettings.json (from template)" -ForegroundColor Green
} elseif (Test-Path $configSource) {
    # Read and clean the config
    $config = Get-Content $configSource -Raw | ConvertFrom-Json
    $config.Telegram.BotToken = "YOUR_BOT_TOKEN_HERE"
    $config.Telegram.AuthorizedUsers = @(0)
    $config.Security.EncryptionKey = "GENERATE_ON_INSTALL"
    $config | ConvertTo-Json -Depth 10 | Set-Content (Join-Path $OutputPath "appsettings.json") -Force
    Write-Host "  OK appsettings.json (cleaned)" -ForegroundColor Green
} else {
    Write-Warning "appsettings.json not found - install.ps1 will create default config"
}

# Copy installation scripts
$installScript = Join-Path $sourceDir "install.ps1"
if (Test-Path $installScript) {
    Copy-Item -Path $installScript -Destination (Join-Path $OutputPath "install.ps1") -Force
    Write-Host "  OK install.ps1" -ForegroundColor Green
}

$uninstallScript = Join-Path $sourceDir "uninstall.ps1"
if (Test-Path $uninstallScript) {
    Copy-Item -Path $uninstallScript -Destination (Join-Path $OutputPath "uninstall.ps1") -Force
    Write-Host "  OK uninstall.ps1" -ForegroundColor Green
}

# Copy task scheduler install script if exists
$taskSchedulerScript = Join-Path $sourceDir "install_taskscheduler.ps1"
if (Test-Path $taskSchedulerScript) {
    Copy-Item -Path $taskSchedulerScript -Destination (Join-Path $OutputPath "install_taskscheduler.ps1") -Force
    Write-Host "  OK install_taskscheduler.ps1" -ForegroundColor Green
}

# Copy documentation
$readmeFile = Join-Path $sourceDir "README.txt"
if (Test-Path $readmeFile) {
    Copy-Item -Path $readmeFile -Destination (Join-Path $OutputPath "README.txt") -Force
    Write-Host "  OK README.txt" -ForegroundColor Green
}

# Create installation guide
$installGuideContent = @'
======================================================================
  WINDOWS FAMILY MONITOR - QUICK INSTALLATION GUIDE
======================================================================

REQUIREMENTS:
  * Windows 10/11 (64-bit)
  * Administrator privileges
  * Telegram account
  * Internet connection

INSTALLATION STEPS:

1. PREPARE TELEGRAM BOT
   - Open Telegram, search for @BotFather
   - Send /newbot and follow instructions
   - Save the bot token (e.g., 123456789:ABCdef...)
   - Search for @userinfobot and get your User ID

2. RUN INSTALLATION
   - Right-click on PowerShell
   - Select "Run as Administrator"
   - Navigate to this folder:
     cd "path\to\this\folder"
   
   - Allow script execution (if needed):
     Set-ExecutionPolicy -ExecutionPolicy Bypass -Scope Process
   
   - Run installer:
     .\install.ps1
   
   - Follow the prompts to enter:
     * Telegram Bot Token
     * Your Telegram User ID
     * Report time (default: 19:00)

3. VERIFY INSTALLATION
   - Open Telegram
   - Find your bot
   - Send /start
   - You should receive a welcome message

TROUBLESHOOTING:
  - If service doesn't start, check logs at:
    C:\ProgramData\Microsoft\NetworkDiagnostics\Logs

  - To uninstall:
    .\uninstall.ps1

SUPPORT:
  - Check README.txt for detailed documentation
  - Logs are in C:\ProgramData\Microsoft\NetworkDiagnostics\Logs

======================================================================
'@

$installGuideContent | Set-Content (Join-Path $OutputPath "INSTALLATION_GUIDE.txt") -Encoding UTF8
Write-Host "  OK INSTALLATION_GUIDE.txt" -ForegroundColor Green

Write-Host ""
Write-Host "======================================================================" -ForegroundColor Green
Write-Host "  Deployment Package Ready!" -ForegroundColor Green
Write-Host "======================================================================" -ForegroundColor Green
Write-Host ""
Write-Host "Package Location: $OutputPath" -ForegroundColor Cyan
Write-Host ""
Write-Host "Package Contents:" -ForegroundColor Cyan
Get-ChildItem $OutputPath | ForEach-Object {
    $size = if ($_.PSIsContainer) { "" } else { " ($([math]::Round($_.Length/1MB, 2)) MB)" }
    Write-Host "  - $($_.Name)$size" -ForegroundColor Gray
}

Write-Host ""
Write-Host "To deploy to another PC:" -ForegroundColor Yellow
Write-Host "  1. Copy the entire '$OutputPath' folder to the target PC" -ForegroundColor Gray
Write-Host "  2. On target PC, run PowerShell as Administrator" -ForegroundColor Gray
Write-Host "  3. Navigate to the folder and run: .\install.ps1" -ForegroundColor Gray
Write-Host ""
Write-Host "Optional: Create a ZIP archive for easy transfer" -ForegroundColor Yellow
Write-Host "  Compress-Archive -Path '$OutputPath\*' -DestinationPath 'WinFamilyMonitor.zip'" -ForegroundColor Cyan
Write-Host ""
