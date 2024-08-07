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
MemoryViewer [-screen-width WIDTH]
             [-screen-height HEIGHT]
             [-zoom ZOOM]
```

## Commands
| Key | Description |
| :-: | - |
| <kbd>ctrl</kbd> + <kbd>mouse wheel</kbd> <br> <kbd>ctrl</kbd> + <kbd>+</kbd> or <kbd>-</kbd>| increase / decrease zoom
| <kbd>ctrl</kbd> + <kbd>0</kbd> | reset zoom
| <kbd>page up</kbd> | decrease memory offset by 640KB
| <kbd>page down</kbd> | increase memory offset by 640KB
| <kbd>space</kbd> | display DOS memory control blocks (MCBs)<br>green = free<br>blue = used<br>red = current executable<br>yellow = not current executable
| <kbd>p</kbd> | show palette

# VarsViewer

## Supported
* Alone in the Dark 1 (CD-ROM, floppy, demo)

## Command-line arguments
```
VarsViewer [-view {vars|cache|actor|object}]
           [-width WIDTH -height HEIGHT]
		   [-fontsize SIZE]
```

Setting console width, height or font size seems to not be supported by Windows 10/11 Terminal.
You have to use legacy console host.

## Commands 

| Key | Description |
| :-: | - |
| <kbd>F1</kbd> | vars view
| <kbd>F2</kbd> | cache view
| <kbd>F4</kbd> | actor view
| <kbd>F3</kbd> | object view
| <kbd>control</kbd> + <kbd>mouse wheel</kbd> | zoom in/out

## Commands (vars)
| Key | Description |
| :-: | - |
| <kbd>f</kbd> | freeze capture
| <kbd>s</kbd> | save state
| <kbd>c</kbd> | compare current state with saved state

## Commands (cache)
| Key | Description |
| :-: | - |
| <kbd>s</kbd> | change sort mode (default, memory, lru)
| <kbd>space</kbd> | display cache entries timestamp / name
| <kbd>F5</kbd> | clear all cache entries

## Commands (actor/object)
| Key | Description |
| :-: | - |
| <kbd>space</kbd> | show/hide inactive rows
| <kbd>page up</kbd> / <kbd>page down</kbd> | scroll
| <kbd>F5</kbd> | force display refresh

# PAKExtract

Extracts files from PAK files.

## Supported
Same as [LifeDISA](#LifeDISA)

## Instructions  
- Copy PAK files to GAMEDATA folder
- Run PAKExtract (all PAK files in GAMEDATA folder are extracted)

You can also drag and drop a single file into PAKExtract executable

## Command-line arguments
```
PAKExtract [-background]
           [-svg "[rotate {0|90|180|270}] [room 1,2,3,...]"]
           [<files>]
```

## Required files
- CAMERAxx.PAK, ITD_RESS.PAK (backgrounds as png)
- ETAGExx.PAK (floor colliders as svg)