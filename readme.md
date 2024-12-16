## Project GeoNimbus

### Overview:
To provide something useful, in the form of an API layer above:
https://netsyms.com/gis/addresses

Ideally it will be deployed as a Docker container, NOT including the source Sqlite Database (its far too large).  Instead providing a docker mount point for the database.

We will take care of caching and provide an API, with methods to do geocoding, reverse geocoding, possibly spatial queries, distance calculations, and a variety of other APIs which some may find useful.

*Note: Im not affialiated with the folks at https://netsyms.com but really appreciate the work done.*

----
## Pre-amble
My first crack at this database was to spin up this docker container:

```docker
# Use a lightweight Python base image
FROM python:3.11-slim

# Set the working directory in the container
WORKDIR /app

# Install required dependencies
RUN pip install --no-cache-dir datasette datasette-dashboards datasette-cluster-map && apt-get update && apt-get install -y unzip

# Copy the zipped SQLite database file to the container
COPY AddressDatabase2024.zip /app/

# Set environment variable to enforce read-only access
ENV DATASETTE_READ_ONLY=true

# Expose the default Datasette port
EXPOSE 8001

# Command to unzip and start Datasette in read-only mode
CMD ["sh", "-c", "unzip -o AddressDatabase2024.zip && datasette serve  --setting sql_time_limit_ms 10000  --setting max_returned_rows 2000 --setting default_page_size 50  --setting num_sql_threads 10  --host 0.0.0.0 --port 8001 --immutable AddressDatabase2024.sqlite"]
```

Run it with...

```bash
chmod 444 AddressDatabase2024.zip
docker build -t address-data-sqlite-readonly .
docker run -d -p 8001:8001 address-data-sqlite-readonly
```


It spins up an instance of https://datasette.io/ around the database.  Notice at docker start it will unzip the database, which takes a bit of time so be patient.  I added the plugins:

datasette-dashboards
datasette-cluster-map 

Just to get folks started on creating some UI around the database.  This is a fantastic start for those who want a visual way to explore the database.

----
Next, my investigation continued to the schema itself.

```sql
CREATE TABLE addresses (
	 zipcode	VARCHAR ( 6 ) NOT NULL,
	 number	VARCHAR ( 30 ) NOT NULL,
	 street	VARCHAR ( 200 ) NOT NULL,
	 street2	VARCHAR ( 20 ),
	 city	VARCHAR ( 50 ) NOT NULL,
	 state	CHAR ( 2 ) NOT NULL,
   plus4	CHAR ( 4 ),
   country CHAR ( 2 ) NOT NULL DEFAULT "US",
	 latitude	DECIMAL ( 8 , 6 ) NOT NULL,
	 longitude	DECIMAL( 9 , 6 ) NOT NULL,
   source	VARCHAR( 40 ), geohash TEXT,
   UNIQUE (zipcode, number, street, street2, country)
)
```

I wasnt super happy about the fact that there was no ID field.  Maybe im overthinking it, but I decided to add a simple auto incrementing field.  I also decided to geohash the lat/lon for each row, into a new field "geohash".

Here is the new schema:

```sql
CREATE TABLE addresses (
    zipcode   VARCHAR (6) NOT NULL,
    number    VARCHAR (30) NOT NULL,
    street    VARCHAR (200) NOT NULL,
    street2   VARCHAR (20),
    city      VARCHAR (50) NOT NULL,
    state     CHAR (2) NOT NULL,
    plus4     CHAR (4),
    country   CHAR (2) NOT NULL DEFAULT "US",
    latitude  DECIMAL (8 , 6) NOT NULL,
    longitude DECIMAL (9 , 6) NOT NULL,
    source    VARCHAR (40),
    geohash   TEXT,
    UNIQUE (zipcode, number, street, street2, country)
)
```

And some python to make all of these changes to our copy of the database, this is in the form of 2 python scripts, the first will create the autoincrementing primary key (into a new database):


