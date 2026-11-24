====================================================================
 Soviet Jump Game - Patch
====================================================================

This bundle patches Soviet Jump Game (Steam AppID 1072710) so it runs
again after its live services were shut down. It includes everything
needed - BepInEx is bundled, so you install nothing and need no
internet connection.

WHAT'S INSIDE
  BepInEx/              The mod loader (BepInEx 5 x64)
  winhttp.dll           Boots BepInEx when the game starts
  doorstop_config.ini   Loader settings
  SjgPatch.dll          The patch itself
  Install.bat           Windows installer (double-click)
  install.sh            Linux / Steam Proton installer

--------------------------------------------------------------------
 WINDOWS
--------------------------------------------------------------------
  1. Double-click  Install.bat
     (if it can't find the game, drag the game folder onto the file,
      or paste the path when prompted)
  2. Launch the game through Steam as usual.

--------------------------------------------------------------------
 LINUX (Steam / Proton)
--------------------------------------------------------------------
  1. In a terminal, in this folder:

         ./install.sh

     (add the game path if auto-detection fails, e.g.
      ./install.sh "$HOME/.steam/steam/steamapps/common/Soviet Jump Game")

  2. In Steam, right-click the game -> Properties -> Launch Options, and set:

         WINEDLLOVERRIDES="winhttp=n,b" %command%

  3. Launch the game through Steam as usual.

--------------------------------------------------------------------
 ALREADY HAVE BepInEx?
--------------------------------------------------------------------
Then you can skip the installers entirely and just drop  SjgPatch.dll
into:   <game>/BepInEx/plugins/

--------------------------------------------------------------------
 UNINSTALL
--------------------------------------------------------------------
  Windows:  double-click  Uninstall.bat
            (run "Uninstall.bat <game folder> -WithBepInEx" to also
             remove the BepInEx loader)
  Linux:    ./uninstall.sh "/path/to/game" [--with-bepinex]

--------------------------------------------------------------------
 USAGE
--------------------------------------------------------------------
  PRACTICE              play the tutorial offline
  MATCHMAKING           connect to the default server (or host a bot match)
  F8                    force-host a local bot match
  F6                    join the server set in Server.DirectConnectAddress

  NOTE: the game's own Rooms (private match / room code) and
  server/region selection menu entries do not work - they still call
  the shut-down live services.

  The installers ask which server to use and write it to the config
  file (just press Enter to accept the default, sjg.dnd.wtf):

    BepInEx/config/games.gfi.sjg.patch.cfg   ->   [Server] ApiBaseUrl

Documentation: https://github.com/gfisher1/soviet-jump-game-patch/wiki
