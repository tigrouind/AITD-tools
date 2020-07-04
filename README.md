

# LifeDISA

This is a disassembler for LIFE scripts in Alone In The Dark (1992).

Both floppy version and CD-ROM version are supported. AITD2 and 3 are not supported.

## How to use : 

1. Create a new folder named "GAMEDATA" (located in same folder as the LifeDISA executable).
3. Copy the following files from game to GAMEDATA :
   - *LISTLIFE.PAK*
   - *ENGLISH.PAK* (or *FRANCAIS.PAK*)
4. Copy the file *OBJETS.ITD* (from AITD folder) into GAMEDATA folder.
5. Start LifeDISA executable. A file named *output.txt* will be created

# TrackDISA

Extract all files located in *LISTTRAK.PAK* into a folder named GAMEDATA

# MemoryViewer
## Command-line arguments : 
```
MemoryViewer [-screen-width width] [-screen-height height] [-zoom zoom]
```
Press `space` to display memory allocation blocks

# CacheViewer
## Command-line arguments : 
```
CacheViewer [ListSamp] [ListBody] [ListBod2] [ListAnim] [ListAni2] [ListLife] [ListTrak] [_MEMORY_]
```
A maximum of 6 columns is supported

# VarsViewer

## Command-line arguments : 
```
VarsViewer [-screen-width width] [-screen-height height]
```

Press `f` to freeze capture<br/>
Press `s` to save state<br/>
Press `c` to compare states
