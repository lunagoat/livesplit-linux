using LiveSplit.Options;
using LiveSplit.Hosting;
using LiveSplit.Web.Share;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace LiveSplit.Web;

public class CompositeGameList
{
    protected static CompositeGameList _Instance = new();

    public static CompositeGameList Instance => _Instance;

    protected IList<string> gameNames;

    protected IDictionary<string, string> gameIDs;

    private static string CacheFilename => Path.Combine(Paths.CacheDirectory, "speedruncom_game_names.json");
    private bool loadedFromAPI = false;

    protected CompositeGameList()
    { }

    public string GetGameID(string gameName)
    {
        if (gameIDs == null && !loadedFromAPI)
        {
            GetGames(true);
        }

        return gameIDs[gameName];
    }

    public IEnumerable<string> GetGameNames(bool loadFromCache)
    {
        if (!loadedFromAPI && (!loadFromCache || gameNames == null))
        {
            GetGames(loadFromCache);
        }

        return gameNames;
    }

    private void GetGames(bool loadFromCache)
    {
        if (loadFromCache && File.Exists(CacheFilename))
        {
            LoadFromCache();
            return;
        }

        var gameNames = new List<string>();
        var gameIDs = new Dictionary<string, string>();

        var headerSet = new HashSet<string>();
        try
        {
            foreach (SpeedrunComSharp.GameHeader header in SpeedrunCom.Client.Games.GetGameHeaders())
            {
                string name = header.Name, id = header.ID;

                if (!string.IsNullOrEmpty(name) && headerSet.Add(name))
                {
                    gameNames.Add(name);
                    gameIDs[name] = id;
                }
            }

            gameNames.Sort();

            this.gameNames = gameNames;
            this.gameIDs = gameIDs;

            SaveToCache();
        }
        catch (Exception ex)
        {
            Log.Error(ex);
        }
        finally
        {
            loadedFromAPI = true;
        }
    }

    private void SaveToCache()
    {
        string jsonString = JsonSerializer.Serialize(gameIDs);
        Directory.CreateDirectory(Paths.CacheDirectory);
        File.WriteAllText(CacheFilename, jsonString);
    }

    private void LoadFromCache()
    {
        string jsonString = File.ReadAllText(CacheFilename);
        gameIDs = JsonSerializer.Deserialize<Dictionary<string, string>>(jsonString);
        gameNames = gameIDs.Keys.ToArray();
    }
}
