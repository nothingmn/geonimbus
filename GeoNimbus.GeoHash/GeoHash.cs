using Geohash;
using GeoNimbus.Contracts;

namespace GeoNimbus.GeoHash;

public class GeoHasher : IGeoHash {
    private Geohasher geohasher = new Geohasher();

    public string Encode(double lat, double lon, int precision = 8) {
        return geohasher.Encode(lat, lon, precision);
    }

    public Location Decode(string geohash) {
        var l = geohasher.Decode(geohash);
        return new Location() {
            Latitude = l.latitude,
            Longitude = l.longitude,
            GeoHash = geohash
        };
    }
}