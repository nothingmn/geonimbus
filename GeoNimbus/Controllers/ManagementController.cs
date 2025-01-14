using GeoNimbus.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace GeoNimbus.Controllers;

[ApiController]
[Route("api/")]
public class ManagementController : ControllerBase {
    private readonly ILogger<ManagementController> _logger;
    private readonly ICache<Address> _addressCache;
    private readonly IGeoHash _geoHash;
    private readonly IAddressService _addressService;

    public ManagementController(ILogger<ManagementController> logger, ICache<Address> addressCache, IGeoHash geoHash, IAddressService addressService) {
        _logger = logger;
        _addressCache = addressCache;
        _geoHash = geoHash;
        _addressService = addressService;
    }

    /// <summary>
    /// Returns the health status of the system.
    /// </summary>
    /// <returns>The health status of various components.</returns>
    [HttpGet("health")]
    public async Task<IActionResult> GetHealth(CancellationToken cancellationToken) {
        _logger.LogInformation("GetHealth called, we are healthy.");
        return Ok(true);
    }

    /// <summary>
    /// Preloads specific regions into the hot cache.
    /// </summary>
    /// <param name="geohashes">A list of geohashes to preload.</param>
    /// <returns>A confirmation message.</returns>
    [HttpPost("cache/preload")]
    public async Task<IActionResult> PreloadCache([FromBody] List<string> geohashes, CancellationToken cancellationToken) {
        foreach (var geohash in geohashes) {
            await _addressService.QueryByGeohashAsync(geohash, cancellationToken);
        }
        return Ok(true);
    }
}