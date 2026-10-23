@echo off
dotnet publish "%~dp0LoudEnv.csproj" -c Release -o "%~dp0dist"
