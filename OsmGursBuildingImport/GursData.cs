using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NetTopologySuite;
using NetTopologySuite.Algorithm;
using NetTopologySuite.Geometries;
using NetTopologySuite.Index.Strtree;
using NetTopologySuite.IO;

namespace OsmGursBuildingImport
{
    record BilingualName(string Name, string NameSecondLanguage);
    record PostInfo(short Id, string Name);// Posts that are in bilingual area have "Koper - Capodistria" format
    record VotingArea(Geometry Geometry, string Name, string Id);
    record BuildingInfo(int Id, Geometry Geometry, string? Date, List<Address>? Addresses);
    record SettlementInfo(Geometry Geometry, BilingualName Name);
    record Address(int Id, Geometry Geometry, string Date, string HouseNumber, BilingualName StreetName, PostInfo PostInfo, BilingualName VillageName);
    record ProcessingArea(Geometry Geometry, string Name, List<BuildingInfo> Buildings)
    {
        public bool Process { get; set; }
    }

    class GursData
    {
        private static GeometryFactory D96Factory = NtsGeometryServices.Instance.CreateGeometryFactory(new PrecisionModel(), 3794);
        public List<ProcessingArea> ProcessingAreas = new();

        Dictionary<int, BilingualName> Streets = new();
        public Dictionary<int, SettlementInfo> Settlements = new();
        Dictionary<int, PostInfo> Posts = new();
        Dictionary<int, Address> Addresses = new();
        List<BuildingInfo> Buildings = new();
        List<VotingArea> VotingAreas = new();
        Dictionary<string, Dictionary<string, string>> Overrides = new();
        STRtree<(int Id, Geometry Geometry)> SettlementsIndex = new();
        STRtree<VotingArea> VotingAreasIndex = new();

        public GursData(string dir, string overridesDir)
        {
            LoadOverrides(overridesDir);
            LoadStreets(dir);
            LoadSettlements(dir);
            LoadPosts(dir);
            LoadAddresses(dir);
            LoadBuildings(dir);
            LoadVotingAreas(dir);

            BuildProcessingAreas();
        }

