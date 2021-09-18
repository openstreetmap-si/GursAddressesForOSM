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
using OsmSharp.Tags;

namespace OsmGursBuildingImport
{
    partial class Program
    {
        record GeoOsmWithGeometry(Geometry Geometry, ICompleteOsmGeo OsmGeo);

        static async Task<int> Main(string[] args)
        {
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
                    osmBuilderFull.AddBuilding(gursBuilding, true);
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