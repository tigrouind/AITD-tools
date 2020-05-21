

# LifeDISA

This is a disassembler for LIFE scripts in Alone In The Dark (1992).

Both floppy version and CD-ROM version are supported. AITD2 and 3 are not supported.

## How to use : 

1. Download [QuickBMS](http://aluigi.altervista.org/quickbms.htm) and [alonedark.bms](http://aluigi.altervista.org/bms/alonedark.bms) script ([alternative link](https://github.com/tigrouind/AITD-roomviewer/releases/download/1.1.14/alonedark.bms)).
2. Create a new folder named "GAMEDATA" (located in same folder as the LifeDISA executable).
3. Use *QuickBMS* to extract the following PAK files (they are located in game folder) into GAMEDATA :
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
