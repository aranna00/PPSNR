namespace PPSNR.Server.Services;

public class ImagesCacheService
{
    private readonly IWebHostEnvironment _env;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ImagesCacheService> _logger;

    private const string ResourcesFolder = "wwwroot/resources";

    public ImagesCacheService(IWebHostEnvironment env, IHttpClientFactory httpClientFactory, ILogger<ImagesCacheService> logger)
    {
        _env = env;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<string> GetCachedImageUrlAsync(string externalUrlOrId, CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.Combine(_env.ContentRootPath, ResourcesFolder));

        // If it's a Pokemon name/id, fetch from PokéAPI
        if (!externalUrlOrId.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            var imageUrl = await PokemonImageFetcher.GetPokemonImageUrlAsync(externalUrlOrId, _httpClientFactory, _logger, ct);
            return await DownloadAndCacheAsync(imageUrl, ct);
        }
        return await DownloadAndCacheAsync(externalUrlOrId, ct);
    }

    private async Task<string> DownloadAndCacheAsync(string url, CancellationToken ct)
    {
        var fileName = MakeSafeFileName(url);
        var absPath = Path.Combine(_env.ContentRootPath, ResourcesFolder, fileName);
        var relPath = $"/resources/{fileName}";

        if (File.Exists(absPath))
        {
            return relPath;
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(20);
            using var resp = await client.GetAsync(url, ct);
            resp.EnsureSuccessStatusCode();
            await using var fs = File.Create(absPath);
            await resp.Content.CopyToAsync(fs, ct);
            return relPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cache image from {Url}", url);
            throw;
        }
    }

    private static string MakeSafeFileName(string input)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            input = input.Replace(c, '_');
        // keep extension if present, else default .png
        if (!Path.HasExtension(input)) input += ".png";
        return input.Length > 150 ? input[..150] : input;
    }
}

public static class PokemonImageFetcher
{
    public static async Task<string> GetPokemonImageUrlAsync(string idOrName, IHttpClientFactory factory, ILogger logger, CancellationToken ct)
    {
        var client = factory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(20);
        var url = $"https://pokeapi.co/api/v2/pokemon/{idOrName.ToLowerInvariant()}";
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                using var resp = await client.GetAsync(url, ct);
                if ((int)resp.StatusCode == 429)
                {
                    var delay = TimeSpan.FromSeconds(1 + attempt * 2);
                    await Task.Delay(delay, ct);
                    continue;
                }
                resp.EnsureSuccessStatusCode();
                var json = await resp.Content.ReadAsStringAsync(ct);
                // naive parse to find official artwork url
                var marker = "official-artwork\":{\"front_default\":\"";
                var idx = json.IndexOf(marker, StringComparison.Ordinal);
                if (idx >= 0)
                {
                    var start = idx + marker.Length;
                    var end = json.IndexOf('"', start);
                    if (end > start)
                    {
                        return json[start..end];
                    }
                }
                // fallback to front_default sprite
                marker = "front_default\":\"";
                idx = json.IndexOf(marker, StringComparison.Ordinal);
                if (idx >= 0)
                {
                    var start2 = idx + marker.Length;
                    var end2 = json.IndexOf('"', start2);
                    if (end2 > start2) return json[start2..end2];
                }
                throw new InvalidOperationException("Could not parse Pokemon image url");
            }
            catch (Exception ex) when (attempt < 2)
            {
                logger.LogWarning(ex, "Retrying PokéAPI fetch for {Pokemon}", idOrName);
                await Task.Delay(TimeSpan.FromMilliseconds(500 * (attempt + 1)), ct);
            }
        }
        throw new InvalidOperationException("Failed to fetch Pokemon image after retries");
    }
}
