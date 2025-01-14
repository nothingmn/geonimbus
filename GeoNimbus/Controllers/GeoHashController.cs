using GeoNimbus.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace GeoNimbus.Controllers;

[ApiController]
[Route("api/")]
public class GeoHashController : ControllerBase {
    private readonly ILogger<GeoHashController> _logger;
    private readonly IGeoHash _geoHash;

    public GeoHashController(ILogger<GeoHashController> logger, IGeoHash geoHash) {
        _logger = logger;
        _geoHash = geoHash;
    }

    /// <summary>
    /// Encodes a latitude and longitude into a geohash.
    /// </summary>
    /// <param name="latitude">The latitude coordinate.</param>
    /// <param name="longitude">The longitude coordinate.</param>
    /// <param name="precision">The precision level of the geohash (default is 6).</param>
    /// <returns>The geohash of the coordinates.</returns>
    [HttpGet("encode")]
    public async Task<IActionResult> EncodeGeohash([FromQuery] double latitude, [FromQuery] double longitude, [FromQuery] int precision = 8) {
        return Ok(_geoHash.Encode(latitude, longitude, precision));
    }

    /// <summary>
    /// Decodes a geohash into a bounding box.
    /// </summary>
    /// <param name="geohash">The geohash to decode.</param>
    /// <returns>The bounding box of the geohash.</returns>
    [HttpGet("decode")]
    public async Task<IActionResult> DecodeGeohash([FromQuery] string geohash) {
        return Ok(_geoHash.Decode(geohash));
    }
}