        private void BuildProcessingAreas()
        {
            var processingAreasIndex = new STRtree<ProcessingArea>();
#if true || VOTING_AREAS
            foreach (var votingArea in VotingAreas)
            {
                var newArea = new ProcessingArea(
                    votingArea.Geometry,
                    $"{votingArea.Id}".Replace("/", "_"),
                    new List<BuildingInfo>());
                ProcessingAreas.Add(newArea);
                processingAreasIndex.Insert(votingArea.Geometry.EnvelopeInternal, newArea);
            }

            processingAreasIndex.Build();
            Parallel.ForEach(Buildings, (building) =>
            {
                foreach (var aprox in processingAreasIndex.Query(building.Geometry.EnvelopeInternal))
                {
                    if (!building.Geometry.Intersects(aprox.Geometry))
                        continue;
                    lock (aprox.Buildings)
                    {
                        aprox.Buildings.Add(building);
                    }
                    return;
                }
            });
#elif true || SPLIT_BY_VOTING_AREAS_AND_SEATTLEMENTS
            foreach (var votingArea in VotingAreas)
            {
                foreach (var aproxSeattlement in SettlementsIndex.Query(votingArea.Geometry.EnvelopeInternal))
                {
                    var intersection = aproxSeattlement.Geometry.Intersection(votingArea.Geometry);
                    if (intersection.Area > 0)
                    {
                        var newArea = new ProcessingArea(
                            intersection,
                            $"{Settlements[aproxSeattlement.Id].Name}-{votingArea.Id}".Replace("/", "_"),
                            new List<BuildingInfo>());
                        ProcessingAreas.Add(newArea);
                        processingAreasIndex.Insert(intersection.EnvelopeInternal, newArea);
                    }
                }
            }
            
            processingAreasIndex.Build();
            Parallel.ForEach(Buildings, (building) =>
            {
                foreach (var aprox in processingAreasIndex.Query(building.Geometry.EnvelopeInternal))
                {
                    if (!building.Geometry.Intersects(aprox.Geometry))
                        continue;
                    lock (aprox.Buildings)
                    {
                        aprox.Buildings.Add(building);
                    }
                    return;
                }
            });

#elif true || SPLIT_BY_VOTING_AREAS_AND_SEATTLEMENTS_AS_NEEDED
            foreach (var votingArea in VotingAreas)
            {
                var newArea = new ProcessingArea(
                    votingArea.Geometry,
                    votingArea.Id,
                    new List<BuildingInfo>());
                ProcessingAreas.Add(newArea);
                processingAreasIndex.Insert(newArea.Geometry.EnvelopeInternal, newArea);
            }

            Parallel.ForEach(Buildings, (building) =>
            {
                foreach (var aprox in processingAreasIndex.Query(building.Geometry.EnvelopeInternal))
                {
                    if (!building.Geometry.Intersects(aprox.Geometry))
                        continue;
                    lock (aprox.Buildings)
                    {
                        aprox.Buildings.Add(building);
                    }
                    return;
                }
            });

            foreach (var area in ProcessingAreas.ToArray())
            {
                if (area.Buildings.Count < 1000)
                {
                    continue;
                }
                ProcessingAreas.Remove(area);
                foreach (var aprox in SettlementsIndex.Query(area.Geometry.EnvelopeInternal))
                {
                    var intersection = aprox.Geometry.Intersection(area.Geometry);
                    if (intersection.Area > 0)
                    {
                        var newArea = new ProcessingArea(
                            intersection,
                            $"{area.Name}-{aprox.Id}".Replace("/", "_"),
                            new List<BuildingInfo>());
                        ProcessingAreas.Add(newArea);

                        foreach (var building in area.Buildings)
                        {
                            if (building.Geometry.Intersects(newArea.Geometry))
                            {
                                newArea.Buildings.Add(building);
                            }
                        }
                    }
                }
            }
#elif SQUARE_BLOCKS
            int id = 1;
            const int blockSizeX = 10;
            const int blockSizeY = 5;
            const int startX = 13300;
            const int endX = 16600;
            const int startY = 45400;
            const int endY = 46900;
            const double scale = 1000;
            const int MAX_BUILDINGS_PER_AREA = 1000;
            ProcessingArea?[,] processingAreas = new ProcessingArea[(endX - startX) / blockSizeX, (endY - startY) / blockSizeY];
            for (int x = startX; x < endX; x += blockSizeX)
            {
                for (int y = startY; y < endY; y += blockSizeY)
                {
                    var newArea = new ProcessingArea(
                        D96Factory.ToGeometry(new Envelope(x / scale, (x + blockSizeX) / scale, y / scale, (y + blockSizeY) / scale)),
                        id++.ToString(),
                        new List<BuildingInfo>());
                    processingAreasIndex.Insert(newArea.Geometry.EnvelopeInternal, newArea);
                    processingAreas[(x - startX) / blockSizeX, (y - startY) / blockSizeY] = newArea;
                }
            }
            processingAreasIndex.Build();
            Parallel.ForEach(Buildings, (building) =>
            {
                foreach (var aprox in processingAreasIndex.Query(building.Geometry.EnvelopeInternal))
                {
                    if (!building.Geometry.Intersects(aprox.Geometry))
                        continue;
                    lock (aprox.Buildings)
                    {
                        aprox.Buildings.Add(building);
                    }
                    return;
                }
            });


            var xLength = processingAreas.GetLength(0);
            var yLength = processingAreas.GetLength(1);

            // This loop is merging 4 squares into 1 square as long as number of buildings is less than MAX_BUILDINGS_PER_AREA
            for (int p = 2; p < 8; p *= 2)
            {
                for (int i = 0; i < xLength; i += p)
                {
                    for (int j = 0; j < yLength; j += p)
                    {
                        var maxN = Math.Min(xLength, i + p);
                        var maxM = Math.Min(yLength, j + p);
                        var buildings = new List<BuildingInfo>();
                        var envolope = processingAreas[i, j]!.Geometry.EnvelopeInternal;
                        for (int n = i; n < maxN; n++)
                        {
                            for (int m = j; m < maxM; m++)
                            {
                                if (processingAreas[n, m] is ProcessingArea area)
                                {
                                    buildings.AddRange(area.Buildings);
                                    envolope.ExpandToInclude(area.Geometry.EnvelopeInternal);
                                }
                            }
                        }
                        buildings = new List<BuildingInfo>(buildings.Distinct());
                        if (buildings.Count < MAX_BUILDINGS_PER_AREA)
                        {
                            var nameOf1st = processingAreas[i, j]!.Name;
                            for (int n = i; n < maxN; n++)
                            {
                                for (int m = j; m < maxM; m++)
                                {
                                    processingAreas[n, m] = null;
                                }
                            }
                            processingAreas[i, j] = new ProcessingArea(
                                D96Factory.ToGeometry(envolope),
                                nameOf1st,
                                buildings);
                        }
                    }
                }
            }

            for (int i = 0; i < xLength; i++)
            {
                for (int j = 0; j < yLength; j++)
                {
                    if (processingAreas[i, j] is ProcessingArea area && area.Buildings.Count > 0)
                    {
                        ProcessingAreas.Add(area);
                    }
                }
            }
#endif


            //foreach (var abc in ProcessingAreas.OrderBy(p => p.Buildings.Count))
            //{
            //    Console.WriteLine(abc.Name + " " + abc.Buildings.Count);
            //}
            //Console.WriteLine();
        }

