using GeoNimbus.Contracts;
using Npgsql;
using Npgsql.NetTopologySuite;
using System.Data.Common;
using NetTopologySuite.Geometries;

namespace GeoNimbus.Postgres;

/// <summary>
/// NOTE:
/// Root Cause of cancellation tokens not triggering based on the timeout:
/// SQLite's ADO.NET provider (System.Data.SQLite) has limited support for CancellationToken.
/// While async methods like ExecuteReaderAsync accept a CancellationToken, they do not properly handle or respect it in all scenarios, particularly for long-running database operations.
/// </summary>
public class PostgresAddressRepository : IAddressRepository {
    private readonly IGeoHash _geoHash;
    private string _connectionString;

    public PostgresAddressRepository(IGeoHash geoHash) {
        _geoHash = geoHash;
    }

    public Task Init(string connectionString) {
        _connectionString = connectionString;
        return Task.CompletedTask;
    }

    public async Task<Address> GetAddressByIdAsync(int id, CancellationToken cancellationToken) {
        const string query = "SELECT * FROM public.addresses WHERE id = @_id LIMIT 1;";

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(_connectionString);
        dataSourceBuilder.UseNetTopologySuite(); // Enables support for spatial types
        var dataSource = dataSourceBuilder.Build();

        await using var conn = await dataSource.OpenConnectionAsync(cancellationToken: cancellationToken);
        await using var cmd = new NpgsqlCommand();
        cmd.Connection = conn;
        cmd.CommandText = query;
        cmd.Parameters.Add("_id", NpgsqlTypes.NpgsqlDbType.Integer).Value = id;

        await using (var reader = await cmd.ExecuteReaderAsync(cancellationToken: cancellationToken))
            if (await reader.ReadAsync(cancellationToken)) {
                return MapToAddress(reader);
            } else {
                return null;
            }
    }

    public async Task<Address> GeocodeAsync(string zipcode, string number, string street, string city, string state, string country, CancellationToken cancellationToken) {
        const string query = @"
            SELECT * FROM addresses
            WHERE zipcode = @zipcode AND number = @number AND street = @street
              AND city = @city AND state = @state AND country = @country
            LIMIT 1";

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(_connectionString);
        dataSourceBuilder.UseNetTopologySuite(); // Enables support for spatial types
        var dataSource = dataSourceBuilder.Build();

        await using var conn = await dataSource.OpenConnectionAsync(cancellationToken: cancellationToken);
        await using var cmd = new NpgsqlCommand();
        cmd.Connection = conn;
        cmd.CommandText = query;

        cmd.Parameters.Add("zipcode", NpgsqlTypes.NpgsqlDbType.Text).Value = zipcode;
        cmd.Parameters.Add("number", NpgsqlTypes.NpgsqlDbType.Text).Value = number;
        cmd.Parameters.Add("street", NpgsqlTypes.NpgsqlDbType.Text).Value = street;
        cmd.Parameters.Add("city", NpgsqlTypes.NpgsqlDbType.Text).Value = city;
        cmd.Parameters.Add("state", NpgsqlTypes.NpgsqlDbType.Text).Value = state;
        cmd.Parameters.Add("country", NpgsqlTypes.NpgsqlDbType.Text).Value = country;

        await using (var reader = await cmd.ExecuteReaderAsync(cancellationToken: cancellationToken))
            if (await reader.ReadAsync(cancellationToken)) {
                return MapToAddress(reader);
            } else {
                return null;
            }
    }

    public async Task<Address> ReverseGeocodeAsync(double latitude, double longitude, CancellationToken cancellationToken) {
        const string query = @"
        SELECT *
        FROM public.addresses
        WHERE ST_DWithin(location, ST_MakePoint(@longitude, @latitude)::geography, 50)
        ORDER BY ST_Distance(location, ST_MakePoint(@longitude, @latitude)::geography)
        LIMIT 1;";

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(_connectionString);
        dataSourceBuilder.UseNetTopologySuite(); // Enable spatial support
        var dataSource = dataSourceBuilder.Build();

        await using (var conn = await dataSource.OpenConnectionAsync(cancellationToken))
        await using (var cmd = new NpgsqlCommand(query, conn)) {
            cmd.Parameters.AddWithValue("latitude", latitude);
            cmd.Parameters.AddWithValue("longitude", longitude);

            await using (var reader = await cmd.ExecuteReaderAsync(cancellationToken)) {
                if (await reader.ReadAsync(cancellationToken)) {
                    return MapToAddress(reader); // Reuse the MapToAddress method
                } else {
                    return null; // No address found within the specified distance
                }
            }
        }
    }

