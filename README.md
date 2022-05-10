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
LifeDISA -version VERSION [-raw]
```
| Argument | Description |
|-|-|
| -version VERSION | Must be one of the following values:<br>AITD1, AITD1_FLOPPY, AITD1_DEMO<br>AITD2, AITD2_DEMO<br>AITD3<br>JACK<br>TIMEGATE, TIMEGATE_DEMO |
| -raw | Raw output. Disable IF ELSE or SWITCH CASE statements detection and indentation |



## How to use : 

1. Create a new folder named "GAMEDATA" (located in same folder as the LifeDISA executable).
2. Copy the following files from game to GAMEDATA :
   - *LISTLIFE.PAK*
   - *ENGLISH.PAK* (or *FRANCAIS.PAK*)
3. Copy the file *OBJETS.ITD* (from AITD folder) into GAMEDATA folder.
4. Start LifeDISA executable with appropriate arguments. A file named *output.txt* will be created.

## Syntax highlighting (Notepad++)

Download file [here](https://github.com/tigrouind/AITD-tools/releases/download/1.40/AITD.xml)

How to install it :
1. Click on `Language` > `User Defined Language` > `Define your language...`
2. Click `Import...`
3. In the opening dialog, choose the xml file you downloaded previously.

# TrackDISA

Copy file *LISTTRAK.PAK* into a folder named GAMEDATA

# MemoryViewer

Supported :
* All DOS games

## Command-line arguments : 
```
MemoryViewer [-screen-width WIDTH] [-screen-height HEIGHT] [-zoom ZOOM]
```

## Commands
| Key | Description |
| :-: | - |
| <kbd>space</kbd> | display memory allocation blocks
| <kbd>ctrl</kbd> + <kbd>mouse wheel</kbd> <br> <kbd>ctrl</kbd> + <kbd>+</kbd> or <kbd>-</kbd>| increase / decrease zoom
| <kbd>ctrl</kbd> + <kbd>0</kbd> | reset zoom

# VarsViewer

Supported :
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

Supported :
* Alone in the Dark 1 (CD-ROM, floppy, demo)

## Commands
| Key | Description |
| :-: | - |
| <kbd>s</kbd> | sort entries by timestamp
| <kbd>space</kbd> | display cache entries timestamp / name
| <kbd>F5</kbd> | clear all cache entries