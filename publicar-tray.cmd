@echo off
dotnet publish "%~dp0LoudEnv.Tray\LoudEnv.Tray.csproj" -c Release -o "%~dp0tray"
