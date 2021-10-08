@echo off
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
 ".\VarsViewer\VarsViewer\bin\Release\*.exe" 
