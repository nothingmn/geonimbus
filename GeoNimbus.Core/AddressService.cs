using GeoNimbus.Contracts;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using GeoNimbus.Contracts;

using Microsoft.Extensions.Configuration;

public class AddressService : IAddressService {
    private readonly IAddressRepository _repository;
    private readonly ICache<Address> _cache;
    private readonly IConfiguration _configuration;
    private readonly IGeoHash _geoHash;

    public AddressService(IAddressRepository repository, ICache<Address> cache, IConfiguration configuration, IGeoHash geoHash) {
        _repository = repository;
        _cache = cache;
        _configuration = configuration;
        _geoHash = geoHash;
        var connectionString = _configuration.GetConnectionString("AddressDatabase");
        this.Init(connectionString).GetAwaiter().GetResult();
    }

    public async Task Init(string connectionString) {
        await _repository.Init(connectionString);
    }

    public async Task<Address> GetAddressByIdAsync(int id, CancellationToken cancellationToken) {
        // Check cache by ID
        if (_cache.TryGet(id.ToString(), out var cachedAddress)) {
            return cachedAddress;
        }

        // Fetch from repository
        var address = await _repository.GetAddressByIdAsync(id, cancellationToken);
        if (address != null) {
            _cache.Add(address.Latitude, address.Longitude, id.ToString(), address);
        }

        return address;
    }

    public async Task<Address> GeocodeAsync(string zipcode, string number, string street, string city, string state, string country, CancellationToken cancellationToken) {
        var cacheKey = $"{zipcode}:{number}:{street}:{city}:{state}:{country}";
        if (_cache.TryGet(cacheKey, out var cachedAddress)) {
            return cachedAddress;
        }

        var address = await _repository.GeocodeAsync(zipcode, number, street, city, state, country, cancellationToken);
        if (address != null) {
            _cache.Add(address.Latitude, address.Longitude, cacheKey, address);
        }

        return address;
    }

    public async Task<Address> ReverseGeocodeAsync(double latitude, double longitude, CancellationToken cancellationToken) {
        // Look for a match in the cache by lat/lon
        var cachedAddresses = _cache.Query(latitude - 0.0001, latitude + 0.0001, longitude - 0.0001, longitude + 0.0001);
        if (cachedAddresses?.Count > 0) {
            return cachedAddresses.First(); // Return the first match from the cache
        }

        var address = await _repository.ReverseGeocodeAsync(latitude, longitude, cancellationToken);
        if (address != null) {
            _cache.Add(address.Latitude, address.Longitude, address.Id.ToString(), address);
        }

        return address;
    }

    public async Task<List<Address>> QueryByBoundingBoxAsync(double minLat, double maxLat, double minLon, double maxLon, CancellationToken cancellationToken) {
        // Query the cache for matching addresses
        var cachedAddresses = _cache.Query(minLat, maxLat, minLon, maxLon);
        if (cachedAddresses?.Count > 0) {
            return cachedAddresses;
        }

        // Query the database if no results found in the cache
        var addresses = await _repository.QueryByBoundingBoxAsync(minLat, maxLat, minLon, maxLon, cancellationToken);
        //foreach (var address in addresses) {
        //    _cache.Add(address.Latitude, address.Longitude, address.Id.ToString(), address);
        //}

        return addresses;
    }

    public async Task<List<Address>> QueryByRadiusAsync(double latitude, double longitude, double radiusKm, CancellationToken cancellationToken) {
        // Convert radius to bounding box
        var latDiff = radiusKm / 111.0; // Approx. 111 km per degree of latitude
        var lonDiff = radiusKm / (111.0 * System.Math.Cos(latitude * System.Math.PI / 180));
        var minLat = latitude - latDiff;
        var maxLat = latitude + latDiff;
        var minLon = longitude - lonDiff;
        var maxLon = longitude + lonDiff;

        // Query by bounding box
        return await QueryByBoundingBoxAsync(minLat, maxLat, minLon, maxLon, cancellationToken);
    }

    public async Task<List<Address>> BatchReverseGeocodeAsync(List<(double Latitude, double Longitude)> coordinates, CancellationToken cancellationToken) {
        var results = new List<Address>();

        foreach (var (latitude, longitude) in coordinates) {
            var address = await ReverseGeocodeAsync(latitude, longitude, cancellationToken);
            if (address != null) {
                results.Add(address);
            }
        }

        return results;
    }

    public async Task<List<Address>> QueryByGeohashPrefixAsync(string geohashPrefix, CancellationToken cancellationToken) {
        // Query database for geohash prefix
        var addresses = await _repository.QueryByGeohashPrefixAsync(geohashPrefix, cancellationToken);

        // Add results to cache
        foreach (var address in addresses) {
            _cache.Add(address.Latitude, address.Longitude, address.Id.ToString(), address);
        }

        return addresses;
    }

    public async Task<Address> QueryByGeohashAsync(string geohash, CancellationToken cancellationToken) {
        // Query database for geohash prefix

        var location = _geoHash.Decode(geohash);
        var cachedAddresses = _cache.Query(location.Latitude - 0.0001, location.Latitude + 0.0001, location.Longitude - 0.0001, location.Longitude + 0.0001);
        if (cachedAddresses?.Count > 0) {
            return cachedAddresses.First(); // Return the first match from the cache
        }

        var address = await _repository.QueryByGeohashAsync(geohash, cancellationToken);

        if (address is not null) {
            // Add results to cache
            _cache.Add(address.Latitude, address.Longitude, address.Id.ToString(), address);
        }

        return address;
    }
}