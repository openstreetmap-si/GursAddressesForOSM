using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.Index.Strtree;
using NetTopologySuite.IO;
using NetTopologySuite.Simplify;
using Newtonsoft.Json;
using OsmSharp;
using OsmSharp.Complete;
using OsmSharp.Db;
using OsmSharp.Db.Impl;
using OsmSharp.Geo;
using OsmSharp.Streams;
using OsmSharp.Tags;

namespace OsmGursBuildingImport
{
    partial class Program
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

        record GeoOsmWithGeometry(Geometry Geometry, ICompleteOsmGeo OsmGeo);

        static int Main(string[] args)
        {
            Directory.SetCurrentDirectory("/Users/davidkarlas/Projects/shp1234/");
            var currentDirectory = Directory.GetCurrentDirectory();
            var overridesDir = Path.Combine(currentDirectory, "overrides");
            if (!Directory.Exists(overridesDir))
            {
                Console.WriteLine("Overrides directory not found, are you running from inside root of repository?");
                return 1;
            }

            var dataFolder = Path.Combine(currentDirectory, "data");
            var outputFolder = Path.Combine(dataFolder, "output");
            Directory.CreateDirectory(outputFolder);
            Directory.CreateDirectory(Path.Combine(outputFolder, "polygons"));
            var tempDir = Path.Combine(dataFolder, "temp");

            var pbfFile = Path.Combine(tempDir, "filtered.pbf");
            if (!File.Exists(pbfFile))
            {
                Console.WriteLine("filtered.pbf file not found, did you run './getFilteredPbf.sh data/download/ data/temp/'?");
                return 2;
            }

            if (!Directory.Exists(Path.Combine(tempDir, "KS_SLO_SHP_G")))
            {
                Console.WriteLine("KS_SLO_SHP_G directory not found, did you run './getSource.sh data/download/ data/temp/'?");
                return 3;
            }

            Directory.CreateDirectory(Path.Combine(tempDir, "polygons"));

            FeatureInterpreter.DefaultInterpreter = new OsmToNtsConvert();
            var osmIndexTask = Task.Run(() => LoadOsmData(pbfFile));
            var model = new GursData(tempDir, overridesDir);
            STRtree<GeoOsmWithGeometry> osmIndex = osmIndexTask.Result;

            var baseUrlForBlobs = "https://osmstorage.blob.core.windows.net/gurs-import/";

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

            var osmosisCommand = new StringBuilder();
            osmosisCommand.AppendLine("#!/bin/bash");
            osmosisCommand.AppendLine("osmosis \\");
            osmosisCommand.AppendLine("  --read-pbf data/download/slovenia-latest.osm.pbf \\");
            osmosisCommand.AppendLine($"  --tee {model.ProcessingAreas.Count} \\");
            var geoJsonWriter = new GeoJsonWriter();
            foreach (var area in model.ProcessingAreas)
            {
                var josmCommands = new List<string>{
                    $"http://localhost:8111/import?new_layer=true&layer_locked=true&layer_name=All%20Buildings&url={baseUrlForBlobs}{area.Name}.full.osm",
                    $"http://localhost:8111/import?new_layer=true&layer_name=Merge%20This&url={baseUrlForBlobs}{area.Name}.merge.osm" 
                    //$"http://localhost:8111/import?new_layer=true&layer_name=OSM%20Data&url={baseUrlForBlobs}{area.Name}.original.osm.bz2"
                };

                var str = "[out:xml];"
                + "("
                + $"node({area.Geometry.EnvelopeInternal.MinY},{area.Geometry.EnvelopeInternal.MinX},{area.Geometry.EnvelopeInternal.MaxY},{area.Geometry.EnvelopeInternal.MaxX});"
                + "<;"
                + ");"
                + "(._;>;);"
                + "out meta;";

                josmCommands.Add($"http://localhost:8111/import?new_layer=true&layer_name=OSM%20Data&url=https://overpass.kumi.systems/api/interpreter?data={HttpUtility.UrlEncode(str)}");
                josmCommands.Add($"http://localhost:8111/zoom?left={area.Geometry.EnvelopeInternal.MinX}&right={area.Geometry.EnvelopeInternal.MaxX}&top={area.Geometry.EnvelopeInternal.MaxY}&bottom={area.Geometry.EnvelopeInternal.MinY}");

                if (ljubljana.Intersects(area.Geometry))
                {
                    josmCommands.Add("http://localhost:8111/imagery?id=LJUBLJANA-DOF-2020");
                }
                else
                {
                    josmCommands.Add("http://localhost:8111/imagery?id=GURS-DOF025");
                }

                josmCommands.Add("http://localhost:8111/imagery?id=GURS-buildings");

                // Turns out relation between area of geometry and distanceTolerance is kind of perfect...
                var simplePoly = TopologyPreservingSimplifier.Simplify(area.Geometry, area.Geometry.Area);

                project.Tasks.Add(new StmTask()
                {
                    Geometry = geoJsonWriter.Write(new Feature(simplePoly
                         , new AttributesTable(new Dictionary<string, object> {
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


                string polyPath = Path.Combine(tempDir, "polygons", area.Name + ".poly");
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

                osmosisCommand.AppendLine($"  --bp file={Path.Combine("data", "temp", "polygons", area.Name)}.poly \\");
                osmosisCommand.AppendLine($"  --wx {Path.Combine("data", "output", "polygons", area.Name)}.original.osm.bz2 \\");
            }
            File.WriteAllText(Path.Combine(outputFolder, "Tasks.json"), JsonConvert.SerializeObject(project));
            File.WriteAllText("createOriginals.sh", osmosisCommand.ToString());

            Parallel.ForEach(model.ProcessingAreas, (processingArea) =>
            {
                var osmBuilder = new OsmBuilder();
                var osmBuilderFull = new OsmBuilder();
                var osmModifiedList = new List<ICompleteOsmGeo>();
                foreach (var gursBuilding in processingArea.Buildings)
                {
                    osmBuilderFull.AddBuilding(gursBuilding);

                    bool foundMatch = false;
                    foreach (var aprox in osmIndex.Query(gursBuilding.Geometry.EnvelopeInternal))
                    {
                        var osmGeometry = aprox.Geometry;
                        if (osmGeometry is LinearRing ring)
                            osmGeometry = new Polygon(ring);
                        Geometry intersection;
                        try
                        {
                            intersection = osmGeometry.Intersection(gursBuilding.Geometry);
                        }
                        catch (TopologyException ex)
                        {
                            Console.WriteLine(ex.Message);
                            continue;
                        }
                        if (intersection.Area > gursBuilding.Geometry.Area * 0.7 && intersection.Area > osmGeometry.Area * 0.7)
                        {
                            foundMatch = true;
                            if (osmBuilder.UpdateBuilding(aprox.OsmGeo, gursBuilding))
                                osmModifiedList.Add(aprox.OsmGeo);
                            break;
                        }
                    }

                    if (foundMatch)
                        continue;
                    osmBuilder.AddBuilding(gursBuilding);
                }

                string areaPath = Path.Combine(outputFolder, $"{processingArea.Name}.merge");
                SaveData(areaPath, osmBuilder.GetGeos().Concat(osmModifiedList));
                SaveData(Path.ChangeExtension(areaPath, ".full"), osmBuilderFull.GetGeos());
            });
            return 0;
        }

        private static STRtree<GeoOsmWithGeometry> LoadOsmData(string path)
        {
            var fs = new FileStream(path, FileMode.Open);
            var osmStream = new PBFOsmStreamSource(fs);
            var db = new SnapshotDb(new MemorySnapshotDb(osmStream));
            var interpreter = new OsmToNtsConvert();
            var osmIndex = new STRtree<GeoOsmWithGeometry>();
            foreach (var osmGeo in osmStream)
            {
                if (!osmGeo.Tags.ContainsKey("building"))
                {
                    continue;
                }
                var osmGeoComplete = osmGeo.CreateComplete(db);
                var featureCollection = interpreter.Interpret(osmGeoComplete);
                if (featureCollection.Count != 1)
                {
                    Console.WriteLine("TODO: Warning, skipping: " + osmGeo);
                    continue;
                }

                var geometry = featureCollection[0].Geometry;
                var geoWithGeom = new GeoOsmWithGeometry(geometry, osmGeoComplete);
                osmIndex.Insert(geometry.EnvelopeInternal, geoWithGeom);
            }
            osmIndex.Build();
            return osmIndex;
        }

        private static void SaveData(string areaPath, IEnumerable<ICompleteOsmGeo> geos)
        {
            if (!geos.Any())
            {
                return;
            }
            using (var fsWriter = new FileStream(areaPath + ".osm", FileMode.Create))
            {
                var writer = new XmlOsmStreamTarget(fsWriter);
                writer.RegisterSource(geos.Select(o =>
                {
                    if (o is Node n)
                        return n;
                    return ((CompleteOsmGeo)o).ToSimple();
                }));
                writer.Pull();
            }
        }
    }
}