```python
import sqlite3

db_path = "AddressDatabase2024.sqlite"

def recreate_table_with_id():
    """
    Recreate the addresses table with an auto-incrementing ID column.
    """
    with sqlite3.connect(db_path) as conn:
        cursor = conn.cursor()

        # Create a new table with the desired schema
        cursor.execute("""
        CREATE TABLE addresses_new (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            zipcode VARCHAR(6) NOT NULL,
            number VARCHAR(30) NOT NULL,
            street VARCHAR(200) NOT NULL,
            street2 VARCHAR(20),
            city VARCHAR(50) NOT NULL,
            state CHAR(2) NOT NULL,
            plus4 CHAR(4),
            country CHAR(2) NOT NULL DEFAULT "US",
            latitude DECIMAL(8, 6) NOT NULL,
            longitude DECIMAL(9, 6) NOT NULL,
            source VARCHAR(40),
            geohash TEXT,
            UNIQUE (zipcode, number, street, street2, country)
        );
        """)

        # Copy data from the old table to the new table
        cursor.execute("""
        INSERT INTO addresses_new (zipcode, number, street, street2, city, state, plus4, country, latitude, longitude, source, geohash)
        SELECT zipcode, number, street, street2, city, state, plus4, country, latitude, longitude, source, geohash
        FROM addresses;
        """)

        # Drop the old table
        cursor.execute("DROP TABLE addresses;")

        # Rename the new table to the original table name
        cursor.execute("ALTER TABLE addresses_new RENAME TO addresses;")

def main():
    try:
        print("Recreating table with ID column...")
        recreate_table_with_id()
        print("Table successfully recreated with ID column.")
    except sqlite3.Error as e:
        print(f"SQLite error: {e}")
    except Exception as e:
        print(f"Error: {e}")

if __name__ == "__main__":
    main()

```

Our second python script will calculate geohashs and update the table:


```python
import sqlite3
import geohash2 as geohash
import math

# Path to your SQLite database
db_path = "AddressDatabase2024.sqlite"

def add_columns():
    """
    Adds auto-incrementing id and geohash columns to the database schema if not already present.
    """
    with sqlite3.connect(db_path) as conn:
        cursor = conn.cursor()
        Add the geohash column if not present
        cursor.execute("""
        ALTER TABLE addresses ADD COLUMN geohash TEXT;
        """)

def calculate_geohashes(batch_size=10000):
    """
    Calculates geohashes for all rows in the table in batches.
    """
    with sqlite3.connect(db_path) as conn:
        cursor = conn.cursor()
        
        # Fetch total row count for progress tracking
        cursor.execute("SELECT COUNT(*) FROM addresses where geohash is NULL ;")
        total_rows = cursor.fetchone()[0]
        print(f"Total rows to process: {total_rows}")

        offset = 0
        while True:
            # Fetch a batch of rows
            cursor.execute(f"""
            SELECT id, latitude, longitude FROM addresses WHERE geohash IS NULL LIMIT {batch_size} OFFSET {offset};
            """)
            rows = cursor.fetchall()

            if not rows:
                break  # No more rows to process
            
            # Calculate geohashes for the batch
            # 9 	≤ 4.77m 	× 	4.77m
            updates = [(geohash.encode(lat, lon, 9), row_id) for row_id, lat, lon in rows]
            
            # Update the database with the calculated geohashes
            cursor.executemany("""
            UPDATE addresses SET geohash = ? WHERE id = ?;
            """, updates)
            conn.commit()  # Commit after each batch

            offset += len(rows)
            print(f"Processed {offset}/{total_rows} rows... ({(offset / total_rows) * 100:.2f}% complete)")


def main():
    """
    Main function to add columns and calculate geohashes.
    """
    try:
        print("Adding ID and Geohash columns...")
        add_columns()
        print("Calculating geohashes...")
        calculate_geohashes()
        print("Geohash calculation completed successfully.")
    except sqlite3.Error as e:
        print(f"SQLite error: {e}")
    except Exception as e:
        print(f"Error: {e}")

if __name__ == "__main__":
    main()

```

Notice I chose a precision of "9" for the geohash, here is why:
https://www.movable-type.co.uk/scripts/geohash.html

| Geohash length | Cell width   | Cell height  |
|----------------|--------------|--------------|
| 1              | ≤ 5,000km    | × 5,000km    |
| 2              | ≤ 1,250km    | × 625km      |
| 3              | ≤ 156km      | × 156km      |
| 4              | ≤ 39.1km     | × 19.5km     |
| 5              | ≤ 4.89km     | × 4.89km     |
| 6              | ≤ 1.22km     | × 0.61km     |
| 7              | ≤ 153m       | × 153m       |
|**8**              | **≤ 38.2m**      | **× 19.1m**      |
|**9**              | **≤ 4.77m**      | **× 4.77m**      |
|**10**             | **≤ 1.19m**      | **× 0.596m**     |
| 11             | ≤ 149mm      | × 149mm      |
| 12             | ≤ 37.2mm     | × 18.6mm     |

