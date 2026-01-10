using NetTopologySuite.Geometries;
using ProjNet;
using ProjNet.CoordinateSystems;
using ProjNet.CoordinateSystems.Transformations;

namespace OsmGursBuildingImport
{
    internal sealed class D96Converter : ICoordinateSequenceFilter
    {
        public static D96Converter Instance { get; } = new D96Converter();

        private readonly MathTransform _mathTransform = new CoordinateSystemServices().CreateTransformation(new CoordinateSystemFactory().CreateFromWkt(@"PROJCS[""Slovenia 1996 / Slovene National Grid"",
    GEOGCS[""Slovenia 1996"",
        DATUM[""Slovenia_Geodetic_Datum_1996"",
            SPHEROID[""GRS 1980"",6378137,298.257222101,
                AUTHORITY[""EPSG"",""7019""]],
            TOWGS84[0,0,0,0,0,0,0],
            AUTHORITY[""EPSG"",""6765""]],
        PRIMEM[""Greenwich"",0,
            AUTHORITY[""EPSG"",""8901""]],
        UNIT[""degree"",0.01745329251994328,
            AUTHORITY[""EPSG"",""9122""]],
        AUTHORITY[""EPSG"",""4765""]],
    UNIT[""metre"",1,
        AUTHORITY[""EPSG"",""9001""]],
    PROJECTION[""Transverse_Mercator""],
    PARAMETER[""latitude_of_origin"",0],
    PARAMETER[""central_meridian"",15],
    PARAMETER[""scale_factor"",0.9999],
    PARAMETER[""false_easting"",500000],
    PARAMETER[""false_northing"",-5000000],
    AUTHORITY[""EPSG"",""3794""],
    AXIS[""Easting"",EAST],
    AXIS[""Northing"",NORTH]]"), GeographicCoordinateSystem.WGS84).MathTransform;

        public bool Done { get; } = false;
        public bool GeometryChanged { get; } = true;

        public void Filter(CoordinateSequence seq, int i)
        {
            var (x, y) = _mathTransform.Transform(seq.GetX(i), seq.GetY(i));
            seq.SetX(i, x);
            seq.SetY(i, y);
        }
    }
}