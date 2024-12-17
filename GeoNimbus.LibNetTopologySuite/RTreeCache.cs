using NetTopologySuite.Index.Strtree;
using System.Collections.Concurrent;
using GeoNimbus.Contracts;

namespace GeoNimbus.LibNetTopologySuite;

public class RTreeCache<T> : ICache<T> {
    private readonly STRtree<T> _spatialTree;
    private readonly ConcurrentDictionary<string, T> _data;

    public RTreeCache() {
        _spatialTree = new STRtree<T>();
        _data = new ConcurrentDictionary<string, T>();
    }

    public void Add(double latitude, double longitude, string id, T data) {
        var point = new NetTopologySuite.Geometries.Point(longitude, latitude);
        _spatialTree.Insert(point.EnvelopeInternal, data);
        _data[id] = data;
    }

    public List<T> Query(double minLat, double maxLat, double minLon, double maxLon) {
        var envelope = new NetTopologySuite.Geometries.Envelope(minLon, maxLon, minLat, maxLat);
        return _spatialTree.Query(envelope).ToList();
    }

    public bool TryGet(string id, out T value) {
        return _data.TryGetValue(id, out value);
    }
}