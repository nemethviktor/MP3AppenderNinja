# Welcome to MP3AppendNinja
## What This Is/Does (?)
Great question! This is a console app (no GUI) for Windows that appends mp3s together within a loop of subfolders. In somewhat plainer  English that means you designate a "root" folder (say `C:\audiobooks`) and then in each of the subfolders (such as `C:\audiobooks\Cryptonomicon`, `C:\audiobooks\Snowcrash` etc) the script will parse those subfolders and get all your mp3s into one merged file per folder.
Things it may also do/certainly does: 
- it will delete your original files (see further below)
- it will create an output file that has the same name as the folder (so following from the logic above if you have `C:\audiobooks\Snowcrash\chapter 1.mp3` and `C:\audiobooks\Snowcrash\chapter 2.mp3` then you'll eventually end up with a `C:\audiobooks\Snowcrash\Snowcrash.mp3` and nothing else)

## Download & Install // Requirements

- There's nothing to install. Just get the zip file from Releases, unzip and run the .exe (read below for arguments/howto)
- You'll need Windows 7 x64 or newer and .NET 8
    - You can get .NET 8 at https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/runtime-8.0.4-windows-x64-installer should you need it.
- You'll need FFMPEG --> https://www.gyan.dev/ffmpeg/builds/ffmpeg-git-full.7z (you'll need to unzip [un7z?] that // in all fairness any FFMPEG version should do)
- The app is unsigned. This is because a certificate costs in the vicinity of £250 per year but the app is free, and I don't particularly feel like splurging out on this at the moment.
	- Due to the lack of a signed certificate, when running the app SmartScreen may complain that the app is unsafe. SmartScreen is meant to be called StupidScreen but MS made a typo there.

## How to use

As mentioned above you need to run the exe file. This is a console app (aka no GUI) and the usage is `MP3AppenderNinja.exe -f fullPathToFFMPEG.exeInDoubleQuotes -p FullPathToRootFolderToParseInDoubleQuotes`, so for example in real terms that would be `MP3AppenderNinja.exe -f "d:\Downloads\ffmpeg-6.0-full_build\bin\ffmpeg.exe" -p "d:\Downloads\Warhammer 40k The Horus Heresy - complete\"`

## Things to Note

- The main _to-note_ is that the script isn't really foolproof and it's unlikely I'll ever cater for that. The assumption is that the files aren't locked, they aren't read-only [in other words turn off your torrent client], the script (technically the user running it) has read/write access to whatever folder and so on. 
	- I'd generally suggest not running the script with a target on a Google Drive/OneDrive folder because those clients can *really* interfere w/ write-operations.
	
### Sorting

- There's some logic to attempt re-sort files. The issue is that if you have files like `whatever chapter 1.mp3` and `whatever chapter 2.mp3` and `whatever chapter 10.mp3` and `whatever chapter 11.mp3` then you'll have a sorting problem because `1` and `10` are strings, not numbers in the world of Windows so there's a fair bit of code that goes in to changing anything that looks like a number to a 3-padded (aka `1` becomes `001` --> `whatever chapter 001.mp3`). This works in most cases.
	- As I've written the script isn't idiot-proof. I've seen some files that were `000 intro.mp3` and then `000 quotes.mp3`; in such cases alphabetic sort will kick in so basically make sure whatever you're appending is following some form of reasonable naming procedure.

### Errors

- The app only works with mp3 files, this is a feature, not a bug.
- It's likely you'll see something in the output such as `Audio packet of size 215 (starting with 00610069...) is invalid, writing it anyway.` - this isn't a bug either, ffmpeg does it own things
- The duration of the output file may be misreported by some players. Again this is a thing of ffmpeg but from what I gather the alternatives don't do a better job on the merge either.