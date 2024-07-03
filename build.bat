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
 ".\LifeDISA\LifeDISA\bin\Release\*.exe" ^
 ".\MemoryViewer\MemoryViewer\bin\Release\*.exe" ^
 ".\MemoryViewer\MemoryViewer\bin\Release\SDL.dll" ^
 ".\MemoryViewer\MemoryViewer\bin\Release\SDL2.dll" ^
 ".\MemoryViewer\MemoryViewer\bin\Release\SDL2_image.dll" ^
 ".\TrackDISA\TrackDISA\bin\Release\*.exe" ^
 ".\VarsViewer\VarsViewer\bin\Release\*.exe" ^
 ".\PAKExtract\PAKExtract\bin\Release\*.exe" ^
 ".\Shared\bin\Release\Shared.dll" ^
 ".\UnPAK\bin\Release\Unpak.dll" ^
 "-mx=9"
if %ERRORLEVEL% NEQ 0 pause