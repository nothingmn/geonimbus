namespace GeoNimbus.Contracts;

public interface IGeoCache {

    Address GetNearest(double latitude, double longitude);
}