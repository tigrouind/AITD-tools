⚠️ some files in the release archive might be detected as malware by some A/V (eg: Windows Defender). The exact reason is unclear but this is probably because it use Win32 API calls such as `ReadProcessMemory` and `WriteProcessMemory`. If you know how to fix this, please let me know.

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

## Supported 
Same as [LifeDISA](#LifeDISA)

## Command-line arguments
```
TrackDISA -version {AITD1|AITD1_FLOPPY|AITD1_DEMO|AITD2|AITD2_DEMO|AITD3|JACK|TIMEGATE|TIMEGATE_DEMO}
          [-verbose] 
          [-output FILENAME]
```

## Instructions  

Copy file *LISTTRAK.PAK* into a folder named GAMEDATA

# MemoryViewer

Allow to view DOS memory in realtime. Each pixel is a byte.
Current VGA palette is automatically loaded.

Supported :
* All DOS games (even non AITD related)

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

You can also drop files or folders to be extracted into PAKExtract executable.

## Command-line arguments
```
PAKExtract [-background]
           [-svg [rotate {0|90|180|270}] [zoom] [room 1,2,3,...] [trigger]]
           [-info]
           [-update]
           [<files>]
           [<folders>]
```

### Converting backgrounds as PNG files
Extract necessary PAK files (CAMERAxx.PAK, ITD_RESS.PAK) into their respective folders, then run PAKEXtract again : 
```
PAKExtract -background
```
Files will be exported to BACKGROUND folder.

### Rendering floors as SVG files
Extract necessary PAK files (ETAGExx.PAK) into their respective folders, then run PAKEXtract again : 
```
PAKExtract -svg rotate 90 room 5,7 
```
Files will be exported to SVG folder.

### Displaying archive information only (eg: compressed size, flags, ...) 
```
PAKExtract LISTLIFE.PAK -info 
```

### Updating entries in the archive
Extract some files, edit them in their respective folders (eg: LISTLIFE), then run PAKExtract again :
```
PAKExtract LISTLIFE.PAK -update 
```
