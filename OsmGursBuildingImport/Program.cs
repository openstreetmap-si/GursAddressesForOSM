using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.BZip2;
using NetTopologySuite.Geometries;
using NetTopologySuite.Index.Strtree;
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
        record GeoOsmWithGeometry(Geometry Geometry, ICompleteOsmGeo OsmGeo);

        static async Task<int> Main(string[] args)
        {
            var repoRoot=Path.Combine(Directory.GetCurrentDirectory(), "..");
            var overridesDir = Path.Combine(repoRoot, "overrides");
            if (!Directory.Exists(overridesDir))
            {
                Console.WriteLine("Overrides directory not found, are you running from inside root of repository?");
                return 1;
            }

            var dataFolder = Path.Combine(repoRoot, "data");
            var tempDir = Path.Combine(dataFolder, "temp/");
            await Process.Start(new ProcessStartInfo(){
               FileName=  Path.Combine(repoRoot, "getSource.sh"),
               Arguments = $"{Path.Combine(dataFolder, "download/")} {tempDir}",
               WorkingDirectory = repoRoot
            })!.WaitForExitAsync();

            var cacheDir = Path.Combine(dataFolder, "cache");
            if (Directory.Exists(cacheDir))
                Directory.Delete(cacheDir, true);

            Directory.CreateDirectory(cacheDir);

            FeatureInterpreter.DefaultInterpreter = new OsmToNtsConvert();
            Console.WriteLine("Loading GURS data");
            GursData gursData = new GursData(tempDir, overridesDir, tempDir);
            Console.WriteLine("Loaded GURS data");
            SimpleTaskManager.CreateStmProjectJson(tempDir, gursData);
            Console.WriteLine("Generated project.json.");

            var fullFileCacheFolder = Path.Combine(cacheDir, "full");
            Directory.CreateDirectory(fullFileCacheFolder);
            Parallel.ForEach(gursData.ProcessingAreas, (processingArea) =>
            {
                CreateFull(processingArea.Value, fullFileCacheFolder);
            });
            Console.WriteLine("Generated all .full.osm.bz2 files.");

            string originalsCacheFolder = Path.Combine(cacheDir, "original");
            Directory.CreateDirectory(originalsCacheFolder);
            var osmDataDownloader = new OsmDataDownloader(originalsCacheFolder, gursData);

            var app = WebApplication.Create(args);

            app.MapGet("/original/{filename}", async (string filename) =>
            {
                var area = FileNameToArea(filename, gursData);
                if (area == null)
                {
                    return Results.BadRequest("Invalid file name.");
                }
                var pathToFile = await osmDataDownloader.GetOriginalPbfFileAsync(area);
                using var fs = new FileStream(pathToFile, FileMode.Open);
                var pbfSource = new PBFOsmStreamSource(fs);
                var memoryStream = new MemoryStream();
                using (var bzip2Stream = new BZip2OutputStream(memoryStream) { IsStreamOwner = false })
                {
                    var xmlTarget = new XmlOsmStreamTarget(bzip2Stream);
                    xmlTarget.RegisterSource(pbfSource);
                    xmlTarget.Pull();
                }
                memoryStream.Position = 0;
                return Results.Stream(memoryStream, fileDownloadName: filename);
            });
            app.MapGet("/full/{filename}", (string filename) =>
            {
                return Results.File(Path.Combine(fullFileCacheFolder, filename));
            });
            app.MapGet("/merge/{filename}", async (string filename) =>
            {
                var area = FileNameToArea(filename, gursData);
                if (area == null)
                {
                    return Results.BadRequest("Invalid file name.");
                }
                var pathToFile = await osmDataDownloader.GetOriginalPbfFileAsync(area);
                var memoryStream = new MemoryStream();
                using (var bzip2Stream = new BZip2OutputStream(memoryStream) { IsStreamOwner = false })
                    CreateMergeFile(area, LoadOsmData(pathToFile), bzip2Stream);
                memoryStream.Position = 0;
                return Results.Stream(memoryStream, fileDownloadName: filename);
            });

            app.Urls.Add("http://*:2009");
            Console.WriteLine("Starting WebServer.");
            await app.RunAsync();
            return 0;
        }

        private static ProcessingArea? FileNameToArea(string filename, GursData gursData)
        {
            var dot = filename.IndexOf('.');
            if (dot == -1)
            {
                return null;
            }

            if (filename[dot..] is not (".original.osm.bz2" or
                ".merge.osm.bz2" or
                ".full.osm.bz2"))
            {
                return null;
            }

            if (!int.TryParse(filename[..dot], out var id))
            {
                return null;
            }

            if (gursData.ProcessingAreas.TryGetValue(id, out var area))
            {
                return area;
            }

            return null;
        }

        private static void CreateMergeFile(ProcessingArea processingArea, STRtree<GeoOsmWithGeometry> osmIndex, Stream writeTo)
        {
            var osmBuilder = new OsmBuilder();
            var osmModifiedList = new List<ICompleteOsmGeo>();
            foreach (var gursBuilding in processingArea.Buildings)
            {
                var queriedElements = osmIndex.Query(gursBuilding.Geometry.EnvelopeInternal).ToArray();
                var intersectingBuildings = queriedElements.Where(g =>
                {
                    try
                    {
                        return (g.OsmGeo is CompleteWay || g.OsmGeo is CompleteRelation) && gursBuilding.Geometry.Intersects(g.Geometry);
                    }
                    catch (TopologyException)
                    {
                        return false;
                    }
                });

                GeoOsmWithGeometry? intersectingBuilding = null;
                foreach (var building in intersectingBuildings)
                {
                    if (building.OsmGeo.Tags.TryGetValue("ref:gurs:sta_sid", out var sta_sid) &&
                        sta_sid == gursBuilding.Id.ToString())
                    {
                        // Looks like this area was already imported and we have matching ID
                        // it doesn't matter if intersecting area is smaller than 70%, match it.
                        intersectingBuilding = building;
                        break;
                    }

                    var osmGeometry = building.Geometry;
                    Geometry intersection;
                    try
                    {
                        intersection = osmGeometry.Intersection(gursBuilding.Geometry);
                    }
                    catch (TopologyException)
                    {
                        continue;
                    }
                    if (intersection.Area > gursBuilding.Geometry.Area * 0.7 && intersection.Area > osmGeometry.Area * 0.7)
                    {
                        intersectingBuilding = building;
                        break;
                    }
                }

                bool setAddressOnBuilding = true;
                var nodesOfIntrest = queriedElements
                        .Where(n => n.OsmGeo is Node && n.OsmGeo.Tags.Any(k => k.Key.StartsWith("addr:")))
                        .Where(n =>
                        {
                            try
                            {
                                return intersectingBuilding?.Geometry.Intersects(n.Geometry) ?? false
                                || gursBuilding.Geometry.Intersects(n.Geometry);
                            }
                            catch (TopologyException)
                            {
                                return false;
                            }
                        })
                        .ToList();

                if (nodesOfIntrest.Count > 0)
                {
                    setAddressOnBuilding = false;
                    if (nodesOfIntrest.Count == 1 && gursBuilding.Addresses?.Count == 1)
                    {
                        // So we have one existing addr: node + gurs only has 1 address, win-win...
                        if (OsmBuilder.SetAddressAttributes(gursBuilding.Addresses[0], nodesOfIntrest[0].OsmGeo.Tags))
                            osmModifiedList.Add(nodesOfIntrest[0].OsmGeo);
                    }
                    else if ((gursBuilding.Addresses?.Count ?? 0) == 0)
                    {
                        foreach (var node in nodesOfIntrest)
                        {
                            OsmBuilder.AddFixmeAttribute(node.OsmGeo.Tags, "This node was inside building without addresses.");
                            osmModifiedList.Add(node.OsmGeo);
                        }
                    }
                    else
                    {
                        // So we have at least multiple of something, either existing or imported...
                        // Lets try to match them...

                        foreach (var imported in gursBuilding.Addresses!)
                        {
                            bool foundAddressMatch = false;
                            foreach (var existing in nodesOfIntrest.ToArray())
                            {
                                if (existing.OsmGeo.Tags.TryGetValue("addr:housenumber", out var houseNumber) &&
                                houseNumber.Equals(imported.HouseNumber, StringComparison.OrdinalIgnoreCase) &&
                                (!existing.OsmGeo.Tags.TryGetValue("addr:street", out var street) ||
                                street.Equals(imported.StreetName.Name, StringComparison.OrdinalIgnoreCase)))
                                {
                                    nodesOfIntrest.Remove(existing);
                                    if (OsmBuilder.SetAddressAttributes(imported, existing.OsmGeo.Tags))
                                        osmModifiedList.Add(existing.OsmGeo);
                                    foundAddressMatch = true;
                                }
                            }
                            if (!foundAddressMatch)
                                osmBuilder.CreateNewNodeFromAddress(imported);
                        }
                        foreach (var unmatched in nodesOfIntrest)
                        {
                            OsmBuilder.AddFixmeAttribute(unmatched.OsmGeo.Tags, "This node was inside building but didn't match any of its addresses.");
                            osmModifiedList.Add(unmatched.OsmGeo);
                        }
                    }
                }

                if (intersectingBuilding != null)
                {
                    if (osmBuilder.UpdateBuilding(intersectingBuilding.OsmGeo, gursBuilding, setAddressOnBuilding))
                        osmModifiedList.Add(intersectingBuilding.OsmGeo);
                }
                else
                {
                    osmBuilder.AddBuilding(gursBuilding, setAddressOnBuilding);
                }
            }

            SaveData(writeTo, osmBuilder.GetGeos().Concat(osmModifiedList));
        }

        private static void CreateFull(ProcessingArea processingArea, string cacheFolder)
        {
            var osmBuilderFull = new OsmBuilder();
            foreach (var gursBuilding in processingArea.Buildings)
            {
                osmBuilderFull.AddBuilding(gursBuilding, true);
            }

            string areaPath = Path.Combine(cacheFolder, $"{processingArea.Name}.full");
            SaveData(areaPath, osmBuilderFull.GetGeos());
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
                    if (osmGeo is not Node)
                    {
                        Console.WriteLine("TODO: Warning, skipping: " + osmGeo);
                    }
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
                return;
            using (var fsWriter = new FileStream(areaPath + ".osm.bz2", FileMode.Create))
            using (var compressStream = new ICSharpCode.SharpZipLib.BZip2.BZip2OutputStream(fsWriter))
            {
                SaveData(compressStream, geos);
            }
        }

        private static void SaveData(Stream stream, IEnumerable<ICompleteOsmGeo> geos)
        {
            var writer = new XmlOsmStreamTarget(stream);
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