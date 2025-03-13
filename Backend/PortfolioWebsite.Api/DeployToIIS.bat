@echo off
:: Check for administrative privileges
openfiles >nul 2>&1
if '%errorlevel%' NEQ '0' (
    echo Requesting administrative privileges...
    PowerShell Start-Process -FilePath '%~f0' -Verb RunAs
    exit /B
)

echo Stopping IIS...
iisreset /stop

:: Delete appsettings.json if it exists
if exist C:\Deployable\PortfolioApi\appsettings.json (
    echo Deleting appsettings.json...
    del /F /Q C:\Deployable\PortfolioApi\appsettings.json
)

:: Delete appsettings.production.json if it exists
if exist C:\Deployable\PortfolioApi\appsettings.production.json (
    echo Deleting appsettings.json...
    del /F /Q C:\Deployable\PortfolioApi\appsettings.production.json
)


echo Copying files...
xcopy /E /I /Y C:\Deployable\PortfolioApi C:\PortfolioApi

echo Starting IIS...
iisreset /start

echo Deployment completed.

echo Deleting files in C:\Deployable\PortfolioApi...
del /F /Q C:\Deployable\PortfolioApi\*
rmdir /S /Q C:\Deployable\PortfolioApi