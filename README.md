> [!CAUTION] 
> Some files in the release archive might be detected as malware by some A/V (eg: Windows Defender). The exact reason is unclear but this is probably because it use Win32 API calls such as `ReadProcessMemory` and `WriteProcessMemory`. If you know how to fix this, please let me know.

# Requirements

You need to install [.NET Desktop Runtime 9.0](https://dotnet.microsoft.com/en-us/download/dotnet/9.0) (requires Windows 10 or newer) to run the tools. Unless you are a developer, you don't need the SDK, the runtime Windows x64 version is enough.

# LifeDISA

This is a disassembler for LIFE scripts in Alone in the Dark series.


## Supported
* Alone in the Dark 1 / 2 / 3 (CD-ROM, floppy, demo)
* Jack in the Dark
* Time Gate: Knight's Chase (CD-ROM, demo)

## Command-line arguments
```
LifeDISA -version {AITD1|AITD1_FLOPPY|AITD1_DEMO|AITD2|AITD2_DEMO|AITD3|JACK|TIMEGATE|TIMEGATE_DEMO}
         [-raw]  
         [-verbose] 
         [-output FILENAME]
```

## Instructions 

1. Create a new folder named "GAMEDATA" (located in same folder as the LifeDISA executable).
2. Copy the following files from game to GAMEDATA :
   - *LISTLIFE.PAK*
   - *ENGLISH.PAK* (or *FRANCAIS.PAK*)
3. Copy the file *OBJETS.ITD* (from AITD folder) into GAMEDATA folder.
4. Start LifeDISA executable with appropriate arguments. A file named *output.txt* will be created.

# TrackDISA

This is a disassembler for TRACK scripts in Alone in the Dark series.

## Supported 
Same as [LifeDISA](#LifeDISA)

## Command-line arguments
```
TrackDISA -version {AITD1|AITD1_FLOPPY|AITD1_DEMO|AITD2|AITD2_DEMO|AITD3|JACK|TIMEGATE|TIMEGATE_DEMO}
          [-verbose] 
          [-output FILENAME]
```

## Instructions  

Copy file *LISTTRAK.PAK* into a folder named GAMEDATA, then run TrackDISA executable.

# MemoryViewer

Allow to view DOS memory in realtime. Each pixel is a byte.
Current VGA palette is automatically loaded.

Supported :
* Most DOS games (even non AITD related)

## Command-line arguments
```
MemoryViewer [-width WIDTH]
             [-height HEIGHT]
             [-zoom ZOOM]
```

## Commands
| Key | Description |
| :-: | - |
| <kbd>ctrl</kbd> + <kbd>mouse wheel</kbd> <br> <kbd>ctrl</kbd> + <kbd>+</kbd> or <kbd>-</kbd>| increase / decrease zoom
| <kbd>ctrl</kbd> + <kbd>0</kbd> | reset zoom
| <kbd>page up</kbd> / <kbd>page down</kbd> / <kbd>mouse wheel</kbd> | increase / decrease memory offset by 640KB
| <kbd>space</kbd> | display DOS memory control blocks (MCBs)<br>green = free<br>blue = used<br>red = current executable<br>yellow = not current executable
| <kbd>p</kbd> | show palette

# VarsViewer

Allow to view/edit game internals variables (named `VARS` and `CVARS` in scripts), view/clear internal cache, inspect actors/objects, in realtime.

## Supported
* Alone in the Dark 1 (CD-ROM, floppy, demo)

## Command-line arguments
```
VarsViewer [-view {vars|cache|actor|object}]
           [-width WIDTH]
           [-height HEIGHT]
```

## Commands 

| Key | Description |
| :-: | - |
| <kbd>F1</kbd> / <kbd>F2</kbd> / <kbd>F3</kbd> / <kbd>F4</kbd> | vars / cache / actor / object view
| <kbd>control</kbd> + <kbd>mouse wheel</kbd> | zoom in/out
| <kbd>f</kbd> | freeze capture

### Vars
| Key | Description |
| :-: | - |
| <kbd>s</kbd> | save state
| <kbd>c</kbd> | compare current state with saved state

### Cache
| Key | Description |
| :-: | - |
| <kbd>s</kbd> | change sort mode (default, memory, lru)
| <kbd>space</kbd> | display cache entries timestamp / name
| <kbd>F5</kbd> | clear all cache entries

### Actors / objects
| Key | Description |
| :-: | - |
| <kbd>space</kbd> | show/hide inactive actors/objects
| <kbd>tab</kbd> | compact/full view
| <kbd>page up</kbd> / <kbd>page down</kbd> / <kbd>mouse wheel</kbd> | scroll
| <kbd>mouse click</kbd> on column header | hide column
| <kbd>r</kbd> | reset columns visible state

# PAKExtract

Extracts files from PAK files.

## Supported
Same as [LifeDISA](#LifeDISA)

## Instructions
- Copy some PAK files to GAMEDATA folder
- Run PAKExtract : all PAK files in GAMEDATA folder are extracted to new folders (eg: LISTLIFE)

You can also drop files (or folders) to be extracted into PAKExtract executable.

## Command-line arguments
```
PAKExtract [<files|folders>]
           [info [<files|folders>]]
           [background]
           [svg [-rotate {0|90|180|270}] [-zoom] [-room <rooms>] [-trigger] [-camera]]
           [archive [-timegate] [<folders>]]
```

### Extracting files or folders with command line
```
PAKExtract LISTLIFE.PAK LISTTRACK.PAK
PAKExtract FOLDER1 FOLDER2
```

You can also drag and drop files or folder into executables as mentionned earlier.

### Displaying archive information only (eg: compressed size, flags, ...) 
```
PAKExtract info LISTBODY.PAK
```

### Converting backgrounds or textures to PNG files
Extract necessary PAK files (CAMERAxx.PAK, ITD_RESS.PAK, TEXTURES.PAK) into their respective folders, then run PAKExtract again : 
```
PAKExtract background
```
Files will be exported to BACKGROUND folder.

### Rendering floors as SVG files
Extract necessary PAK files (ETAGExx.PAK) into their respective folders, then run PAKExtract again : 
```
PAKExtract svg -rotate 90 -room 1 4 5 
```
Files will be exported to SVG folder.


### Creating a new PAK archive (or editing some entries)
Extract some PAK archives, edit them in their respective folders (eg: LISTLIFE), then run PAKExtract again :
```
PAKExtract archive LISTLIFE
```

> [!NOTE]
> When creating a new archive, entries are not recompressed (which might result in an archive being bigger than expected). AFAIK, there is currently no C source code available for the implode compression algorithm used by AITD. It seems to original game files have been compressed with *PKZIP 1.1*.
>
> If PKZIP and DOSBox are available, PAKExtract will use them for compressing back the files. *PKZIP.EXE 1.1* should be in main folder (same as PAKExtract), and DOSBox should be located in *C:\Program Files\\*, *C:\Program Files (x86)\\* or main folder. 
> 
> Some distributions of PKZIP are a self-extracting executable, so it might be needed to run that executable once (under DOSBox) to extract *PKZIP.EXE* out of it. You can simply drag and drop the PKZIP self-extracting executable on DOSBox. *PKZIP.EXE 1.1* should be around 40KB.


# MoviePlayer

This allow to record a game session and then play it back for study.
All tools from here are compatible as the player imitates DOSBox during playback.

## Instructions
- Copy the file *movie.dat* to same place as executable (which should be named *DOSBox.exe*). By default, it will always load the latest file written with a *.dat* extension.

You can also drop a specific movie on the executable.

## Commands
| Key | Description |
| :-: | - |
| <kbd>F5</kbd> | Play / pause
| <kbd>Shift</kbd> + <kbd>F5</kbd> | Record (any movie being played has to be stopped before)
| <kbd>F6</kbd> | Single frame advance
| <kbd>F7</kbd> | Fast forward (should be held down)
| <kbd>F8</kbd> | Stop
| <kbd>Shift</kbd> + <kbd>1</kbd> ... <kbd>9</kbd> | Save state 1-9
| <kbd>1</kbd> ... <kbd>9</kbd> | Restore state 1-9

