@echo off

"%ProgramFiles%\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\msbuild.exe" /p:Configuration=Release ".\Shared\Shared.csproj" /t:Rebuild
if %ERRORLEVEL% NEQ 0 pause
"%ProgramFiles%\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\msbuild.exe" /p:Configuration=Release ".\LifeDISA\LifeDISA.sln" /t:Rebuild
if %ERRORLEVEL% NEQ 0 pause
"%ProgramFiles%\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\msbuild.exe" /p:Configuration=Release ".\MemoryViewer\MemoryViewer.sln" /t:Rebuild
if %ERRORLEVEL% NEQ 0 pause
"%ProgramFiles%\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\msbuild.exe" /p:Configuration=Release ".\TrackDISA\TrackDISA.sln" /t:Rebuild
if %ERRORLEVEL% NEQ 0 pause
"%ProgramFiles%\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\msbuild.exe" /p:Configuration=Release ".\VarsViewer\VarsViewer.sln" /t:Rebuild
if %ERRORLEVEL% NEQ 0 pause
"%ProgramFiles%\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\msbuild.exe" /p:Configuration=Release ".\PAKExtract\PAKExtract.sln" /t:Rebuild
if %ERRORLEVEL% NEQ 0 pause
if exist C:\MinGW\bin\gcc.exe (
	C:\MinGW\bin\gcc.exe -shared -O2 -s "UnPAK\unpak.c" -o "UnPAK\bin\Release\UnPAK.dll"
	if %ERRORLEVEL% NEQ 0 pause
) 

"%PROGRAMFILES%\7-Zip\7z" a -tzip "AITD-tools.zip" ^
 ".\LifeDISA\LifeDISA\bin\Release\LifeDISA.exe" ^
 ".\LifeDISA\LifeDISA\bin\Release\GAMEDATA\vars.txt" ^
 ".\MemoryViewer\MemoryViewer\bin\Release\MemoryViewer.exe" ^
 ".\MemoryViewer\MemoryViewer\bin\Release\SDL.dll" ^
 ".\MemoryViewer\MemoryViewer\bin\Release\SDL2.dll" ^
 ".\TrackDISA\TrackDISA\bin\Release\TrackDISA.exe" ^
 ".\VarsViewer\VarsViewer\bin\Release\VarsViewer.exe" ^
 ".\VarsViewer\VarsViewer\bin\Release\Newtonsoft.Json.dll" ^
 ".\PAKExtract\PAKExtract\bin\Release\PAKExtract.exe" ^
 ".\Shared\bin\Release\Shared.dll" ^
 ".\UnPAK\bin\Release\Unpak.dll" ^
 "-mx=9"
if %ERRORLEVEL% NEQ 0 pause
 
"%PROGRAMFILES%\7-Zip\7z" rn "AITD-tools.zip" "vars.txt" "GAMEDATA\vars.txt"
if %ERRORLEVEL% NEQ 0 pause