Clearly a precision of 8 would suffice, at 19.1 meters, but I chose the just slighly more precise level of 9.

FYI, both of these python scripts were GPT generated and should be reviewed in depth by you before running on your system.  I noticed the second script needed to be ran multiple times, and I reduced the 'batch_size=10000' down accordingly.  If someone wants to take the time to correct it, just do a PR.

We now have a database with a auto incrementing primary key, and geohashs.  GPT recommends some indexes be created as well, use a tool or the above code to make the change if you need:

```sql

CREATE INDEX idx_geohash ON addresses(geohash);
CREATE INDEX idx_lat_lon ON addresses(latitude, longitude);

```
These do take time if your running them manually, so again, be patient.  Btw here is a tool you can use to open, view the database structure, browse the data, and execute sql against a sqlite database:

https://sqlitebrowser.org/dl/

I just grabbed the zipped version.  Quick and easy, no install.  Open the database, and execute the sql above, you should see something like:

```
Execution finished without errors.
Result: query executed successfully. Took 197893ms
At line 1:
CREATE INDEX idx_lat_lon ON addresses(latitude, longitude);
```

Dont forget to "Write Changes" and then exit the app.  


----
Now with all of the pre-amble over with, lets dig into the main topic of this repository...

### **Full Set of Requirements**
Here’s a comprehensive list of **technical** and **non-technical requirements** based on all the discussions, features, and design concept.

---

### **Technical Requirements**

I would consider this first set of features as "Phase 1"

#### **Core Features**
1. **Caching**
   - Hybrid caching system:
     - **Hot Cache**: In-memory caching using spatial data structures (e.g., R-Tree, QuadTree, KD-Tree).
     - **Cold Cache**: SQLite for less frequently accessed data.
   - Configurable eviction policy for hot cache (e.g., Least Recently Used).

2. **Geocoding**
   - Convert address to latitude/longitude.
   - Convert address to geohash.
   - Batch geocoding for multiple addresses.

3. **Reverse Geocoding**
   - Convert latitude/longitude to the nearest address.
   - Convert geohash to the corresponding address or bounding box.
   - Batch reverse geocoding for multiple coordinates.

4. **Spatial Queries**
   - Find the nearest point to a given latitude/longitude.
   - Query points within a radius.
   - Query points within a bounding box.
   - Generate heatmaps for a specified geographic region.
   - Retrieve neighboring geohashes for a given geohash.
   - Cluster points by geohash or other criteria.

5. **Distance Calculation**
   - Use Haversine formula for precise distance between coordinates.
   - Apply bounding box approximations for faster filtering when precision is less critical.


#### **API Design and Patterns**
6. **RESTful API Design**
   - Expose geospatial features via REST endpoints.
   - Define clear, concise endpoints with appropriate HTTP methods (GET, POST).
   - Support query parameters for customizable requests (e.g., precision, filters).

7. **Async/Await for Non-Blocking Operations**
   - All APIs must use asynchronous methods to handle concurrent requests efficiently.


8. **Health Endpoint Monitoring**
   - Expose a `/health` endpoint to report system health:
     - Hot cache availability and memory usage.
     - Cold cache connection health.
     - System resource usage (e.g., CPU, memory).

9. **Logging and Telemetry**
   - Capture logs for API usage, errors, and performance metrics.
   - Use distributed tracing tools like Application Insights or OpenTelemetry.
   - Log details like request execution time, cache hits/misses, and database fallback usage.


#### **Cloud Native Design**
12. **Containerization**
    - Package the application as Docker containers for consistent deployment.
    - Include containerized dependencies like SQLite or Redis for local development.

13. **Orchestration Support**
    - Deploy the system on Kubernetes or other orchestration platforms for scalability.
    - Include readiness and liveness probes for Kubernetes health checks.

14. **Integration with Cloud Services**
    - Utilize managed cloud services for scalability:
      - Redis for distributed caching.
      - PostGIS or cloud databases for geospatial querying.

#### **Documentation and User Experience**
15. **Comprehensive API Documentation**
    - Use Swagger/OpenAPI to auto-generate API documentation.
    - Include:
      - Request and response examples.
      - Error handling details.
      - Description of query parameters and return types.

---

### **Phase-Based Advanced Features**

