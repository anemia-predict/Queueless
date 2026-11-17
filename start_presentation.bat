@echo off
title QueueLess Presentation Launcher
echo ========================================================
echo   QueueLess Presentation Launcher
echo   Starting PostgreSQL (5433), Backend (5000), Frontend (5173)
echo ========================================================

echo [1/3] Starting PostgreSQL on port 5433...
start "QueueLess - PostgreSQL" "C:\Program Files\PostgreSQL\18\bin\postgres.exe" -D "c:\Users\maria\Desktop\qms\db_data" -p 5433

timeout /t 3 /nobreak >nul

echo [2/3] Starting ASP.NET Core API Backend on http://0.0.0.0:5000...
start "QueueLess - Backend API" powershell -NoExit -Command "$env:DOTNET_ROOT = 'C:\Users\maria\.dotnet'; $env:PATH = 'C:\Users\maria\.dotnet;' + $env:PATH; cd 'c:\Users\maria\Desktop\qms'; dotnet run --project backend/QueueLess.Api --urls http://0.0.0.0:5000"

timeout /t 4 /nobreak >nul

echo [3/3] Starting Vite React Frontend on http://0.0.0.0:5173...
start "QueueLess - Frontend" powershell -NoExit -Command "cd 'c:\Users\maria\Desktop\qms\frontend'; npm run dev"

echo ========================================================
echo   All 3 services launched!
echo   Laptop Access:   http://localhost:5173
echo   Mobile Access:   http://10.116.108.97:5173
echo   Backend Swagger: http://localhost:5000/swagger
echo ========================================================
pause
