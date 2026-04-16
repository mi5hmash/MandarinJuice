[![License: MIT](https://img.shields.io/badge/License-MIT-blueviolet.svg)](https://opensource.org/license/mit)
[![Release Version](https://img.shields.io/github/v/tag/mi5hmash/MandarinJuice?label=Version)](https://github.com/mi5hmash/MandarinJuice/releases/latest)
[![Visual Studio 2026](https://custom-icon-badges.demolab.com/badge/Visual%20Studio%202026-F0ECF8.svg?&logo=visual-studio-26)](https://visualstudio.microsoft.com/)
[![.NET10](https://img.shields.io/badge/.NET%2010-512BD4?logo=dotnet&logoColor=fff)](#)

> [!IMPORTANT]
> **This software is free and open source. If someone asks you to pay for it, it's likely a scam.**

# 🍊 MandarinJuice - What is it :interrobang:
This application can **decrypt and encrypt SaveData files** from various games running on RE Engine. It can also **re-sign these SaveData files** with your own SteamID to **use anyone’s SaveData on your Steam Account**.

## Supported game titles
|Game Title|App ID|Platform|
|---|---|---|
|Dead Rising Deluxe Remaster|2527390|Steam|
|Dragon's Dogma 2|2054970|Steam|
|Kunitsu-Gami Path of the Goddess|2510710|Steam|
|Mega Man Star Force Legacy Collection|3500390|Steam|
|Monster Hunter Stories 3: Twisted Reflection|2852190|Steam|
|Monster Hunter Wilds|2246340|Steam|
|PRAGMATA|3357650|Steam|
|Resident Evil 9 Requiem|3764200|Steam|

# 🤯 Why was it created :interrobang:
I wanted to share a SaveData file with a friend, but it isn't possible by default.

# :scream: Is it safe?
The short answer is: **No.** 
> [!CAUTION]
> If you unreasonably edit your SaveData files, you risk corrupting them or getting banned from playing online. In both cases, you will lose your progress.

> [!IMPORTANT]
> Always create a backup of any files before editing them.

> [!IMPORTANT]
> Disable the Steam Cloud before you replace any SaveData files.

You’ve been warned. Now that you fully understand the possible consequences, you may proceed to the next chapter.

# :scroll: How to use this tool
## [GUI] - 🪟 Windows 
> [!IMPORTANT]
> If you’re working on Linux or macOS, skip this chapter and move on to the next one.

On Windows, you can use either the CLI or the GUI version, but in this chapter I’ll describe the latter.

<img src="https://github.com/mi5hmash/MandarinJuice/blob/main/.resources/images/MainWindow-v1.png" alt="MainWindow-v1"/>

#### 1. Selecting the Game Profile
Game Profile is a configuration file that stores the settings for a specific game.
In plain terms, it tells my application how it should behave for that particular game.
I include a package with ready‑to‑use Game Profile files (**profiles.zip**) in the release section.
The ***"_profiles"*** folder inside that package, containing the Game Profile files, should be placed in the same directory as the program’s executable.
Button **(2)** opens the local ***"_profiles"*** folder.

#### 2. Setting the Input Directory
You can set the input folder in whichever way feels most convenient:
- **Drag & drop:** Drop SaveData file - or the folder containing it - onto the TextBox **(3)**.
- **Pick a folder manually:** Click the button **(4)** to open a folder‑picker window and browse to the directory where SaveData file is.
- **Type it in:** If you already know the path, simply enter it directly into the TextBox **(3)**.

#### 3. Entering the User ID
In the case of Steam, your User ID is 64-bit SteamID.  
One way to find it is by using the SteamDB calculator at [steamdb.info](https://steamdb.info/calculator/).

#### 4. Re-signing SaveData files
If you want to re‑sign your SaveData file/s so it works on another Steam account, select the Game Profile **(1)** corresponding to the game from which the save file comes. Once you have it selected, type the User ID of the account that originally created that SaveData file/s into the TextBox **(5)**. Then enter the User ID of the account that should be allowed to use that SaveData file/s into the TextBox **(7)**. Finally, press the **"Re-sign All"** button **(11)**.

> [!NOTE]
> The re‑signed files will be placed in a newly created folder within the ***"MandarinJuice/_OUTPUT/"*** folder.

#### 5. Accessing modified files
Modified files are being placed in a newly created folder within the ***"MandarinJuice/_OUTPUT/"*** folder. You may open this directory in a new File Explorer window by pressing the button **(12)**.

> [!NOTE]
> After you locate the modified files, you can copy them into your save‑game folder.

### ADVANCED OPERATIONS

#### Enabling SuperUser Mode

> [!WARNING]
> This mode is for advanced users only.

If you really need it, you can enable SuperUser mode by triple-clicking the version number label **(13)**.

#### Decrypting SaveData files

> [!IMPORTANT]
> This button is visible only when the SuperUser Mode is Enabled. 

If you want to decrypt SaveData file\s to read its content, select the Game Profile **(1)** corresponding to the game from which the SaveData file comes, type the User ID of the account that originally created that SaveData file/s into the TextBox **(5)**, and press the **"Decrypt All"** button **(8)**.

#### Encrypting SaveData files

> [!IMPORTANT]
> This button is visible only when the SuperUser Mode is Enabled. 

If you want to encrypt the decrypted SaveData file\s, select the Game Profile **(1)** corresponding to the game from which the SaveData file comes, enter the User ID of the account that should be allowed to use that SaveData file/s into the TextBox **(7)**, and press the **"Encrypt All"** button **(9)**.

### OTHER BUTTONS
Button **(6)** swaps the values in the **"User ID (INPUT)"** and **"User ID (OUTPUT)"** TextBoxes.
Button **(10)** cancels the currently running operation.
Button **(14)** uses a brute‑force approach to find the correct UserID for source SaveData file.

## [CLI] - 🪟 Windows | 🐧 Linux | 🍎 macOS

```plaintext
Usage: .\mandarin-juice-cli.exe -m <mode> [options]

Modes:
  -m d  Decrypt SaveData files
  -m e  Encrypt SaveData files
  -m r  Re-sign SaveData files
  -m b  Bruteforce User ID for the first file in the input directory

Options:
  -g <game_profile_path>  Path to the Game Profile file
  -p <path>               Path to folder containing SaveData files
  -u <user_id>            User ID (used in decrypt/encrypt modes)
  -uI <old_id>            Original User ID (used in re-sign mode)
  -uO <new_id>            New User ID (used in re-sign mode)
  -q                      Don't wait for user input to exit after operation completes (auto-close)
  -h                      Show this help message
```

### Examples
#### Decrypt
```bash
.\mandarin-juice-cli.exe -m d -g ".\game_profile.bin" -p ".\InputDirectory" -u 76561197960265729
```
#### Encrypt
```bash
.\mandarin-juice-cli.exe -m e -g ".\game_profile.bin" -p ".\InputDirectory" -u 76561197960265730
```
#### Re-sign
```bash
.\mandarin-juice-cli.exe -m r -g ".\game_profile.bin" -p ".\InputDirectory" -uI 76561197960265729 -uO 76561197960265730
```
#### Bruteforce
```bash
.\mandarin-juice-cli.exe -m b -g ".\game_profile.bin" -p ".\InputDirectory"
```

> [!NOTE]
> Modified files are being placed in a newly created folder within the ***"MandarinJuice/_OUTPUT/"*** folder.

# :fire: Issues
All the problems I've encountered during my tests have been fixed on the go. If you find any other issues (which I hope you won't) feel free to report them [there](https://github.com/mi5hmash/MandarinJuice/issues).

> [!TIP]
> This application creates a log file that may be helpful in troubleshooting.
It can be found in the same directory as the executable file.
Application stores up to two log files from the most recent sessions.

## [ISSUE] Game doesn’t detect SaveData file\s on Steam
If you purchased the game on Steam and it doesn’t detect your re‑signed SaveData files after you’ve placed them in the correct folder, the issue is usually that a valid `remotecache.vdf` file also needs to be generated.

Instructions on how to generate a proper `remotecache.vdf` can be found in [this gist](https://gist.github.com/mi5hmash/47f1be53d213be9b00f2c7e0aa151b11).

## [ISSUE] Not all controls are visible in the WPF application on Windows
You probably have your Windows system font size set higher than the default.
Set the font size back to the default value, or press **`CTRL + SHIFT + J`** to unlock window resizing in the application.

# 🔄 Alternatives  
If you’re looking for a save editor (not just a re-signer or deencryptor), you might be interested in [ree-save-editor](https://github.com/kvasszn/ree-save-editor) by [kvasszn](https://github.com/kvasszn).
