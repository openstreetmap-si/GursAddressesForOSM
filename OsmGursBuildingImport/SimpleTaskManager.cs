using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using NetTopologySuite.Simplify;
using Newtonsoft.Json;

namespace OsmGursBuildingImport
{
    class SimpleTaskManager
    {
        public class StmTask
        {
            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("processPoints")]
            public int ProcessPoints { get; set; }

            [JsonProperty("maxProcessPoints")]
            public int MaxProcessPoints { get; set; }

            [JsonProperty("geometry")]
            public string Geometry { get; set; }

            [JsonProperty("assignedUser")]
            public string AssignedUser { get; set; }
        }

        public class StmProject
        {
            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("users")]
            public List<string> Users { get; set; }

            [JsonProperty("owner")]
            public string Owner { get; set; }

            [JsonProperty("description")]
            public string Description { get; set; }

            [JsonProperty("creationDate")]
            public DateTime CreationDate { get; set; }

            [JsonProperty("tasks")]
            public List<StmTask> Tasks { get; set; }
        }

        public class JsonPolygon
        {
            [JsonProperty("file_name")]
            public string FileName { get; set; }

            [JsonProperty("file_type")]
            public string FileType { get; set; }
        }

        public class Extract
        {
            [JsonProperty("output")]
            public string Output { get; set; }

            [JsonProperty("polygon")]
            public JsonPolygon Polygon { get; set; }
        }

        public class Root
        {
            [JsonProperty("directory")]
            public string Directory { get; set; }

            [JsonProperty("extracts")]
            public List<Extract> Extracts { get; set; }
        }

        public static void CreateStmProjectJson(string outputFolder, string tempDir, GursData model)
        {
            var baseUrlForBlobs = "https://osm-func.azurewebsites.net/api/BlobDownloader/";

            var ljubljana = model.Settlements.Values.Single(s => s.Name.Name == "Ljubljana").Geometry;
            var project = new StmProject()
            {
                Name = "Gurs Buildings&Addresses Import",
                Owner = "12422736",
                Users = new(new[] { "12422736" }),
                Tasks = new List<StmTask>(),
                CreationDate = DateTime.Now,
                Description = "How to do this, go here: http://example.com"
            };

            var json = new Root();
            json.Directory = "data/output/";
            json.Extracts = new List<Extract>();

            var poliesDir = Path.Combine(tempDir, "polygons");
            Directory.CreateDirectory(poliesDir);

            var geoJsonWriter = new GeoJsonWriter();
            foreach (var area in model.ProcessingAreas)
            {
                var josmCommands = new List<string>{
                    $"http://localhost:8111/import?new_layer=true&layer_locked=true&layer_name=All%20Buildings&url={HttpUtility.UrlEncode($"{baseUrlForBlobs}{area.Name}.full.osm.bz2")}",
                    $"http://localhost:8111/import?new_layer=true&layer_name=Merge%20This&url={HttpUtility.UrlEncode($"{baseUrlForBlobs}{area.Name}.merge.osm.bz2")}",
                    $"http://localhost:8111/import?new_layer=true&layer_name=OSM%20Data&url={HttpUtility.UrlEncode($"{baseUrlForBlobs}{area.Name}.original.osm.bz2")}"
                };

                josmCommands.Add($"http://localhost:8111/zoom?left={area.Geometry.EnvelopeInternal.MinX}&right={area.Geometry.EnvelopeInternal.MaxX}&top={area.Geometry.EnvelopeInternal.MaxY}&bottom={area.Geometry.EnvelopeInternal.MinY}");

                josmCommands.Add("http://localhost:8111/imagery?id=GURS-DOF025");
                if (ljubljana.Intersects(area.Geometry))
                {
                    josmCommands.Add("http://localhost:8111/imagery?id=LJUBLJANA-DOF-2020");
                }

                josmCommands.Add("http://localhost:8111/imagery?id=GURS-buildings");

                // Turns out relation between area of geometry and distanceTolerance is kind of perfect...
                var simplePoly = TopologyPreservingSimplifier.Simplify(area.Geometry, area.Geometry.Area);

                project.Tasks.Add(new StmTask()
                {
                    Geometry = geoJsonWriter.Write(new Feature(simplePoly,
                         new AttributesTable(new Dictionary<string, object> {
                            { "name", area.Name },
                            { "josmCommands", josmCommands.ToArray()}}))),
                    MaxProcessPoints = area.Buildings.Count,
                    Name = area.Name
                });

                Polygon[] polygons;
                if (area.Geometry is MultiPolygon mp)
                {
                    polygons = mp.Geometries.OfType<Polygon>().ToArray();
                    if (polygons.Length != mp.Geometries.Length)
                        throw new Exception(string.Join(", ", mp.Geometries.Select(g => g.GetType().ToString())));
                }
                else if (area.Geometry is Polygon poly2)
                {
                    polygons = new[] { poly2 };
                }
                else
                {
                    throw new Exception(area.Geometry.GetType().ToString());
                }
                string polyPath = Path.Combine(poliesDir, area.Name + ".poly");
                using var sw = new StreamWriter(polyPath);
                sw.WriteLine(area.Name + ".original");
                for (int i = 0; i < polygons.Length; i++)
                {
                    sw.WriteLine("poly" + (i + 1));
                    foreach (var cord in polygons[i].Coordinates)
                    {
                        sw.WriteLine($"\t{cord.X} {cord.Y}");
                    }
                    sw.WriteLine("END");
                }
                sw.WriteLine("END");

                json.Extracts.Add(new Extract()
                {
                    Output = area.Name + ".original.osm",
                    Polygon = new JsonPolygon()
                    {
                        FileType = "poly",
                        FileName = Path.Combine("..", "polygons", area.Name + ".poly")
                    }
                });
            }
            File.WriteAllText(Path.Combine(outputFolder, "project.json"), JsonConvert.SerializeObject(project));
            int index = 0;
            var osmiumFolder = Path.Combine(tempDir, "osmium");
            Directory.CreateDirectory(osmiumFolder);
            const int OSMIUM_EXPORTS_AT_ONCE = 50;
            while (json.Extracts.Count > 0)
            {
                index++;
                File.WriteAllText(Path.Combine(osmiumFolder, $"export{index}.json"), JsonConvert.SerializeObject(new Root()
                {
                    Directory = json.Directory,
                    Extracts = new(json.Extracts.Take(OSMIUM_EXPORTS_AT_ONCE))
                }));
                json.Extracts.RemoveRange(0, Math.Min(json.Extracts.Count, OSMIUM_EXPORTS_AT_ONCE));
            }
        }

    }
}

