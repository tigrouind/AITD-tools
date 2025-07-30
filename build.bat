@echo off

rd /s/q ".\LifeDISA\LifeDISA\bin\Release\net9.0-windows\"
rd /s/q ".\MemoryViewer\MemoryViewer\bin\Release\net9.0-windows\"
rd /s/q ".\TrackDISA\TrackDISA\bin\Release\net9.0\"
rd /s/q ".\VarsViewer\VarsViewer\bin\Release\net9.0\"
rd /s/q ".\PAKExtract\PAKExtract\bin\Release\net9.0-windows\"

dotnet build -c Release ".\LifeDISA\LifeDISA.sln"
if %ERRORLEVEL% NEQ 0 pause
dotnet build -c Release ".\MemoryViewer\MemoryViewer.sln"
if %ERRORLEVEL% NEQ 0 pause
dotnet build -c Release ".\TrackDISA\TrackDISA.sln"
if %ERRORLEVEL% NEQ 0 pause
dotnet build -c Release ".\VarsViewer\VarsViewer.sln"
if %ERRORLEVEL% NEQ 0 pause
dotnet build -c Release ".\PAKExtract\PAKExtract.sln"
if %ERRORLEVEL% NEQ 0 pause

if exist C:\MinGW\bin\gcc.exe (
	C:\MinGW\bin\gcc.exe -shared -O2 -s "UnPAK\unpak.c" -o "UnPAK\bin\Release\UnPAK.dll"
	if %ERRORLEVEL% NEQ 0 pause
) 

"%PROGRAMFILES%\7-Zip\7z" a -tzip "LifeDISA.zip" ^
 ".\LifeDISA\LifeDISA\bin\Release\net9.0-windows\*" ^
 "-x!*\" ^
 "-mx=9"
if %ERRORLEVEL% NEQ 0 pause
 
"%PROGRAMFILES%\7-Zip\7z" a -tzip "MemoryViewer.zip" ^
 ".\MemoryViewer\MemoryViewer\bin\Release\net9.0-windows\*" ^
 "-x!*\" ^
 "-mx=9"
if %ERRORLEVEL% NEQ 0 pause
 
"%PROGRAMFILES%\7-Zip\7z" a -tzip "TrackDISA.zip" ^
 ".\TrackDISA\TrackDISA\bin\Release\net9.0\*" ^
 "-x!*\" ^
 "-mx=9"
if %ERRORLEVEL% NEQ 0 pause
 
"%PROGRAMFILES%\7-Zip\7z" a -tzip "VarsViewer.zip" ^
 ".\VarsViewer\VarsViewer\bin\Release\net9.0\*" ^
 "-x!*\" ^
 "-mx=9"
if %ERRORLEVEL% NEQ 0 pause
 
"%PROGRAMFILES%\7-Zip\7z" a -tzip "PAKExtract.zip" ^
 ".\PAKExtract\PAKExtract\bin\Release\net9.0-windows\*" ^
 "-x!*\" ^
 "-mx=9"
if %ERRORLEVEL% NEQ 0 pause
 
"%PROGRAMFILES%\7-Zip\7z" a -tzip "AITD-tools.zip" ^
 ".\LifeDISA\LifeDISA\bin\Debug\net9.0-windows\GAMEDATA\vars.txt" ^
 "-x!*\" ^
 "LifeDISA.zip" ^
 "MemoryViewer.zip" ^
 "TrackDISA.zip" ^
 "VarsViewer.zip" ^
 "PAKExtract.zip" ^
 "-mx=9" 
if %ERRORLEVEL% NEQ 0 pause

"%PROGRAMFILES%\7-Zip\7z" rn "AITD-tools.zip" "vars.txt" "GAMEDATA\vars.txt"
if %ERRORLEVEL% NEQ 0 pause

del "LifeDISA.zip"
del "MemoryViewer.zip"
del "TrackDISA.zip"
del "VarsViewer.zip"
del "PAKExtract.zip"

