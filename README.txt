Tesla Effect - Subtitle Size Patcher
=====================================

This patcher modifies the game's Assembly-CSharp.dll to add a "Subtitle Size" option 
directly into the in-game Video Settings menu.

INSTALLATION
------------

1. Run the patcher:
   SubtitlePatcher.exe "F:\SteamLibrary\steamapps\common\Tesla Effect\TeslaEffect_Data\Managed\Assembly-CSharp.dll"

   (Replace the path with your actual game installation path)

2. The patcher will:
   - Create a backup: Assembly-CSharp.dll.backup
   - Inject Subtitle Size control into the Settings class
   - Add a "Subtitle Size" slider to the Video Settings menu
   - Patch Voiceover and Video subtitle rendering

3. Launch the game.

CHANGING SUBTITLE SIZE
----------------------

1. Go to Options -> Video in the game menu.
2. Locate the "Subtitle Size" option.
3. Change the value (18 to 96, in steps of 4).
4. Click "OK" to apply.

Your selection is saved to your player profile and will persist across game restarts.

UNINSTALLING
------------

1. Delete the patched Assembly-CSharp.dll
2. Rename Assembly-CSharp.dll.backup to Assembly-CSharp.dll

TROUBLESHOOTING
---------------

- If the game crashes, restore the backup.
- After game updates, you'll need to run the patcher again.

NOTES
-----

- The patcher creates a backup automatically.