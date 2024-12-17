using GeoNimbus.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace GeoNimbus.Controllers;

[ApiController]
[Route("geocode")]
public class GeocodeController : ControllerBase {
    private readonly ILogger<GeocodeController> _logger;
    private readonly IAddressService _addressService;

    private TimeSpan _timeout = TimeSpan.FromSeconds(5);

    public GeocodeController(ILogger<GeocodeController> logger, IAddressService addressService, IConfiguration configuration) {
        _logger = logger;
        _addressService = addressService;

        var timeout = configuration.GetValue<double>("API:Timeout");
        if (timeout > 0) {
            _timeout = TimeSpan.FromSeconds(timeout);
        }
    }

    [HttpGet("id/{id}")]
    public async Task<IActionResult> GetAddressByIdAsync(int id) {
        try {
            // Set a timeout of 5 seconds
            using var cts = new CancellationTokenSource(_timeout);

            var result = await _addressService.GetAddressByIdAsync(id, cts.Token);
            if (result == null)
                return NotFound();

            return Ok(result);
        } catch (OperationCanceledException) {
            return StatusCode(408, "The operation was canceled."); // HTTP 408: Request Timeout
        }
    }

    [HttpGet("geocode")]
    public async Task<IActionResult> Geocode([FromQuery] string zipcode, [FromQuery] string number, [FromQuery] string street, [FromQuery] string city, [FromQuery] string state, [FromQuery] string country) {
        try {
            using var cts = new CancellationTokenSource(_timeout);

            var result = await _addressService.GeocodeAsync(zipcode, number, street, city, state, country, cts.Token);
            if (result == null)
                return NotFound();

            return Ok(result);
        } catch (OperationCanceledException) {
            return StatusCode(408, "The operation was canceled."); // HTTP 408: Request Timeout
        }
    }

    [HttpGet("reverse-geocode")]
    public async Task<IActionResult> ReverseGeocodeAsync([FromQuery] double latitude, [FromQuery] double longitude) {
        try {
            using var cts = new CancellationTokenSource(_timeout);

            var result = await _addressService.ReverseGeocodeAsync(latitude, longitude, cts.Token);
            if (result == null)
                return NotFound();

            return Ok(result);
        } catch (OperationCanceledException) {
            return StatusCode(408, "The operation was canceled."); // HTTP 408: Request Timeout
        }
    }

    [HttpGet("bbox")]
    public async Task<IActionResult> QueryByBoundingBoxAsync([FromQuery] double minLat, [FromQuery] double maxLat, [FromQuery] double minLon, [FromQuery] double maxLon) {
        try {
            using var cts = new CancellationTokenSource(_timeout);
            var result = await _addressService.QueryByBoundingBoxAsync(minLat, maxLat, minLon, maxLon, cts.Token);
            return Ok(result);
        } catch (OperationCanceledException) {
            return StatusCode(408, "The operation was canceled."); // HTTP 408: Request Timeout
        }
    }

    [HttpGet("radius")]
    public async Task<IActionResult> QueryByRadiusAsync([FromQuery] double latitude, [FromQuery] double longitude, [FromQuery] double radius) {
        try {
            using var cts = new CancellationTokenSource(_timeout);
            var result = await _addressService.QueryByRadiusAsync(latitude, longitude, radius, cts.Token);
            return Ok(result);
        } catch (OperationCanceledException) {
            return StatusCode(408, "The operation was canceled."); // HTTP 408: Request Timeout
        }
    }

    [HttpGet("geohash")]
    public async Task<IActionResult> QueryByGeohashAsync([FromQuery] string geohash) {
        using var cts = new CancellationTokenSource(_timeout);

        try {
            var result = await _addressService.QueryByGeohashAsync(geohash, cts.Token);

            if (result == null)
                return NotFound("No results found for the specified geohash prefix.");

            return Ok(result);
        } catch (OperationCanceledException) {
            return StatusCode(408, "The operation timed out."); // HTTP 408: Request Timeout
        } catch (Exception ex) {
            return StatusCode(500, $"An error occurred: {ex.Message}");
        }
    }

    [HttpGet("geohash-prefix")]
    public async Task<IActionResult> QueryByGeohashPrefixAsync([FromQuery] string geohashPrefix) {
        using var cts = new CancellationTokenSource(_timeout);

        try {
            var result = await _addressService.QueryByGeohashPrefixAsync(geohashPrefix, cts.Token);

            if (result == null || result.Count == 0)
                return NotFound("No results found for the specified geohash prefix.");

            return Ok(result);
        } catch (OperationCanceledException) {
            return StatusCode(408, "The operation timed out."); // HTTP 408: Request Timeout
        } catch (Exception ex) {
            return StatusCode(500, $"An error occurred: {ex.Message}");
        }
    }
}