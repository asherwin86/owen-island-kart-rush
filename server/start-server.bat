@echo off
cd /d "%~dp0"
if not exist node_modules (call npm install)
echo Starting Island Kart Rush server... close this window to stop it.
node kart-server.js
pause
