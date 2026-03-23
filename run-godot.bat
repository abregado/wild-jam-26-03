@echo off
echo [BUILD] Building C# project...
dotnet build || ( echo BUILD FAILED & pause & exit /b 1 )
echo [BUILD] Success. Launching game...
"D:\Programs\Godot_v4.6.1-stable_win64\Godot_v4.6.1-stable_win64_console.exe" --path "D:\repos\wild-jam-26-03" > godot_log.txt 2>&1
echo [DONE] Godot exited. Output saved to godot_log.txt
type godot_log.txt
pause