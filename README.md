# LifeDISA

This is a disassembler for LIFE scripts in Alone in the Dark series.

Supported :
* Alone in the Dark 1 (floppy and CD-ROM)
* Alone in the Dark 2 / 3
* Jack in the Dark

Not supported :
* Time Gate: Knight's Chase

## How to use : 

1. Create a new folder named "GAMEDATA" (located in same folder as the LifeDISA executable).
3. Copy the following files from game to GAMEDATA :
   - *LISTLIFE.PAK*
   - *ENGLISH.PAK* (or *FRANCAIS.PAK*)
4. Copy the file *OBJETS.ITD* (from AITD folder) into GAMEDATA folder.
5. Start LifeDISA executable. A file named *output.txt* will be created.

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

Copy file *ITD_RESS.PAK* to same folder as executable to get palette to work.

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

# CacheViewer
## Command-line arguments : 
```
CacheViewer [ListSamp] [ListLife] [ListBody|ListBod2] [ListAnim|ListAni2] [ListTrak] [_MEMORY_]
```

## Commands
| Key | Description |
| :-: | - |
| <kbd>F5</kbd> | clear all cache entries (ideally, the game should be paused before by pressing `p`)