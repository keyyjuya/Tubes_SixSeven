@echo off
REM Keyju.cmd - Run the bot in development or release mode
REM Set MODE=dev for development (default, always rebuilds)
REM Set MODE=release for release (only runs if bin exists)

set MODE=dev

if "%MODE%"=="dev" (
    rmdir /s /q bin obj >nul 2>&1
    dotnet build >nul
    dotnet run --no-build >nul
) else if "%MODE%"=="release" (
    if exist bin\ (
        dotnet run --no-build >nul
    ) else (
        @REM dotnet run
        dotnet build >nul
        dotnet run --no-build >nul
    )
) else (
    echo Error: Invalid MODE value. Use "dev" or "release".
)