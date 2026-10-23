@echo off
setlocal
"%~dp0dist\LoudEnv.exe" --alert --threshold 0.20 --device 1 --log "%~dp0alertas.csv" %*
