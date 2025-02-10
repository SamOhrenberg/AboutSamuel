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

echo Copying files...
xcopy /E /I /Y C:\Deployable\PortfolioApi C:\PortfolioApi

echo Deleting files in C:\Deployable\PortfolioApi...
del /F /Q C:\Deployable\PortfolioApi\*
rmdir /S /Q C:\Deployable\PortfolioApi

echo Starting IIS...
iisreset /start

echo Deployment completed.