    public async Task<List<Address>> QueryByBoundingBoxAsync(double minLat, double maxLat, double minLon, double maxLon, CancellationToken cancellationToken) {
        const string query = @"
        SELECT *
        FROM public.addresses
        WHERE location && ST_MakeEnvelope(@minLon, @minLat, @maxLon, @maxLat, 4326);";

        var results = new List<Address>();

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(_connectionString);
        dataSourceBuilder.UseNetTopologySuite(); // Enable spatial support
        var dataSource = dataSourceBuilder.Build();

        await using (var conn = await dataSource.OpenConnectionAsync(cancellationToken))
        await using (var cmd = new NpgsqlCommand(query, conn)) {
            // Add parameters for the bounding box
            cmd.Parameters.AddWithValue("minLat", minLat);
            cmd.Parameters.AddWithValue("maxLat", maxLat);
            cmd.Parameters.AddWithValue("minLon", minLon);
            cmd.Parameters.AddWithValue("maxLon", maxLon);

            await using (var reader = await cmd.ExecuteReaderAsync(cancellationToken)) {
                while (await reader.ReadAsync(cancellationToken)) {
                    if (cancellationToken.IsCancellationRequested) break;
                    results.Add(MapToAddress(reader)); // Reuse the MapToAddress method
                }
            }
        }

        return results;
    }

    public async Task<List<Address>> QueryByRadiusAsync(double latitude, double longitude, double radius, CancellationToken cancellationToken) {
        var latDiff = radius / 111.0;
        var lonDiff = radius / (111.0 * Math.Cos(latitude * Math.PI / 180));
        return await QueryByBoundingBoxAsync(latitude - latDiff, latitude + latDiff, longitude - lonDiff, longitude + lonDiff, cancellationToken);
    }

    public async Task<List<Address>> BatchReverseGeocodeAsync(List<(double Latitude, double Longitude)> coordinates, CancellationToken cancellationToken) {
        var results = new List<Address>();

        foreach (var coord in coordinates) {
            if (cancellationToken.IsCancellationRequested) break;
            var address = await ReverseGeocodeAsync(coord.Latitude, coord.Longitude, cancellationToken);
            if (address != null) {
                results.Add(address);
            }
        }

        return results;
    }

    public async Task<List<Address>> QueryByGeohashPrefixAsync(string geohashPrefix, CancellationToken cancellationToken) {
        const string query = @"
        SELECT id, zipcode, number, street, street2, city, state, plus4, country,
               source, geohash, location::geometry AS location
        FROM public.addresses
        WHERE geohash LIKE @geohashPrefix || '%';";

        var results = new List<Address>();

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(_connectionString);
        dataSourceBuilder.UseNetTopologySuite(); // Enable spatial support
        var dataSource = dataSourceBuilder.Build();

        await using (var conn = await dataSource.OpenConnectionAsync(cancellationToken))
        await using (var cmd = new NpgsqlCommand(query, conn)) {
            // Add the geohash prefix parameter
            cmd.Parameters.AddWithValue("geohashPrefix", geohashPrefix);

            await using (var reader = await cmd.ExecuteReaderAsync(cancellationToken)) {
                while (await reader.ReadAsync(cancellationToken)) {
                    if (cancellationToken.IsCancellationRequested) break;
                    results.Add(MapToAddress(reader)); // Reuse the same MapToAddress method
                }
            }
        }

        return results;
    }

    public async Task<Address> QueryByGeohashAsync(string geohash, CancellationToken cancellationToken) {
        const string query = @"
        SELECT id, zipcode, number, street, street2, city, state, plus4, country,
               source, geohash, location::geometry AS location
        FROM public.addresses
        WHERE geohash = @geohash;";

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(_connectionString);
        dataSourceBuilder.UseNetTopologySuite(); // Enable spatial support
        var dataSource = dataSourceBuilder.Build();

        await using (var conn = await dataSource.OpenConnectionAsync(cancellationToken))
        await using (var cmd = new NpgsqlCommand(query, conn)) {
            // Add the geohash parameter
            cmd.Parameters.AddWithValue("geohash", geohash);

            await using (var reader = await cmd.ExecuteReaderAsync(cancellationToken)) {
                if (await reader.ReadAsync(cancellationToken)) {
                    return MapToAddress(reader); // Reuse the existing MapToAddress method
                }
            }
        }

        // Return null if no address is found
        return null;
    }

    private Address MapToAddress(DbDataReader reader) {
        if (!reader.HasRows)
            return null;

        Point location = reader.IsDBNull(reader.GetOrdinal("location"))
            ? null
            : reader.GetFieldValue<Point>(reader.GetOrdinal("location"));

        return new Address {
            Id = Convert.ToInt32(reader["id"]),
            Zipcode = reader["zipcode"].ToString(),
            Number = reader["number"].ToString(),
            Street = reader["street"].ToString(),
            Street2 = reader["street2"] == DBNull.Value ? null : reader["street2"].ToString(),
            City = reader["city"].ToString(),
            State = reader["state"].ToString(),
            Plus4 = reader["plus4"] == DBNull.Value ? null : reader["plus4"].ToString(),
            Country = reader["country"].ToString(),
            Latitude = location.Y, // Y corresponds to latitude
            Longitude = location.X, // X corresponds to longitude
            Source = reader["source"] == DBNull.Value ? null : reader["source"].ToString(),
            Geohash = reader["geohash"] == DBNull.Value ? null : reader["geohash"].ToString(),
        };
    }
}