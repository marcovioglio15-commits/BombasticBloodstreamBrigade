@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Serve-WebGLBuild.ps1" %*