        private void LoadOverrides(string overridesDir)
        {
            foreach (var file in Directory.GetFiles(overridesDir))
            {
                using var csv = Sylvan.Data.Csv.CsvDataReader.Create(file);
                var dict = Overrides[Path.GetFileNameWithoutExtension(file)] = new();
                while (csv.Read())
                {
                    dict.Add(csv.GetString(0), csv.GetString(1));
                }
            }
        }

        void LoadStreets(string dir)
        {
            var dict = Overrides["UL_UIME"];
            var dictBi = Overrides["UL_DJ"];
            using var csv = Sylvan.Data.Csv.CsvDataReader.Create(Path.Combine(dir, "UL_VSE", "UL_VSE.csv"));
            while (csv.Read())
            {
                if (csv.GetInt32(2) == 0)
                    continue;
                Streets.Add(
                    csv.GetInt32(1),
                    new BilingualName(
                        OverrideString(dict, csv.GetString(3)),
                        OverrideString(dictBi, csv.GetString(4))));
            }
        }

        void LoadSettlements(string dir)
        {
            var dict = Overrides["NA_UIME"];
            using var shapeReader = new ShapefileDataReader(Path.Combine(dir, "NA", "NA.shp"), D96Factory);
            while (shapeReader.Read())
            {
                shapeReader.Geometry.Apply(D96Converter.Instance);
                var id = shapeReader.GetInt32(2);
                SettlementsIndex.Insert(shapeReader.Geometry.EnvelopeInternal, (id, shapeReader.Geometry));
                Settlements.Add(id,
                    new SettlementInfo(
                        shapeReader.Geometry,
                        new BilingualName(
                            OverrideString(dict, shapeReader.GetString(4)),
                            shapeReader.GetString(5))));
            }
            SettlementsIndex.Build();
        }

        void LoadPosts(string dir)
        {
            using var shapeReader = new ShapefileDataReader(Path.Combine(dir, "PT", "PT.shp"), D96Factory);
            var dict = Overrides["PT_UIME"];
            while (shapeReader.Read())
            {
                shapeReader.Geometry.Apply(D96Converter.Instance);
                Posts.Add(shapeReader.GetInt32(2),
                    new PostInfo(
                        shapeReader.GetInt16(3),
                        OverrideString(dict, shapeReader.GetString(4))));
            }
        }

        private static string OverrideString(Dictionary<string, string> dict, string original)
        {
            if (dict.TryGetValue(original, out var overriden))
                return overriden;
            return original;
        }

