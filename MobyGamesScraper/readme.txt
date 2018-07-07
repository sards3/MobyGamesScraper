To use this program, you need a MobyGames API key. You can specify the key in one of two ways:

1. At the command line with the -k option, e.g.: MobyGamesScraper -k YOURKEYGOESHERE
2. In a file named mobyapikey.txt, located in the same directory as the MobyGamesScraper.exe file. This is useful if you don't want to keep supplying the key at the command line.

There are two commands available:

1. "platforms" (Usage: MobyGamesScraper platforms): This command lists all of the available platforms, with their numerical IDs and names.
2. "games": This command will retrieve all of the games from the specified platforms and export them to CSV files.

The games command has a required option: -p. (-p is short for platform.) This option can be supplied in a few different ways:
1. One or more numerical platform IDs, comma separated. Usage: MobyGamesScraper games -p 3,4,5
2. One or more platform names, non-case-sensitive, comma separated. Usage: MobyGamesScraper games -p dos,windows
3. "all": This will retrieve all games from all platforms. Usage: MobyGamesScraper games -p all

The CSV files will be saved to the same directory as the MobyGamesScraper.exe file, with filenames matching the platform names (e.g. DOS.csv). 
Be aware that existing files will be overwritten.