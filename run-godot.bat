@echo off
set GODOT=D:\Programs\Godot_v4.61-stable_win64\Godot_v4.6.1-stable_win64_console.exe
echo [RUN] Launching game...
"%GODOT%" --path "%~dp0" > godot_log.txt 2>&1
echo [DONE] Godot exited. Output saved to godot_log.txt
type godot_log.txt
pause
