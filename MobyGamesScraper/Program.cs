using CsvHelper;
using McMaster.Extensions.CommandLineUtils;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MobyGamesScraper
{

    class Platform
    {
        public int platform_id { get; set; }
        public string platform_name { get; set; }
    }

    class Platforms
    {
        public List<Platform> platforms { get; set; }
    }

    class PlatformRelease
    {
        public string first_release_date { get; set; }
        public int platform_id { get; set; }
        public string platform_name { get; set; }
    }

    class Genre
    {
        public string genre_category { get; set; }
        public int genre_category_id { get; set; }
        public int genre_id { get; set; }
        public string genre_name { get; set; }
    }

    class Game
    {
        public string description { get; set; }
        public int game_id { get; set; }
        public List<Genre> genres { get; set; }
        public decimal moby_score { get; set; }
        public string moby_url { get; set; }
        public int num_votes { get; set; }
        public string official_url { get; set; }
        public List<PlatformRelease> platforms { get; set; }
        // sample_cover
        // sample_screenshots
        public string title { get; set; }
    }

    class Games
    {
        public List<Game> games { get; set; }
    }    

    class GameCsvRow
    {
        public int MobyID { get; set; }
        public string Title { get; set; }
        public string FirstReleaseDate { get; set; }
        public string MobyURL { get; set; }
        public string OfficialURL { get; set; }
        public string Genre { get; set; }        
    }


    class Program
    {
        const string MobyApiBase = "https://api.mobygames.com/v1/";
        static string MobyApiKey;

        const int secondsBetweenRequests = 2;
        const int gamesPerRequest = 100;

        static HttpClient http = new HttpClient();

        static List<Platform> platforms;
        static Dictionary<int, Platform> platformsById;
        static Dictionary<string, Platform> platformsByName;

        static int Main(string[] args)
        {
            var app = new CommandLineApplication();

            app.HelpOption();
            var optionKey = app.Option("-k|--key <MOBYAPIKEY>", "The MobyGames API key", CommandOptionType.SingleValue);

            app.OnExecute(() =>
            {
                Console.WriteLine("Specify a command");
                app.ShowHelp();
                return 1;
            });

            app.Command("platforms", listPlatformsCmd =>
            {
                listPlatformsCmd.HelpOption();
                listPlatformsCmd.Description = "List the IDs and names of the available platforms";
                listPlatformsCmd.OnExecute(async () =>
                {
                    if (SetMobyApiKey(optionKey))
                    {
                        await ListPlatforms();
                        return 1;
                    }
                    else
                    {
                        return 0;
                    }
                });                
            });

            app.Command("games", gamesCmd => {
                gamesCmd.HelpOption();
                gamesCmd.Description = "Retrieve games belonging to the specified platforms and write them to CSV files";                
                var optionPlatforms = gamesCmd.Option("-p|--platforms <PLATFORMS>", "A comma-separated list of platform IDs or platform names, or \"all\"", CommandOptionType.SingleValue).IsRequired();
                gamesCmd.OnExecute(async () =>
                {
                    if (SetMobyApiKey(optionKey))
                    {
                        await WriteGamesToCsv(optionPlatforms.Value());
                        return 1;
                    }
                    else
                    {
                        return 0;
                    }
                });
            });           

            return app.Execute(args);
        }

        static bool SetMobyApiKey(CommandOption optionKey)
        {
            if (optionKey.HasValue())
            {
                MobyApiKey = optionKey.Value();
                return true;
            }
            else if (File.Exists("mobyapikey.txt"))
            {
                var key = File.ReadAllText("mobyapikey.txt");
                MobyApiKey = key.Trim();
                return true;
            }
            else
            {
                Console.WriteLine("You must specify the MobyGames API key with the -k option, or create a file called mobyapikey.txt containing the key.");
                return false;
            }
        }

        static async Task RetrievePlatforms()
        {
            Console.WriteLine("Retrieving platforms...");
            var platformsUrl = MobyApiBase + $"platforms?api_key={MobyApiKey}";
            var platformsJson = await http.GetStringAsync(platformsUrl);
            platforms = JsonConvert.DeserializeObject<Platforms>(platformsJson).platforms;
            platformsById = platforms.ToDictionary(p => p.platform_id);
            platformsByName = platforms.ToDictionary(p => p.platform_name, StringComparer.OrdinalIgnoreCase);
            Console.WriteLine($"Retrieved {platforms.Count} platforms.");            
        }

        static async Task ListPlatforms()
        {
            await RetrievePlatforms();
            foreach (var platform in platforms)
                Console.WriteLine($"{platform.platform_id}: {platform.platform_name}");
        }

        static async Task WriteGamesToCsv(string platformIdsOrNames)
        {
            await RetrievePlatforms();

            List<Platform> platformsToRetrieve;

            if (platformIdsOrNames.ToLower() == "all")
            {
                platformsToRetrieve = platforms;
            }
            else
            {
                platformsToRetrieve = new List<Platform>();
                foreach (var idOrName in platformIdsOrNames.Split(','))
                {
                    Platform platform = null;
                    if (int.TryParse(idOrName, out var id))
                    {
                        if (platformsById.ContainsKey(id))
                            platform = platformsById[id];
                        else
                            Console.WriteLine($"Unrecognized platform ID: {id}. Ignoring.");
                    }
                    else
                    {
                        if (platformsByName.ContainsKey(idOrName))
                            platform = platformsByName[idOrName];
                        else
                            Console.WriteLine($"Unrecognized platform name: {idOrName}. Ignoring.");
                    }

                    if (platform != null)
                        platformsToRetrieve.Add(platform);
                }
            }

            var yearRegex = new Regex("(19|20)\\d{2}");

            foreach (var platform in platformsToRetrieve)
            {
                var platformGames = await RetrieveGamesForPlatform(platform);
                var gameRows = new List<GameCsvRow>(platformGames.Count);
                foreach (var game in platformGames)
                {
                    var releaseDate = game.platforms.First(p => p.platform_id == platform.platform_id).first_release_date;
                    var match = yearRegex.Match(releaseDate);
                    var year = match.Success ? match.Value : releaseDate;

                    var row = new GameCsvRow
                    {
                        MobyID = game.game_id,
                        Title = game.title,
                        MobyURL = game.moby_url,
                        OfficialURL = game.official_url,
                        FirstReleaseDate = year,
                        Genre = game.genres.Any() ? game.genres.First().genre_name : null
                    };

                    gameRows.Add(row);
                }

                var csvFileName = platform.platform_name + ".csv";
                foreach (var invalidChar in Path.GetInvalidFileNameChars())
                    csvFileName = csvFileName.Replace(invalidChar, '_');

                var createdOrOverwrote = File.Exists(csvFileName) ? "Overwrote" : "Created";
                
                using (var streamWriter = File.CreateText(csvFileName))
                {
                    var csvWriter = new CsvWriter(streamWriter);
                    csvWriter.Configuration.Delimiter = "|";
                    csvWriter.WriteRecords(gameRows);
                }

                Console.WriteLine($"{createdOrOverwrote} {csvFileName}");
            }            
        }

        static async Task<List<Game>> RetrieveGamesForPlatform(Platform platform)
        {
            Console.WriteLine($"Retrieving games for platform ID {platform.platform_id}: {platform.platform_name}");
            int offset = 0;
            var allGames = new List<Game>();

            do
            {
                Pause();
                var gamesUrl = MobyApiBase + $"games?format=normal&platform={platform.platform_id}&limit={gamesPerRequest}&offset={offset}&api_key={MobyApiKey}";
                var gamesJson = await http.GetStringAsync(gamesUrl);
                var games = JsonConvert.DeserializeObject<Games>(gamesJson).games;
                allGames.AddRange(games);

                if (games.Count > 0)
                    Console.WriteLine($"Retrieved {games.Count} games...");                

                if (games.Count < gamesPerRequest)
                    break;

                offset += gamesPerRequest;
            }
            while (true);

            Console.WriteLine($"Done. Retrieved a total of {allGames.Count} games.");
            return allGames;
        }        

        static void Pause()
        {
            Thread.Sleep(1000 * secondsBetweenRequests);
        }
    }
}
