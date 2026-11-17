@echo off
title QueueLess - Stop All Services
echo Stopping QueueLess services...
taskkill /F /IM QueueLess.Api.exe /T 2>nul
taskkill /F /IM postgres.exe /T 2>nul
taskkill /F /IM node.exe /T 2>nul
echo All services stopped.
pause