#### **Phase 2**
1. **Polygon Queries**
   - Query points within a custom polygon (e.g., city boundaries or drawn areas).

2. **On-Demand Cache Prefetching**
   - Enable users to preload specific regions (e.g., geohashes or bounding boxes) into the hot cache.

3. **L1 and L2 Cache**
   - Implement a hierarchical caching strategy:
     - **L1 Cache**: In-memory for low-latency access.
     - **L2 Cache**: Redis for shared caching across instances.
     - Cold Cache: Database fallback (e.g., SQLite or PostGIS).

4. **SDKs for Common Languages**
   - Provide developer SDKs for:
     - Python
     - JavaScript
     - Java
     - C#

5. **Circuit Breaker**  (Phase 2)
    - Implement a circuit breaker pattern using a library like Polly:
      - Open circuit after a defined number of database query failures.
      - Automatically retry queries after a cool-down period.
      - Fallback to cache if the database is unavailable.

6. **Throttling**  (Phase 2)
    - Protect system resources by throttling excessive requests:
      - Apply global and per-endpoint throttling.
      - Use middleware to enforce limits.
      
      
#### **Phase 3**
5. **API Usage Metrics**
   - Capture and expose usage patterns:
     - Most queried regions.
     - Cache performance (hit/miss rates).
     - API response times and latencies.

6. **Rate Limiting Per Endpoint**
   - Apply endpoint-specific rate limits based on resource intensity:
     - Higher limits for lightweight queries.
     - Lower limits for expensive queries.

7. **API Key Management**
   - Allow users to:
     - Generate and manage API keys.
     - Configure quotas and expiration dates for keys.
     - Enable role-based access control (e.g., read-only keys).

8. **Export Formats**
   - Allow users to export query results in common geospatial formats:
     - GeoJSON
     - KML
     - Shapefile

---

### **Non-Technical Requirements**

#### **1. Scalability**
- The system must handle datasets with 150+ million rows.
- Design APIs to scale horizontally with increased traffic.
- Ensure caching mechanisms are extensible to manage growing data efficiently.

#### **2. Performance**
- Ensure hot cache queries are served in milliseconds.
- Optimize cold cache queries with indexing and caching.
- Batch operations should process large datasets efficiently without overloading the system.

#### **3. Reliability**
- Maintain high availability even under heavy load.
- Include health monitoring for critical system components (cache, database, services).
- Fallback mechanisms must ensure partial functionality during component failures.

#### **4. Extensibility**
- Use modular design principles:
  - Rely on a Dependancy Injection container for object lifetime managment, and extensibility.  
  - Swappable components for future integrations (e.g., PostGIS or NoSQL).
- Ensure new features (e.g., polygon queries) can be added with minimal impact on existing code.

#### **5. Developer Experience**
- Provide a seamless developer onboarding experience:
  - Clear API documentation with code samples.
  - Developer SDKs for major programming languages.
- Include a sandbox environment for API testing.

#### **6. Security**
- Validate input parameters to prevent malicious queries.
- Ensure data encryption at rest and in transit.
- Implement authentication and authorization using API keys or tokens.
- Provide audit logs for API usage.

#### **7. Cost Efficiency**
- Optimize resource usage to minimize infrastructure costs:
  - Use Redis and cloud databases selectively for high-value use cases.
  - Cache frequently queried regions to reduce database load.

#### **8. Testing and Validation**
- Include unit, integration, and load testing.
- Simulate cold cache failures to validate circuit breaker functionality.
- Perform performance benchmarks to ensure SLA compliance.

#### **9. Deployment and Maintenance**
- Automate deployments using CI/CD pipelines.
- Provide scripts for infrastructure setup (e.g., Docker Compose, Helm charts).
- Offer clear upgrade paths for schema changes or feature additions.

#### **10. Legal and Compliance**
- Ensure compliance with data protection laws (e.g., GDPR, CCPA) if handling user-sensitive data.
- Implement role-based access control to limit access to sensitive APIs or data.

---

### **Summary**
This full set of **technical** and **non-technical requirements** provides a roadmap for building a robust, scalable, and user-friendly geospatial caching and querying system. It prioritizes core functionality in Phase 1 while allowing for iterative improvements in Phase 2 and Phase 3.


### References:
- https://chatgpt.com/share/675e9e25-525c-8011-8d73-09f5d501be1a
- https://learn.microsoft.com/en-us/azure/architecture/patterns/
