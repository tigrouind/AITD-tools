@echo off

%WINDIR%\Microsoft.NET\Framework\v4.0.30319\msbuild.exe /p:Configuration=Release ".\CacheViewer\CacheViewer.sln"
if %ERRORLEVEL% NEQ 0 pause
%WINDIR%\Microsoft.NET\Framework\v4.0.30319\msbuild.exe /p:Configuration=Release ".\LifeDISA\LifeDISA.sln"
if %ERRORLEVEL% NEQ 0 pause
%WINDIR%\Microsoft.NET\Framework\v4.0.30319\msbuild.exe /p:Configuration=Release ".\MemoryViewer\MemoryViewer.sln"
if %ERRORLEVEL% NEQ 0 pause
%WINDIR%\Microsoft.NET\Framework\v4.0.30319\msbuild.exe /p:Configuration=Release ".\TrackDISA\TrackDISA.sln"
if %ERRORLEVEL% NEQ 0 pause
%WINDIR%\Microsoft.NET\Framework\v4.0.30319\msbuild.exe /p:Configuration=Release ".\VarsViewer\VarsViewer.sln"
if %ERRORLEVEL% NEQ 0 pause

"%PROGRAMFILES%\7-Zip\7z" a -tzip "AITD-tools.zip" ^
 ".\CacheViewer\CacheViewer\bin\Release\*.exe" ^
 ".\LifeDISA\LifeDISA\bin\Release\*.exe" ^
 ".\LifeDISA\LifeDISA\bin\Release\UnPAK.dll" ^
 ".\MemoryViewer\MemoryViewer\bin\Release\*.exe" ^
 ".\MemoryViewer\MemoryViewer\bin\Release\SDL.dll" ^
 ".\MemoryViewer\MemoryViewer\bin\Release\SDL2.dll" ^
 ".\MemoryViewer\MemoryViewer\bin\Release\SDL2_image.dll" ^
 ".\MemoryViewer\MemoryViewer\bin\Release\Shared.dll" ^
 ".\TrackDISA\TrackDISA\bin\Release\*.exe" ^
 ".\VarsViewer\VarsViewer\bin\Release\*.exe" ^
 "-mx=9"
if %ERRORLEVEL% NEQ 0 pause