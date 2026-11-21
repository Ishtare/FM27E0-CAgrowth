@echo off
rem Builds FM27-euro-0 via build.ps1 (handles the euro sign in the file name).
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1"
