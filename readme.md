A tool for extracting original text from, and patching translated text into, a CatSystem2 script format (`.cst`)

Remake from [VNTextPatch](https://github.com/arcusmaximus/VNTranslationTools) with custom fontsize

# Usage:
```
  VNCS2.exe extract <inputfolder> <scriptfolder|scriptfile(.xlsx)>
  VNCS2.exe insert <inputfolder> <scriptfolder|scriptfile(.xlsx)> <outputfolder>
  VNCS2.exe insert [fontsize option] <inputfolder> <scriptfolder|scriptfile(.xlsx)> <outputfolder>
```
# Ingfo Fontsize:
```
  --fss          = font extra small
  --fs           = font small (Default)
  --fn           = font normal
  --f            = font normal
  --fl           = font large
  --fl           = font extra large
  --f[number]    = font custom number
```
# Example:
```
  VNCS2.exe insert --fss Amakano2 Amakano2.xlsx Amakano2TL
  VNCS2.exe insert --f18 Amakano2 Amakano2.xlsx Amakano2TL
```