@echo off

%WINDIR%\Microsoft.NET\Framework\v4.0.30319\msbuild.exe /p:Configuration=Release ".\CacheViewer\CacheViewer.sln"
if %ERRORLEVEL% NEQ 0 pause
%WINDIR%\Microsoft.NET\Framework\v4.0.30319\msbuild.exe /p:Configuration=Release;OutputPath=bin\Release\AITD1\CDROM\ /t:Rebuild ".\LifeDISA\LifeDISA.sln"
if %ERRORLEVEL% NEQ 0 pause
%WINDIR%\Microsoft.NET\Framework\v4.0.30319\msbuild.exe /p:Configuration=Release;DefineConstants="AITD1_FLOPPY";OutputPath=bin\Release\AITD1\FLOPPY\ /t:Rebuild ".\LifeDISA\LifeDISA.sln"
if %ERRORLEVEL% NEQ 0 pause
%WINDIR%\Microsoft.NET\Framework\v4.0.30319\msbuild.exe /p:Configuration=Release;DefineConstants="JITD";OutputPath=bin\Release\JITD\ /t:Rebuild ".\LifeDISA\LifeDISA.sln"
if %ERRORLEVEL% NEQ 0 pause
%WINDIR%\Microsoft.NET\Framework\v4.0.30319\msbuild.exe /p:Configuration=Release;DefineConstants="JITD;AITD2";OutputPath=bin\Release\AITD2\ /t:Rebuild ".\LifeDISA\LifeDISA.sln"
if %ERRORLEVEL% NEQ 0 pause
%WINDIR%\Microsoft.NET\Framework\v4.0.30319\msbuild.exe /p:Configuration=Release;DefineConstants="JITD;AITD2;AITD3";OutputPath=bin\Release\AITD3\ /t:Rebuild ".\LifeDISA\LifeDISA.sln"
if %ERRORLEVEL% NEQ 0 pause
%WINDIR%\Microsoft.NET\Framework\v4.0.30319\msbuild.exe /p:Configuration=Release ".\MemoryViewer\MemoryViewer.sln"
if %ERRORLEVEL% NEQ 0 pause
%WINDIR%\Microsoft.NET\Framework\v4.0.30319\msbuild.exe /p:Configuration=Release ".\TrackDISA\TrackDISA.sln"
if %ERRORLEVEL% NEQ 0 pause
%WINDIR%\Microsoft.NET\Framework\v4.0.30319\msbuild.exe /p:Configuration=Release ".\VarsViewer\VarsViewer.sln"
if %ERRORLEVEL% NEQ 0 pause

del ".\LifeDISA\LifeDISA\bin\Release\AITD1\CDROM\LIFEDISA_CDROM.exe"
del ".\LifeDISA\LifeDISA\bin\Release\AITD1\FLOPPY\LIFEDISA_FLOPPY.exe"
del ".\LifeDISA\LifeDISA\bin\Release\JITD\LIFEDISA_JITD.exe"
del ".\LifeDISA\LifeDISA\bin\Release\AITD2\LIFEDISA_AITD2.exe"
del ".\LifeDISA\LifeDISA\bin\Release\AITD3\LIFEDISA_AITD3.exe"

ren ".\LifeDISA\LifeDISA\bin\Release\AITD1\CDROM\LIFEDISA.exe" "LIFEDISA_CDROM.exe"
ren ".\LifeDISA\LifeDISA\bin\Release\AITD1\FLOPPY\LIFEDISA.exe" "LIFEDISA_FLOPPY.exe"
ren ".\LifeDISA\LifeDISA\bin\Release\JITD\LIFEDISA.exe" "LIFEDISA_JITD.exe"
ren ".\LifeDISA\LifeDISA\bin\Release\AITD2\LIFEDISA.exe" "LIFEDISA_AITD2.exe"
ren ".\LifeDISA\LifeDISA\bin\Release\AITD3\LIFEDISA.exe" "LIFEDISA_AITD3.exe"

"%PROGRAMFILES%\7-Zip\7z" a -tzip "AITD-tools.zip" ^
 ".\CacheViewer\CacheViewer\bin\Release\*.exe" ^
 ".\LifeDISA\LifeDISA\bin\Release\AITD1\CDROM\*.exe" ^
 ".\LifeDISA\LifeDISA\bin\Release\AITD1\FLOPPY\*.exe" ^
 ".\LifeDISA\LifeDISA\bin\Release\JITD\*.exe" ^
 ".\LifeDISA\LifeDISA\bin\Release\AITD2\*.exe" ^
 ".\LifeDISA\LifeDISA\bin\Release\AITD3\*.exe" ^
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