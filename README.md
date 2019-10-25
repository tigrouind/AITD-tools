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