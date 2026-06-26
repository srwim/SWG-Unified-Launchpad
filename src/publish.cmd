@echo off
rem Builds a single-file SwgLaunchpad.exe (no .NET install needed on players' PCs).
rem Requires the .NET 8 SDK: https://dotnet.microsoft.com/download/dotnet/8.0
setlocal
cd /d "%~dp0"

dotnet publish SwgLaunchpad.App\SwgLaunchpad.App.csproj -c Release -r win-x64 ^
  --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:EnableCompressionInSingleFile=true ^
  -o publish

if errorlevel 1 (
  echo.
  echo BUILD FAILED — make sure the .NET 8 SDK is installed.
  pause
  exit /b 1
)

echo.
echo Done. Your exe: %~dp0publish\SwgLaunchpad.exe
pause
