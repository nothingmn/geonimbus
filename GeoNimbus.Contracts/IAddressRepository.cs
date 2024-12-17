using System.Collections.Generic;
using System.Threading.Tasks;

namespace GeoNimbus.Contracts;

public interface IAddressRepository {

    Task Init(string connectionString);

    Task<Address> GetAddressByIdAsync(int id, CancellationToken cancellationToken);

    Task<Address> GeocodeAsync(string zipcode, string number, string street, string city, string state, string country, CancellationToken cancellationToken);

    Task<Address> ReverseGeocodeAsync(double latitude, double longitude, CancellationToken cancellationToken);

    Task<List<Address>> QueryByBoundingBoxAsync(double minLat, double maxLat, double minLon, double maxLon, CancellationToken cancellationToken);

    Task<List<Address>> QueryByRadiusAsync(double latitude, double longitude, double radius, CancellationToken cancellationToken);

    Task<List<Address>> BatchReverseGeocodeAsync(List<(double Latitude, double Longitude)> coordinates, CancellationToken cancellationToken);

    Task<List<Address>> QueryByGeohashPrefixAsync(string geohashPrefix, CancellationToken cancellationToken);

    Task<Address> QueryByGeohashAsync(string geohashPrefix, CancellationToken cancellationToken);
}