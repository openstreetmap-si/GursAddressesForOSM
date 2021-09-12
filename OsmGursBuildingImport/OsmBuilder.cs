using System;
using System.Collections.Generic;
using System.Linq;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using OsmSharp;
using OsmSharp.Complete;
using OsmSharp.Tags;

namespace OsmGursBuildingImport
{
    class OsmBuilder
    {
        int newIdCounter = -1;
        List<ICompleteOsmGeo> geos = new();
        Dictionary<Coordinate, Node> ExistingNodes = new();


        private ICompleteOsmGeo LineStringToWay(LineString lineString)
        {
            var nodes = new List<Node>();
            foreach (var coord in lineString.Coordinates)
            {
                nodes.Add((Node)GeometryToOsmGeo(new Point(coord)));
            }

            return Add(new CompleteWay
            {
                Id = newIdCounter--,
                Nodes = nodes.ToArray()
            });
        }

        public bool UpdateBuilding(ICompleteOsmGeo building, BuildingInfo gursBuilding)
        {
            var attributes = building.Tags;
            attributes["ref:gurs:sta_sid"] = gursBuilding.Id.ToString();
            var addresses = gursBuilding.Addresses;
            if (addresses != null)
            {
                if (addresses.Count > 1)
                {
                    foreach (var addr in addresses)
                    {
                        var newNode = GetOrCreateNode(addr.Geometry.Centroid);
                        newNode.Tags = new TagsCollection();
                        SetAttributes(addr, newNode.Tags, gursBuilding.Geometry.Coordinate);
                    }
                }
                else
                {
                    SetAttributes(addresses[0], attributes, gursBuilding.Geometry.Coordinate);
                }
                return true;
            }

            return false;
        }


        private static IEnumerable<ICompleteOsmGeo> GetNodes(ICompleteOsmGeo building)
        {
            switch (building)
            {
                case Node node:
                    yield return node;
                    break;
                case CompleteWay way:
                    foreach (var node in way.Nodes)
                    {
                        yield return node;
                    }
                    break;
                case CompleteRelation relation:
                    foreach (var member in relation.Members)
                    {
                        foreach (var item in GetNodes(member.Member))
                        {
                            yield return item;
                        }
                    }
                    break;
            }
        }

        private static bool UpdateAttribute(TagsCollectionBase attributes, string attributeName, string newValue)
        {
            if (attributes.TryGetValue(attributeName, out var oldValue))
            {
                if (oldValue == newValue)
                {
                    return false;
                }

                attributes[attributeName] = newValue;
                AddFixmeAttribute(attributes,
                    $"\"{attributeName}\" changed from {attributes[attributeName]} to {newValue}.");
                return true;
            }
            else
            {
                attributes[attributeName] = newValue;
                return true;
            }
        }

        private static void AddFixmeAttribute(TagsCollectionBase attributes, string fixmeMessage)
        {
            if (attributes.ContainsKey("fixme"))
            {
                for (int i = 1; i < 100; i++)
                {
                    if (!attributes.ContainsKey("fixme" + i))
                    {

                        attributes["fixme" + i] = fixmeMessage;
                        return;
                    }
                }
                throw new Exception("What is happening? More than 100 fixmes...");
            }

            attributes["fixme"] = fixmeMessage;
        }

        private static string Suffix(Coordinate coordinate)
        {
            return coordinate.X > 14.815333 ? ":hu" : ":it";
        }

