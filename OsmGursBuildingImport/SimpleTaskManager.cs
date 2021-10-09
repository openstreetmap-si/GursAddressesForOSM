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

        public static void CreateStmProjectJson(string tempDir, GursData model)
        {
            var baseUrlForBlobs = "http://osm.karlas.si:2009/";

            var ljubljana = model.Settlements.Values.Single(s => s.Name.Name == "Ljubljana").Geometry;
            var project = new StmProject()
            {
                Name = "Gurs Buildings&Addresses Import",
                Owner = "12422736",
                Users = new(new[] { "12422736" }),
                Tasks = new List<StmTask>(),
                CreationDate = DateTime.Now,
                Description = ""
            };

            var geoJsonWriter = new GeoJsonWriter();
            foreach (var area in model.ProcessingAreas.Values)
            {
                var josmCommands = new List<string>{
                    $"http://localhost:8111/import?new_layer=true&layer_name=All%20Buildings&url={HttpUtility.UrlEncode($"{baseUrlForBlobs}full/{area.Name}.full.osm.bz2")}",
                    $"http://localhost:8111/import?new_layer=true&layer_name=Merge%20This&url={HttpUtility.UrlEncode($"{baseUrlForBlobs}merge/{area.Name}.merge.osm.bz2")}",
                    $"http://localhost:8111/import?new_layer=true&layer_name=OSM%20Data&url={HttpUtility.UrlEncode($"{baseUrlForBlobs}original/{area.Name}.original.osm.bz2")}"
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
            }
            File.WriteAllText(Path.Combine(tempDir, "project.json"), JsonConvert.SerializeObject(project));
        }
    }
}

