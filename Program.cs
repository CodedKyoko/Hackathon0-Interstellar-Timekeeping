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
    double daysSinceEpoch = now.ToUnixTimeSeconds() / 86400;
    double planetYear = (daysSinceEpoch + (1970 * 365.25)) / (planet.OrbitalPeriodDays ?? 0);

    Console.WriteLine($"Earth year: {now.Year}");
    Console.WriteLine($"Yeah of Requested planet: {Math.Floor(planetYear)}");
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