        void LoadAddresses(string dir)
        {
            using var shapeReader = new ShapefileDataReader(Path.Combine(dir, "HS", "HS.shp"), D96Factory);
            while (shapeReader.Read())
            {
                var status = shapeReader.GetString(13);
                if (status != "V")
                    continue;

                shapeReader.Geometry.Apply(D96Converter.Instance);
                int id = shapeReader.GetInt32(2);
                var date = shapeReader.GetDateTime(11);
                BilingualName settlementName = Settlements[shapeReader.GetInt32(7)].Name;
                Address addr = new Address(
                                    id,
                                    shapeReader.Geometry,
                                    date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                                    shapeReader.GetString(5).ToLower(),
                                    Streets.TryGetValue(shapeReader.GetInt32(6), out var street) ? street : settlementName,
                                    Posts[shapeReader.GetInt32(9)],
                                    settlementName
                                );
                Addresses.Add(id, addr);
            }
        }

        void LoadVotingAreas(string dir)
        {
            using var shapeReader = new ShapefileDataReader(Path.Combine(dir, "VLV", "VLV.shp"), D96Factory);
            while (shapeReader.Read())
            {
                if (shapeReader.GetString(1) != "LV")
                    continue;
                shapeReader.Geometry.Apply(D96Converter.Instance);
                VotingArea votingArea = new VotingArea(
                                    shapeReader.Geometry,
                                    shapeReader.GetString(4),
                                    shapeReader.GetString(3));
                VotingAreas.Add(votingArea);
                VotingAreasIndex.Insert(votingArea.Geometry.EnvelopeInternal, votingArea);
            }
        }

        void LoadBuildings(string dir)
        {
            var buildingToAddresses = new Dictionary<long, List<Address>>();
            using var csvAddresses = Sylvan.Data.Csv.CsvDataReader.Create(Directory.GetFiles(Path.Combine(dir, "KS_SLO_CSV_A_U"), "KS_SLO_KHS_*.csv").Single());
            while (csvAddresses.Read())
            {
                var sta_sid = csvAddresses.GetInt64(0);
                var hs_mid = csvAddresses.GetInt32(1);
                try
                {
                    if (buildingToAddresses.TryGetValue(sta_sid, out var list))
                        list.Add(Addresses[hs_mid]);
                    else
                        buildingToAddresses.Add(sta_sid, new List<Address>() { Addresses[hs_mid] });

                }
                catch (KeyNotFoundException)
                {
                    Console.WriteLine(
                        $"GURS has out of sync files?" +
                        $"Building with STA_SID:'{sta_sid}' is referencing non-existing address with HS_MID:'{hs_mid}'");
                }
            }

            // Just date for now...
            var buildingToInfo = new Dictionary<long, string?>();
            using var csvInfo = Sylvan.Data.Csv.CsvDataReader.Create(Directory.GetFiles(Path.Combine(dir, "KS_SLO_CSV_A_U"), "KS_SLO_KST_*.csv").Single());
            while (csvInfo.Read())
            {
                var sta_sid = csvInfo.GetInt64(0);
                string date = csvInfo.GetString(12);
                if (date == "")
                    buildingToInfo[sta_sid] = null;
                else
                    buildingToInfo[sta_sid] = $"{date.Substring(6, 4)}-{date.Substring(3, 2)}-{date.Substring(0, 2)}";
            }
            var shapeReader = new ShapefileDataReader(Directory.GetFiles(Path.Combine(dir, "KS_SLO_SHP_G"), "KS_SLO_TLORISI_*.shp").Single(), D96Factory);
            while (shapeReader.Read())
            {
                shapeReader.Geometry.Apply(D96Converter.Instance);
                var id = shapeReader.GetInt32(1);

                if (!buildingToAddresses.TryGetValue(id, out var addresses))
                    addresses = null;
                Buildings.Add(new BuildingInfo(
                    id,
                    shapeReader.Geometry,
                    buildingToInfo[id],
                    addresses));
            }
        }
    }
}