        private static void SetAttributes(Address address, TagsCollectionBase attributes, Coordinate coordinate)
        {
            var anythingWasSet = false;
            anythingWasSet |= UpdateAttribute(attributes, "addr:housenumber", address.HouseNumber);
            anythingWasSet |= UpdateAttribute(attributes, "addr:street", address.StreetName.Name);
            if (!string.IsNullOrEmpty(address.StreetName.NameSecondLanguage))
            {
                anythingWasSet |= UpdateAttribute(attributes, "addr:street" + Suffix(coordinate), address.StreetName.NameSecondLanguage);
            }
            anythingWasSet |= UpdateAttribute(attributes, "addr:postcode", address.PostInfo.Id.ToString());
            anythingWasSet |= UpdateAttribute(attributes, "addr:city", address.PostInfo.Name);

            // We want to add village only when it's not already mentioned, so when user enters
            // some address into navigation it re-assuress them when seeing also correct village name...
            // It is pretty common in Slovenia for people to be more fimiliar with village name than street names.
            if (!address.PostInfo.Name.StartsWith(address.VillageName.Name) &&
                address.StreetName.Name != address.VillageName.Name)
            {
                anythingWasSet |= UpdateAttribute(attributes, "addr:village", address.VillageName.Name);
                if (!string.IsNullOrEmpty(address.StreetName.NameSecondLanguage))
                {
                    anythingWasSet |= UpdateAttribute(attributes, "addr:village" + Suffix(coordinate), address.VillageName.NameSecondLanguage);
                }
            }

            // If only thing GURS is contributing is ID...
            // lets not state it is as source, because someone else
            // already got all relavant data elsewhere...
            UpdateAttribute(attributes, "ref:gurs:hs_mid", address.Id.ToString());
            if (anythingWasSet)
            {
                UpdateAttribute(attributes, "source:addr", "GURS");
                if (!string.IsNullOrEmpty(address.Date))
                    UpdateAttribute(attributes, "source:addr:date", address.Date);
            }
        }

        internal void AddBuilding(BuildingInfo gursBuilding)
        {
            var newBuilding = GeometryToOsmGeo(gursBuilding.Geometry);
            newBuilding.Tags = new TagsCollection(
                new Tag("building", "yes"),
                new Tag("source:geometry", "GURS"));

            if (!string.IsNullOrEmpty(gursBuilding.Date))
                newBuilding.Tags.Add(new Tag("source:geometry:date", gursBuilding.Date));
            UpdateBuilding(newBuilding, gursBuilding);
        }

        public ICompleteOsmGeo GeometryToOsmGeo(Geometry geometry)
        {
            switch (geometry)
            {
                case Point point:
                    return GetOrCreateNode(point);
                case Polygon polygon:
                    {
                        if (polygon.NumInteriorRings == 0)
                        {
                            return LineStringToWay(polygon.Shell);
                        }
                        else
                        {
                            var members = new List<CompleteRelationMember>();
                            var outerWay = LineStringToWay(polygon.Shell);
                            members.Add(new CompleteRelationMember()
                            {
                                Member = outerWay,
                                Role = "outer"
                            });
                            foreach (var pol in polygon.InteriorRings)
                            {
                                var osmGeo = LineStringToWay(pol);
                                members.Add(new CompleteRelationMember
                                {
                                    Member = osmGeo,
                                    Role = "inner"
                                });
                            }

                            return Add(new CompleteRelation()
                            {
                                Id = newIdCounter--,
                                Members = members.ToArray()
                            });
                        }
                    }
                case MultiPolygon multiPolygon:
                    {
                        var members = new List<CompleteRelationMember>();
                        foreach (var pol in multiPolygon.Geometries)
                        {
                            var osmGeo = GeometryToOsmGeo(pol);
                            members.Add(new CompleteRelationMember()
                            {
                                Member = osmGeo
                            });
                        }

                        return Add(new CompleteRelation()
                        {
                            Id = newIdCounter--,
                            Members = members.ToArray()
                        });
                    }
                default:
                    throw new NotImplementedException(geometry.GetType().FullName);
            }
        }

        public Node GetOrCreateNode(Point point)
        {
            if (ExistingNodes.TryGetValue(point.Coordinate, out var node))
                return node;
            var newNode = new Node()
            {
                Id = newIdCounter--,
                Longitude = point.X,
                Latitude = point.Y
            };
            ExistingNodes.Add(point.Coordinate, newNode);
            return Add(newNode);
        }

        private T Add<T>(T geo) where T : ICompleteOsmGeo
        {
            geos.Add(geo);
            return geo;
        }

        public IEnumerable<ICompleteOsmGeo> GetGeos()
        {
            return geos;
        }
    }
}