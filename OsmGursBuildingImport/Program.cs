using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NetTopologySuite.Geometries;
using NetTopologySuite.Index.Strtree;
using OsmSharp;
using OsmSharp.Complete;
using OsmSharp.Db;
using OsmSharp.Db.Impl;
using OsmSharp.Geo;
using OsmSharp.Streams;

namespace OsmGursBuildingImport
{
    partial class Program
    {
        record GeoOsmWithGeometry(Geometry Geometry, ICompleteOsmGeo OsmGeo);

        static async Task<int> Main(string[] args)
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
            STRtree<GeoOsmWithGeometry> osmIndex = await osmIndexTask;
            SimpleTaskManager.CreateStmProjectJson(outputFolder, tempDir, model);

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