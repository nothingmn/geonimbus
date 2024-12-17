using GeoNimbus.Contracts;
using GeoNimbus.GeoHash;
using GeoNimbus.LibNetTopologySuite;
using GeoNimbus.Sqlite;

namespace GeoNimbus;

public class DependencyManagement {

    public void RegisterDependancies(WebApplicationBuilder builder) {
        builder.Services.AddTransient<IAddressRepository, SqliteAddressRepository>();
        builder.Services.AddSingleton<ICache<Address>, RTreeCache<Address>>();
        builder.Services.AddTransient<IAddressService, AddressService>();
        builder.Services.AddTransient<IGeoHash, GeoHasher>();
    }

    public Task FinalizeDependancies(WebApplication app) {
        return Task.CompletedTask;
    }
}