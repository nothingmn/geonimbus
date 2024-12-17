namespace GeoNimbus.Contracts;

public interface ICache<T> {

    void Add(double latitude, double longitude, string id, T data);

    List<T> Query(double minLat, double maxLat, double minLon, double maxLon);

    bool TryGet(string id, out T value);
}