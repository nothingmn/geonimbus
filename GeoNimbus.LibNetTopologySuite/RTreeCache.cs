using NetTopologySuite.Index.Strtree;
using System.Collections.Concurrent;
using GeoNimbus.Contracts;

namespace GeoNimbus.LibNetTopologySuite;

public class RTreeCache<T> : ICache<T> {
    private STRtree<T> _spatialTree;
    private ConcurrentDictionary<string, T> _data;

    public RTreeCache() {
        _spatialTree = new STRtree<T>();
        _data = new ConcurrentDictionary<string, T>();
    }

    private void RebuildTree() {
        _spatialTree = new STRtree<T>();
        foreach (var entry in _data.Values) {
            if (entry is Address) {
                var e = entry as Address;
                var point = new NetTopologySuite.Geometries.Point(e.Longitude, e.Latitude);
                _spatialTree.Insert(point.EnvelopeInternal, entry);
            }
        }
    }

    public void Add(double latitude, double longitude, string id, T data) {
        //var point = new NetTopologySuite.Geometries.Point(longitude, latitude);
        //_spatialTree.Insert(point.EnvelopeInternal, data);
        _data[id] = data;
        RebuildTree();
    }

    public List<T> Query(double minLat, double maxLat, double minLon, double maxLon) {
        var envelope = new NetTopologySuite.Geometries.Envelope(minLon, maxLon, minLat, maxLat);
        return _spatialTree.Query(envelope).ToList();
    }

    public bool TryGet(string id, out T value) {
        return _data.TryGetValue(id, out value);
    }

    public bool TryRemove(string id, out T value) {
        return _data.Remove(id, out value);
    }
}