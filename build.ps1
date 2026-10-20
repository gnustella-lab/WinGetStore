$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$wpfDir = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF"
$outExe = Join-Path $root "WinGetStore.exe"

Write-Host "Compiling native Windows 10 WinGetStore.exe..." -ForegroundColor Cyan

& $csc /nologo /target:winexe /optimize+ /platform:anycpu `
    /win32icon:"$root\Assets\AppTiles\StoreLogo.ico" `
    /out:$outExe `
    /r:"$wpfDir\PresentationFramework.dll" `
    /r:"$wpfDir\PresentationCore.dll" `
    /r:"$wpfDir\WindowsBase.dll" `
    /r:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Xaml.dll" `
    /r:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Drawing.dll" `
    "$root\src\Models.cs" `
    "$root\src\IconResolver.cs" `
    "$root\src\WinGetService.cs" `
    "$root\src\StoreTheme.cs" `
    "$root\src\MainWindow.cs" `
    "$root\src\Program.cs"

if ($LASTEXITCODE -ne 0) {
    throw "Build failed for WinGetStore.exe (ExitCode: $LASTEXITCODE)"
}

Write-Host "Build completed successfully: $outExe" -ForegroundColor Green
