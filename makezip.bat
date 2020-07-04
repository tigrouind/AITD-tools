@echo off
"%PROGRAMFILES%\7-Zip\7z" a -tzip "AITD-tools.zip" ^
 ".\CacheViewer\CacheViewer\bin\Release\*.exe" ^
 ".\LifeDISA\LifeDISA\bin\Release\*.exe" ^
 ".\LifeDISA\LifeDISA\bin\Release\UnPAK.dll" ^
 ".\MemoryViewer\MemoryViewer\bin\Release\*.exe" ^
 ".\MemoryViewer\MemoryViewer\bin\Release\*.dll" ^
 ".\TrackDISA\TrackDISA\bin\Release\*.exe" ^
 ".\VarsViewer\VarsViewer\bin\Release\*.exe" 
