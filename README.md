# LifeDISA

This is a disassembler for LIFE scripts in Alone in the Dark series.

Supported :
* Alone in the Dark 1 (CD-ROM, floppy, demo)
* Alone in the Dark 2 (CD-ROM, floppy, demo)
* Alone in the Dark 3 (CD-ROM, demo)
* Jack in the Dark
* Time Gate: Knight's Chase (CD-ROM, demo)

## Command-line arguments : 
```
LifeDISA {AITD1|AITD1_FLOPPY|JACK|AITD2|AITD2_DEMO|AITD3|TIMEGATE|TIMEGATE_DEMO}
```

## How to use : 

1. Create a new folder named "GAMEDATA" (located in same folder as the LifeDISA executable).
2. Copy the following files from game to GAMEDATA :
   - *LISTLIFE.PAK*
   - *ENGLISH.PAK* (or *FRANCAIS.PAK*)
3. Copy the file *OBJETS.ITD* (from AITD folder) into GAMEDATA folder.
4. Start LifeDISA executable with appropriate arguments. A file named *output.txt* will be created.

# TrackDISA

Copy file *LISTTRAK.PAK* into a folder named GAMEDATA

# MemoryViewer
## Command-line arguments : 
```
MemoryViewer [-screen-width width] [-screen-height height] [-zoom zoom]
```

## Commands
| Key | Description |
| :-: | - |
| <kbd>space</kbd> | display memory allocation blocks
| <kbd>ctrl</kbd> + <kbd>mouse wheel</kbd> <br> <kbd>ctrl</kbd> + <kbd>+</kbd> or <kbd>-</kbd>| increase / decrease zoom
| <kbd>ctrl</kbd> + <kbd>0</kbd> | reset zoom

# VarsViewer

## Command-line arguments : 
```
VarsViewer [-screen-width width] [-screen-height height]
```

## Commands
| Key | Description |
| :-: | - |
| <kbd>f</kbd> | freeze capture
| <kbd>s</kbd> | save state
| <kbd>c</kbd> | compare current state with saved state

## Commands
| Key | Description |
| :-: | - |
| <kbd>F5</kbd> | clear all cache entries (ideally, the game should be paused before by pressing `p`)