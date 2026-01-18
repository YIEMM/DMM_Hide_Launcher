function Pause {
    Write-Host "Press any key to continue..."
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
}

# Set working directory to script location
Set-Location $PSScriptRoot

# Project root directory
$PROJECT_ROOT = $PSScriptRoot
$SOLUTION_FILE = "src\DMM_Hide_Launcher.sln"
$OUTPUT_DIR = "publish"

# Title
$Host.UI.RawUI.WindowTitle = "DMM_Hide_Launcher Build Script"

function Build-Version {
    param(
        [string]$VersionType,
        [string]$ProjectFile,
        [string]$NativeLib
    )
    
    while ($true) {
        Clear-Host
        Write-Host "=========================================" -ForegroundColor Cyan
        Write-Host "Building $VersionType version" -ForegroundColor Cyan
        Write-Host "=========================================" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "1. x86 (32-bit)"
        Write-Host "2. x64 (64-bit)"
        Write-Host "3. Both x86 and x64"
        Write-Host ""
        
        $arch = Read-Host "Enter architecture [1-3]"
        
        $buildSuccess = $true
        $outputPath = ""
        
        switch ($arch) {
            "1" {
                $rid = "win-x86"
                $outputSubDir = "x86"
                $outputPath = "$OUTPUT_DIR\$VersionType\$outputSubDir"
                Write-Host "Building x86 version..."
                
                dotnet publish $ProjectFile -c Release -r $rid --no-self-contained `
                    /p:PublishSingleFile=true `
                    /p:IncludeNativeLibrariesForSelfExtract=$NativeLib `
                    /p:DebugType=None `
                    /p:TrimUnusedDependencies=true `
                    /p:IncludeAllContentForSelfExtract=false `
                    -o $outputPath
                
                if ($LASTEXITCODE -ne 0) {
                    Write-Host "Build failed!" -ForegroundColor Red
                    $buildSuccess = $false
                }
                break
            }
            "2" {
                $rid = "win-x64"
                $outputSubDir = "x64"
                $outputPath = "$OUTPUT_DIR\$VersionType\$outputSubDir"
                Write-Host "Building x64 version..."
                
                dotnet publish $ProjectFile -c Release -r $rid --no-self-contained `
                    /p:PublishSingleFile=true `
                    /p:IncludeNativeLibrariesForSelfExtract=$NativeLib `
                    /p:DebugType=None `
                    /p:TrimUnusedDependencies=true `
                    /p:IncludeAllContentForSelfExtract=false `
                    -o $outputPath
                
                if ($LASTEXITCODE -ne 0) {
                    Write-Host "Build failed!" -ForegroundColor Red
                    $buildSuccess = $false
                }
                break
            }
            "3" {
                # Build x86
                $rid = "win-x86"
                $outputSubDir = "x86"
                $outputPath = "$OUTPUT_DIR\$VersionType"
                Write-Host "Building x86 version..."
                
                dotnet publish $ProjectFile -c Release -r $rid --no-self-contained `
                    /p:PublishSingleFile=true `
                    /p:IncludeNativeLibrariesForSelfExtract=$NativeLib `
                    /p:DebugType=None `
                    /p:TrimUnusedDependencies=true `
                    /p:IncludeAllContentForSelfExtract=false `
                    -o "$OUTPUT_DIR\$VersionType\$outputSubDir"
                
                if ($LASTEXITCODE -ne 0) {
                    Write-Host "Build x86 failed!" -ForegroundColor Red
                    $buildSuccess = $false
                }
                
                # Build x64
                $rid = "win-x64"
                $outputSubDir = "x64"
                Write-Host "Building x64 version..."
                
                dotnet publish $ProjectFile -c Release -r $rid --no-self-contained `
                    /p:PublishSingleFile=true `
                    /p:IncludeNativeLibrariesForSelfExtract=$NativeLib `
                    /p:DebugType=None `
                    /p:TrimUnusedDependencies=true `
                    /p:IncludeAllContentForSelfExtract=false `
                    -o "$OUTPUT_DIR\$VersionType\$outputSubDir"
                
                if ($LASTEXITCODE -ne 0) {
                    Write-Host "Build x64 failed!" -ForegroundColor Red
                    $buildSuccess = $false
                }
                break
            }
            default {
                Write-Host "Invalid architecture, try again." -ForegroundColor Red
                Pause
                continue
            }
        }
        
        if ($buildSuccess) {
            Write-Host "Build completed!" -ForegroundColor Green
            Write-Host "Output: $outputPath"
        }
        
        Write-Host ""
        Pause
        break
    }
}

while ($true) {
    Clear-Host
    Write-Host "=========================================" -ForegroundColor Cyan
    Write-Host "DMM_Hide_Launcher Build Script" -ForegroundColor Cyan
    Write-Host "=========================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "1. Build Normal Version"
    Write-Host "2. Build WebView2 Version"
    Write-Host "3. Exit"
    Write-Host ""
    
    $choice = Read-Host "Enter choice [1-3]"
    
    switch ($choice) {
        "1" { 
            Build-Version -VersionType "normal" `
                -ProjectFile "src\DMM_Hide_Launcher\DMM_Hide_Launcher.csproj" `
                -NativeLib "false"
        }
        "2" { 
            Build-Version -VersionType "webview2" `
                -ProjectFile "src\DMM_Hide_Launcher_Webview2\DMM_Hide_Launcher_Webview2.csproj" `
                -NativeLib "true"
        }
        "3" { 
            Write-Host ""
            Write-Host "Thank you for using DMM_Hide_Launcher Build Script!" -ForegroundColor Green
            Pause
            exit
        }
        default {
            Write-Host "Invalid choice, try again." -ForegroundColor Red
            Pause
        }
    }
}