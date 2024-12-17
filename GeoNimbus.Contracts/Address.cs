namespace GeoNimbus.Contracts;

public class Address {
    public int Id { get; set; }  // Auto-incrementing ID, primary key
    public string Zipcode { get; set; } // 6 characters, required
    public string Number { get; set; }  // 30 characters, required
    public string Street { get; set; }  // 200 characters, required
    public string Street2 { get; set; } // 20 characters, nullable
    public string City { get; set; }    // 50 characters, required
    public string State { get; set; }   // 2 characters, required
    public string Plus4 { get; set; }   // 4 characters, nullable
    public string Country { get; set; } // 2 characters, required, default = "US"
    public double Latitude { get; set; }  // DECIMAL(8,6), required
    public double Longitude { get; set; } // DECIMAL(9,6), required
    public string Source { get; set; }   // 40 characters, nullable
    public string Geohash { get; set; }  // TEXT, nullable
}