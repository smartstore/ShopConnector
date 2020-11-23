@setlocal enableextensions
@cd /d "%~dp0"

@echo off

mklink /j "%CD%\..\SmartStoreNET\src\Plugins\SmartStore.ShopConnector-sym" "%CD%\SmartStore.ShopConnector"

pause
