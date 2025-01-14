using GeoNimbus.Contracts;
using Microsoft.AspNetCore.Mvc;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Diagnostics.Metrics;
using System.IO;
using System.Reflection.Emit;

namespace GeoNimbus.Controllers;

[ApiController]
[Route("api/")]
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

            _logger.LogInformation("OK GetAddressByIdAsync for {id}", id);
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

            _logger.LogInformation("OK Geocode for {zipcode} {number} {street} {city} {state} {country}", zipcode, number, street, city, state, country);
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
            _logger.LogInformation("OK ReverseGeocodeAsync for {latitude} {longitude}", latitude, longitude);
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
            _logger.LogInformation("OK QueryByBoundingBoxAsync for {minLat} {maxLat} {minLon} {maxLon}", minLat, maxLat, minLon, maxLon);
            return Ok(result);
        } catch (OperationCanceledException) {
            return StatusCode(408, "The operation was canceled."); // HTTP 408: Request Timeout
        }
    }

    [HttpGet("radius")]
    public async Task<IActionResult> QueryByRadiusAsync([FromQuery] double latitude, [FromQuery] double longitude, [FromQuery] double radiusKm) {
        try {
            using var cts = new CancellationTokenSource(_timeout);
            var result = await _addressService.QueryByRadiusAsync(latitude, longitude, radiusKm, cts.Token);
            _logger.LogInformation("OK QueryByRadiusAsync for {latitude} {longitude} {radiusKm}", latitude, longitude, radiusKm);
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

            _logger.LogInformation("OK QueryByGeohashAsync for {geohash}", geohash);
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

            _logger.LogInformation("OK QueryByGeohashPrefixAsync for {geohashPrefix}", geohashPrefix);
            return Ok(result);
        } catch (OperationCanceledException) {
            return StatusCode(408, "The operation timed out."); // HTTP 408: Request Timeout
        } catch (Exception ex) {
            return StatusCode(500, $"An error occurred: {ex.Message}");
        }
    }
}