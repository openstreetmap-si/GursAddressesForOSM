using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading.Tasks;
using NetTopologySuite;
using NetTopologySuite.Algorithm;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Utilities;
using NetTopologySuite.Index.Strtree;
using NetTopologySuite.IO;
using Newtonsoft.Json;

namespace OsmGursBuildingImport
{
    record BilingualName(string Name, string NameSecondLanguage);
    record PostInfo(short Id, string Name);// Posts that are in bilingual area have "Koper - Capodistria" format
    record VotingArea(Geometry Geometry, string Name, string Id);
    record BuildingInfo(int Id, Geometry Geometry, string? Date, List<Address>? Addresses);
    record SettlementInfo(Geometry Geometry, BilingualName Name);
    record Address(int Id, Geometry Geometry, string Date, string HouseNumber, BilingualName StreetName, PostInfo PostInfo, BilingualName VillageName);
    record ProcessingArea(Geometry Geometry, string Name, List<BuildingInfo> Buildings, string pathToPoly)
    {
        public bool Process { get; set; }
    }

    class GursData
    {
        private static GeometryFactory D96Factory = NtsGeometryServices.Instance.CreateGeometryFactory(new PrecisionModel(), 3794);
        public Dictionary<int, ProcessingArea> ProcessingAreas = new();

        Dictionary<int, BilingualName> Streets = new();
        public Dictionary<int, SettlementInfo> Settlements = new();
        Dictionary<int, PostInfo> Posts = new();
        Dictionary<int, Address> Addresses = new();
        List<VotingArea> VotingAreas = new();
        Dictionary<string, Dictionary<string, string>> Overrides = new();
        STRtree<BuildingInfo> BuildingsIndex = new();

        public GursData(string dir, string overridesDir, string tempDir)
        {
            LoadOverrides(overridesDir);
            LoadStreets(dir);
            LoadSettlements(dir);
            LoadPosts(dir);
            LoadAddresses(dir);
            LoadBuildings(dir);
            //LoadVotingAreas(dir);
            LoadVotingAreasGeoJson();

            BuildProcessingAreas(tempDir);
        }

        private static string WritePoly(string poliesDir, Geometry geometry, string id)
        {
            Polygon[] polygons;
            if (geometry is MultiPolygon mp)
            {
                polygons = mp.Geometries.OfType<Polygon>().ToArray();
                if (polygons.Length != mp.Geometries.Length)
                    throw new Exception(string.Join(", ", mp.Geometries.Select(g => g.GetType().ToString())));
            }
            else if (geometry is Polygon poly2)
            {
                polygons = new[] { poly2 };
            }
            else
            {
                throw new Exception(geometry.GetType().ToString());
            }

            for (int i = 0; i < polygons.Length; i++)
            {
                polygons[i] = (Polygon)GeometryFixer.Fix(polygons[i]);
            }

            string polyPath = Path.Combine(poliesDir, id + ".poly");
            using var sw = new StreamWriter(polyPath);
            sw.WriteLine(id + ".original");
            for (int i = 0; i < polygons.Length; i++)
            {
                sw.WriteLine("poly" + (i + 1));
                foreach (var cord in polygons[i].ExteriorRing.Coordinates)
                {
                    sw.WriteLine($"\t{cord.X} {cord.Y}");
                }
                sw.WriteLine("END");
            }
            sw.WriteLine("END");
            return polyPath;
        }


        private void BuildProcessingAreas(string tempDir)
        {
            var poliesDir = Path.Combine(tempDir, "polygons");
            Directory.CreateDirectory(poliesDir);

            foreach (var votingArea in VotingAreas)
            {
                var newArea = new ProcessingArea(
                    votingArea.Geometry,
                    votingArea.Id,
                    new List<BuildingInfo>(),
                    WritePoly(poliesDir, votingArea.Geometry, votingArea.Id));
                ProcessingAreas.Add(int.Parse(votingArea.Id), newArea);
            }

            Parallel.ForEach(ProcessingAreas.Values, (area) =>
            {
                foreach (var aprox in BuildingsIndex.Query(area.Geometry.EnvelopeInternal))
                {
                    if (!area.Geometry.Intersects(aprox.Geometry))
                        continue;
                    area.Buildings.Add(aprox);
                }
            });
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
                Settlements.Add(shapeReader.GetInt32(2),
                    new SettlementInfo(
                        shapeReader.Geometry,
                        new BilingualName(
                            OverrideString(dict, shapeReader.GetString(4)),
                            shapeReader.GetString(5).Replace("\0", ""))));
            }
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

        void LoadVotingAreasGeoJson()
        {
            using var sr = new StreamReader("VLV.geojson");
            var reader = new GeoJsonReader();
            var features = reader.Read<FeatureCollection>(sr.ReadToEnd());
            foreach (var feature in features)
            {
                if (feature.Attributes["ENOTA"].ToString() != "LV")
                    continue;
                var id = feature.Attributes["VLV_ID"].ToString();
                var name = feature.Attributes["VLV_UIME"].ToString();
                var geometry = feature.Geometry;
                VotingAreas.Add(new VotingArea(geometry, name, id));
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
                BuildingsIndex.Insert(shapeReader.Geometry.EnvelopeInternal, new BuildingInfo(
                    id,
                    shapeReader.Geometry,
                    buildingToInfo[id],
                    addresses));
            }
            BuildingsIndex.Build();
        }
    }
}