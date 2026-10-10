using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

// Fetch a planet's composite parameters from the NASA Exoplanet Archive.
// Usage: dotnet run -- "Proxima Centauri b"
// Requires .NET 8 or later.

if (args.Length == 0 || string.IsNullOrWhiteSpace(args[0]))
{
    Console.WriteLine("Usage: dotnet run -- \"Planet Name\"");
    Console.WriteLine("Example: dotnet run -- \"Proxima Centauri b\"");
    return;
}

string requestedName = string.Join(' ', args).Trim();
using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
http.DefaultRequestHeaders.UserAgent.ParseAdd("InterstellarTimekeepingApp/1.1");

try
{
    // First try the exact name. If that fails, search using the first host-name
    // token and the planet letter (e.g. "Proxima Centauri b" -> "%proxima% b").
    List<PlanetRecord> planets = await QueryPlanetsAsync(requestedName, exact: true);

    if (planets.Count == 0)
    {
        string[] parts = requestedName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
        {
            string planetSuffix = parts[^1];
            string hostFirstWord = parts[0];
            planets = await QueryPlanetsAsync($"{hostFirstWord} {planetSuffix}", exact: false);
        }
    }

    if (planets.Count == 0)
    {
        Console.WriteLine($"No planet found for '{requestedName}'.");
        Console.WriteLine("Try the archive's canonical name, or search the NASA Exoplanet Archive:");
        Console.WriteLine("https://exoplanetarchive.ipac.caltech.edu/");
        return;
    }

    // If fallback search returned several possible planets, show choices rather
    // than silently returning the wrong one.
    if (planets.Count > 1)
    {
        Console.WriteLine($"More than one possible match for '{requestedName}':");
        foreach (PlanetRecord candidate in planets)
            Console.WriteLine($" - {candidate.Name}");
        Console.WriteLine("Run the program again with the exact name shown above.");
        return;
    }

    PlanetRecord planet = planets[0];
    Console.WriteLine($"Requested name: {requestedName}");
    Console.WriteLine($"Archive name: {Value(planet.Name)}");
    Console.WriteLine($"Host star: {Value(planet.HostName)}");
    Console.WriteLine($"Discovery method: {Value(planet.DiscoveryMethod)}");
    Console.WriteLine($"Discovery year: {Value(planet.DiscoveryYear)}");
    Console.WriteLine($"Orbital period: {Format(planet.OrbitalPeriodDays, "days")}");
    Console.WriteLine($"Semi-major axis: {Format(planet.SemiMajorAxisAu, "AU")}");
    Console.WriteLine($"Orbital eccentricity: {Value(planet.OrbitalEccentricity)}");
    Console.WriteLine($"Planet radius: {Format(planet.RadiusEarth, "Earth radii")}");
    Console.WriteLine($"Planet mass: {Format(planet.MassEarth, "Earth masses")}");
    Console.WriteLine($"Host distance: {Format(planet.SystemDistancePc, "parsecs")}");
    Console.WriteLine();
    Console.WriteLine("These are catalogued parameters, not a calculated 3D position for a requested date.");

    // Epoch shit
    DateTimeOffset now = (DateTimeOffset)DateTime.UtcNow;
    int secondsToDays = 86400;
    double daysToYears = 365.25;

    double year0Epoch = now.ToUnixTimeSeconds() + (secondsToDays * daysToYears * 1970);
    double planetEpoch = year0Epoch / ((planet.OrbitalPeriodDays ?? 0 * daysToYears) / daysToYears);

    int planetYear = (int)Math.Floor(planetEpoch / secondsToDays / daysToYears);
    int planetMonth = (int)Math.Floor(((planetEpoch / secondsToDays / daysToYears) * 12) % 12) + 1;
    int planetDay = (int)Math.Floor(((planetEpoch / secondsToDays) * 31) % 31) + 1;
    int planetHour = (int)Math.Floor(((planetEpoch / 60 / 60) * 24) % 24);
    int planetMinute = (int)Math.Floor((planetEpoch / 60) % 60);
    int planetSecond = (int)Math.Floor(planetEpoch % 60);

    string earthDate = $"{now.Year:D4}-{now.Month:D2}-{now.Day:D2} {now.Hour:D2}:{now.Minute:D2}";
    string planetDate = $"{planetYear}-{planetMonth:D2}-{planetDay:D2} {planetHour:D2}:{planetMinute:D2}";

    Console.WriteLine($"Earth Time/Date: {earthDate}");
    Console.WriteLine($"{requestedName} Time/Date: {planetDate}");

    Console.WriteLine($"#############################################################################################");
    Console.WriteLine($"#               _____                #                                                      #");
    Console.WriteLine($"#            .-'.  ':'-.             #                                                      #");
    Console.WriteLine($"#          .''::: .:    '.           #                                                      #");
    Console.WriteLine($"#         /   :::::'      \\          #       Earth                                          #");
    Console.WriteLine($"#        ;.    ':' `       ;         #       Day: {now.Day:D2}                                        #");
    Console.WriteLine($"#        |       '..       |         #       Month: {now.Month:D2}                                      #");
    Console.WriteLine($"#        ; '      ::::.    ;         #       Year: {now.Year:D4}                                     #");
    Console.WriteLine($"#         \\       '::::   /          #       Time: {now.Hour}:{now.Minute}                                    #");
    Console.WriteLine($"#          '.      :::  .'           #                                                      #");
    Console.WriteLine($"#            '-.___'_.-'             #                                                      #");
    Console.WriteLine($"#                                    #                                                      #");
    Console.WriteLine($"#############################################################################################");
    Console.WriteLine($"#               _____                #                                                      #");
    Console.WriteLine($"#            .-'     '-.             #                                                      #");
    Console.WriteLine($"#          .'           '.           #                                                      #");
    Console.WriteLine($"#         /               \\          #       {requestedName, -30}                 #");
    Console.WriteLine($"#        ;                 ;         #       Day: {planetDay:D2}                                        #");
    Console.WriteLine($"#        |        ?        |         #       Month: {planetMonth:D2}                                      #");
    Console.WriteLine($"#        ;                 ;         #       Year: {planetYear, -30}           #");
    Console.WriteLine($"#         \\               /          #       Time: {planetHour}:{planetMinute}                                     #");
    Console.WriteLine($"#          '.           .'           #                                                      #");
    Console.WriteLine($"#            '-._____.-'             #                                                      #");
    Console.WriteLine($"#                                    #                                                      #");
    Console.WriteLine($"#############################################################################################");

    Console.WriteLine($"Interstellar Timekeeping™ is sponsored by US Military, Nestle and Raid Shadow Legends \n");

    System.Threading.Thread.Sleep(120000);

    Console.Write(@"          ^␍
         | |␍
       @#####@␍
     (###   ###)-.␍
   .(###     ###) \␍
  /  (###   ###)   )␍
 (=-  .@#####@|_--''␍
 /\    \_|l|_/ (\␍
(=-\     |l|    /␍
 \  \.___|l|___/␍
 /\      |_|   /␍
(=-\._________/\␍
 \             /␍
   \._________/␍
     #  ----  #␍
     #   __   #␍
     \########/␍
    ");

    Console.WriteLine("Enter Your Credit Card Details Below (Or we will kill you with the above gun.): ");
    Console.ReadLine();

    Console.WriteLine("Thank you for giving us your entire life savings: Draining bank account now.");

}
catch (TaskCanceledException)
{
    Console.Error.WriteLine("The request timed out. Check your connection and try again.");
}
catch (HttpRequestException ex)
{
    Console.Error.WriteLine($"Network error contacting NASA Exoplanet Archive: {ex.Message}");
}
catch (JsonException ex)
{
    Console.Error.WriteLine($"Could not parse the archive's JSON response: {ex.Message}");
}

async Task<List<PlanetRecord>> QueryPlanetsAsync(string name, bool exact)
{
    string escapedName = name.Replace("'", "''");
    string condition;
    if (exact)
    {
        condition = $"lower(pl_name) = lower('{escapedName}')";
    }
    else
    {
        string[] parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string hostToken = parts[0].Replace("'", "''");
        string planetSuffix = parts[^1].Replace("'", "''");
        condition = $"lower(pl_name) LIKE lower('%{hostToken}%') AND lower(pl_name) LIKE lower('% {planetSuffix}')";
    }

    string query = "SELECT pl_name, hostname, discoverymethod, disc_year, pl_orbper, pl_orbsmax, pl_orbeccen, pl_rade, pl_masse, sy_dist " +
                   $"FROM pscomppars WHERE {condition} ORDER BY pl_name";
    string url = "https://exoplanetarchive.ipac.caltech.edu/TAP/sync?query=" +
                 Uri.EscapeDataString(query) + "&format=json";

    using HttpResponseMessage response = await http.GetAsync(url);
    string json = await response.Content.ReadAsStringAsync();
    if (!response.IsSuccessStatusCode)
        throw new HttpRequestException($"Archive returned {(int)response.StatusCode} {response.ReasonPhrase}: {json}");

    return JsonSerializer.Deserialize<List<PlanetRecord>>(
        json,
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<PlanetRecord>();
}

static string Value<T>(T? value) => value is null ? "Not available" : value.ToString() ?? "Not available";
static string Format(double? value, string unit) => value is null ? "Not available" : $"{value.Value:G8} {unit}";

public sealed class PlanetRecord
{
    [JsonPropertyName("pl_name")] public string? Name { get; set; }
    [JsonPropertyName("hostname")] public string? HostName { get; set; }
    [JsonPropertyName("discoverymethod")] public string? DiscoveryMethod { get; set; }
    [JsonPropertyName("disc_year")] public int? DiscoveryYear { get; set; }
    [JsonPropertyName("pl_orbper")] public double? OrbitalPeriodDays { get; set; }
    [JsonPropertyName("pl_orbsmax")] public double? SemiMajorAxisAu { get; set; }
    [JsonPropertyName("pl_orbeccen")] public double? OrbitalEccentricity { get; set; }
    [JsonPropertyName("pl_rade")] public double? RadiusEarth { get; set; }
    [JsonPropertyName("pl_masse")] public double? MassEarth { get; set; }
    [JsonPropertyName("sy_dist")] public double? SystemDistancePc { get; set; }
}
