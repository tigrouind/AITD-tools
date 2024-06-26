# LifeDISA

This is a disassembler for LIFE scripts in Alone in the Dark series.

## Supported
* Alone in the Dark 1 (CD-ROM, floppy, demo)
* Alone in the Dark 2 (CD-ROM, floppy, demo)
* Alone in the Dark 3 (CD-ROM, demo)
* Jack in the Dark
* Time Gate: Knight's Chase (CD-ROM, demo)

## Command-line arguments : 
```
LifeDISA -version VERSION [-raw] [-verbose] [-output FILENAME]
```
| Argument | Description |
|-|-|
| -version VERSION | Must be one of the following values:<br>AITD1, AITD1_FLOPPY, AITD1_DEMO<br>AITD2, AITD2_DEMO<br>AITD3<br>JACK<br>TIMEGATE, TIMEGATE_DEMO |
| -raw | Raw output. Disable IF ELSE and SWITCH CASE statements detection and indentation |
| -verbose | Display byte information on the left side of the disassembly |
| -output | Output filename |

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

## Command-line arguments : 
```
TrackDISA -version VERSION [-verbose] [-output FILENAME]
```
| Argument | Description |
|-|-|
| -version VERSION | Must be one of the following values:<br>AITD1, AITD1_FLOPPY, AITD1_DEMO<br>AITD2, AITD2_DEMO<br>AITD3<br>JACK<br>TIMEGATE, TIMEGATE_DEMO |
| -verbose | Display byte information on the left side of the disassembly |
| -output | Output filename |

## Instructions  

Copy file *LISTTRAK.PAK* into a folder named GAMEDATA

# MemoryViewer

Allow to view DOS memory in realtime. Each pixel is a byte.
Current VGA palette is automatically loaded.

Supported :
* All DOS games (even non AITD related)

## Command-line arguments : 
```
MemoryViewer [-screen-width WIDTH] [-screen-height HEIGHT] [-zoom ZOOM]
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

## Command-line arguments : 
```
VarsViewer [-screen-width WIDTH] [-screen-height HEIGHT]
```

## Commands
| Key | Description |
| :-: | - |
| <kbd>f</kbd> | freeze capture
| <kbd>s</kbd> | save state
| <kbd>c</kbd> | compare current state with saved state

# CacheViewer

## Supported 
* Alone in the Dark 1 (CD-ROM, floppy, demo)

## Commands
| Key | Description |
| :-: | - |
| <kbd>s</kbd> | change sort mode (default, memory, lru)
| <kbd>space</kbd> | display cache entries timestamp / name
| <kbd>F5</kbd> | clear all cache entries

# PAKExtract

Extracts files from PAK files.

Supported :
* Alone in the Dark 1, 2 and 3
* Jack in the Dark
* Time Gate: Knight's Chase

## Instructions  
- Copy PAK files to GAMEDATA folder
- Run PAKExtract

## Command-line arguments : 
```
PAKExtract  [-raw] [-AITD1] [<files>]
```
| Argument | Description |
|-|-|
| -raw | Extract files in binary data, without extra processing |
| -AITD1 | Extract overlay masks from ETAGExx.PAK files |
| &lt;files&gt; | Specifies a list of one or more PAK files to be extracted. If no not specified, all PAK files in GAMEDATA folder are extracted |

Files that are exported as PNG: 
- backgrounds (CAMERAxx.PAK and ITD_RESS.PAK)
- overlay masks (ETAGExx.PAK (AITD1), MASKxx.PAK or NASKxx.PAK (AITD2/3), MKxxxxxx.PAK or NKxxxxxx.PAK (Time Gate)