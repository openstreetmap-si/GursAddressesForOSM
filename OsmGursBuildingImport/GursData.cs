using System;
using System.Collections.Generic;
using System.Data;
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
    record PostInfo(short Id, BilingualName Name);
    record VotingArea(Geometry Geometry, string Name, string Id);
    record BuildingInfo(long Id, Geometry Geometry, string? Date, List<Address>? Addresses, int? ConstructionYear);
    record Address(long Id, Geometry Geometry, string Date, string HouseNumber, BilingualName StreetName, PostInfo PostInfo, BilingualName VillageName);
    record ProcessingArea(Geometry Geometry, string Name, List<BuildingInfo> Buildings, string pathToPoly)
    {
        public bool Process { get; set; }
    }

    class GursData
    {
        private static GeometryFactory D96Factory = NtsGeometryServices.Instance.CreateGeometryFactory(new PrecisionModel(), 3794);
        public Dictionary<int, ProcessingArea> ProcessingAreas = new();

        Dictionary<long, Address> Addresses = new();
        List<VotingArea> VotingAreas = new();
        Dictionary<string, Dictionary<string, string>> Overrides = new();
        STRtree<BuildingInfo> BuildingsIndex = new();
        Dictionary<long, List<Address>> BuildingToAddresses = new();

        public GursData(string dir, string overridesDir, string tempDir)
        {
            LoadOverrides(overridesDir);
            LoadAddresses(dir);
            LoadBuildings(dir);
            LoadVotingAreasGeoJson();

            BuildProcessingAreas(tempDir);
        }

        private static string WritePoly(string poliesDir, Geometry geometry, string id)
        {
            List<Polygon> polygons;
            if (geometry is MultiPolygon mp)
            {
                polygons = mp.Geometries.OfType<Polygon>().ToList();
                if (polygons.Count != mp.Geometries.Length)
                    throw new Exception(string.Join(", ", mp.Geometries.Select(g => g.GetType().ToString())));
            }
            else if (geometry is Polygon poly2)
            {
                polygons = new() { poly2 };
            }
            else
            {
                throw new Exception(geometry.GetType().ToString());
            }

            var fixedPolygons = new List<Polygon>();
            foreach (var poly in polygons)
            {
                var fixedPoly = GeometryFixer.Fix(poly);
                if (fixedPoly is Polygon poly2)
                {
                    fixedPolygons.Add(poly2);
                }
                else if (fixedPoly is MultiPolygon multiPolygon)
                {
                    foreach (var poly3 in multiPolygon.Geometries.OfType<Polygon>())
                    {
                        fixedPolygons.Add(poly3);
                    }
                }
                else
                {
                    throw new NotImplementedException(fixedPoly.GetType().ToString());
                }
            }

            string polyPath = Path.Combine(poliesDir, id + ".poly");
            using var sw = new StreamWriter(polyPath);
            sw.WriteLine(id + ".original");
            for (int i = 0; i < fixedPolygons.Count; i++)
            {
                sw.WriteLine("poly" + (i + 1));
                foreach (var cord in fixedPolygons[i].ExteriorRing.Coordinates)
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

            Parallel.ForEach(ProcessingAreas.Values, (area) => {
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

        private static string OverrideString(Dictionary<string, string> dict, string original)
        {
            if (dict.TryGetValue(original, out var overriden))
                return overriden;
            return original;
        }

        void LoadAddresses(string dir)
        {
            var streetNameOverride = Overrides["UL_UIME"];
            var streetNameSecondaryLanguageOverride = Overrides["UL_DJ"];
            var settlementNameOverride = Overrides["NA_UIME"];
            var postalNameOverride = Overrides["PT_UIME"];

            using var csvAddresses = Sylvan.Data.Csv.CsvDataReader.Create(Directory.GetFiles(Path.Combine(dir, "Addresses"), "KN_SLO_NASLOVI_HS_*.csv").Single());
            var wktReader = new WKTReader(D96Factory.GeometryServices);
            while (csvAddresses.Read())
            {
                var buildingId = ConvertToOldGursBuildingId(csvAddresses.GetInt64("EID_STAVBA"));
                var id = ConvertToOldGursAddressId(csvAddresses.GetInt64("EID_HISNA_STEVILKA"));
                var geom = wktReader.Read(csvAddresses.GetString("GEOM"));
                geom.Apply(D96Converter.Instance);//Convert from D96 to OSM coordinate system
                var houseNumber = csvAddresses.GetString("HS_STEVILKA") + csvAddresses.GetString("HS_DODATEK");
                var streetName = new BilingualName(OverrideString(streetNameOverride, csvAddresses.GetString("ULICA_NAZIV")), OverrideString(streetNameSecondaryLanguageOverride, csvAddresses.GetString("ULICA_NAZIV_DJ")));
                var postInfo = new PostInfo(csvAddresses.GetInt16("POSTNI_OKOLIS_SIFRA"), new BilingualName(OverrideString(postalNameOverride, csvAddresses.GetString("POSTNI_OKOLIS_NAZIV")), csvAddresses.GetString("POSTNI_OKOLIS_NAZIV_DJ")));
                var villageName = new BilingualName(OverrideString(settlementNameOverride, csvAddresses.GetString("NASELJE_NAZIV")), csvAddresses.GetString("NASELJE_NAZIV_DJ"));
                var address = new Address(id, geom, null, houseNumber, streetName, postInfo, villageName);
                Addresses.Add(id, address);
                if (BuildingToAddresses.TryGetValue(buildingId, out var list))
                    list.Add(address);
                else
                    BuildingToAddresses.Add(buildingId, new List<Address>() { address });
            }
        }

        static long ConvertToOldGursBuildingId(long newId)
        {
            newId /= 10;//Remove control value
            var result = newId % 1000_000_000_000;
            if (result + 10020000000000000 != newId)
                throw new Exception("Failed converting new GURS building EID to old sta_sid");
            return result;
        }

        static long ConvertToOldGursAddressId(long newId)
        {
            newId /= 10;//Remove control value
            var result = newId % 1000_000_000_000;
            if (result + 10040000000000000 != newId)
                throw new Exception("Failed converting new GURS building EID to old sta_sid");
            return result;
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

        void LoadBuildings(string dir)
        {
            var buildingsPolygons = new Dictionary<long, Geometry>();
            var shapeReader = new ShapefileDataReader(Path.Combine(dir, "Buildings", "KN_SLO_STAVBE_SLO_STAVBE_NADZEMNI_TLORIS", "KN_SLO_STAVBE_SLO_STAVBE_NADZEMNI_TLORIS_poligon.shp"), D96Factory);
            while (shapeReader.Read())
            {
                shapeReader.Geometry.Apply(D96Converter.Instance);
                var id = ConvertToOldGursBuildingId(shapeReader.GetInt64(2));
                buildingsPolygons.Add(id, shapeReader.Geometry);
            }

            shapeReader = new ShapefileDataReader(Path.Combine(dir, "Buildings", "KN_SLO_STAVBE_SLO_STAVBE_TLORIS", "KN_SLO_STAVBE_SLO_STAVBE_TLORIS_poligon.shp"), D96Factory);
            while (shapeReader.Read())
            {
                shapeReader.Geometry.Apply(D96Converter.Instance);
                var id = ConvertToOldGursBuildingId(shapeReader.GetInt64(2));
                if (buildingsPolygons.ContainsKey(id))
                    continue;
                buildingsPolygons.Add(id, shapeReader.Geometry);
            }
            shapeReader = new ShapefileDataReader(Path.Combine(dir, "Buildings", "KN_SLO_STAVBE_SLO_STAVBE", "KN_SLO_STAVBE_SLO_STAVBE_tocka.shp"), D96Factory);
            while (shapeReader.Read())
            {
                var id = ConvertToOldGursBuildingId(shapeReader.GetInt64(2));

                if (!buildingsPolygons.TryGetValue(id, out var geometry))
                {
                    shapeReader.Geometry.Apply(D96Converter.Instance);
                    //Console.WriteLine($"Building with id {id} at {shapeReader.Geometry}, does not have tloris polygon!");
                    continue;
                }

                if (!BuildingToAddresses.TryGetValue(id, out var addresses))
                    addresses = null;

                var yearOfConstruction = shapeReader["LETO_IZGRA"] switch {
                    double val => (int)val,
                    _ => (int?)null
                };

                if (yearOfConstruction > 2050 || yearOfConstruction < 1000)
                {
                    Console.WriteLine($"Year of construction outside range. {yearOfConstruction}");
                    yearOfConstruction = null;
                }
                BuildingsIndex.Insert(geometry.EnvelopeInternal, new BuildingInfo(
                    id,
                    geometry,
                    null,
                    addresses,
                    yearOfConstruction
                    ));
            }
            BuildingsIndex.Build();
        }
    }
}