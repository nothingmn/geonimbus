namespace GeoNimbus.Contracts;

public interface IGeoHash {

    string Encode(double lat, double lon, int precision = 8);

    Location Decode(string geohash);
}