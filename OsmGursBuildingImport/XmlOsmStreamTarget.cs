﻿// The MIT License (MIT)

// Copyright (c) 2016 Ben Abelshausen

// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:

// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using CsvHelper;
using OsmSharp;
using OsmSharp.IO.PBF;
using OsmSharp.IO.Xml;
using OsmSharp.Streams;
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using Node = OsmSharp.Node;
using Relation = OsmSharp.Relation;
using Way = OsmSharp.Way;

namespace OsmGursBuildingImport
{
    /// <summary>
    /// A stream target that writes OSM-XML.
    /// </summary>
    public class XmlOsmStreamTarget : OsmStreamTarget, IDisposable
    {
        private readonly XmlWriter _writer;
        private readonly bool _disposeStream = false;

        /// <summary>
        /// Creates a new stream target.
        /// </summary>
        public XmlOsmStreamTarget(Stream stream)
        {
            var settings = new XmlWriterSettings();
            settings.OmitXmlDeclaration = true;
            settings.Indent = true;
            settings.ConformanceLevel = ConformanceLevel.Fragment;

            _writer = XmlWriter.Create(stream, settings);
        }

        private bool _initialized = false;

        /// <summary>
        /// Gets or sets the generator.
        /// </summary>
        public string Generator { get; set; } = "OsmSharp";

        /// <summary>
        /// Gets or sets the bounds.
        /// </summary>
        public OsmSharp.API.Bounds Bounds { get; set; }

        /// <summary>
        /// Initializes this target.
        /// </summary>
        public override void Initialize()
        {
            if (!_initialized)
            {
                _writer.WriteRaw("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
                _writer.WriteStartElement("osm");
                _writer.WriteAttributeString("version", "0.6");
                if (string.IsNullOrWhiteSpace(this.Generator))
                {
                    _writer.WriteAttributeString("generator", "OsmSharp");
                }
                else
                {
                    _writer.WriteAttributeString("generator", this.Generator);
                }

                if (this.ExtraRootAttributes.Count > 0)
                {
                    foreach (var pair in this.ExtraRootAttributes)
                    {
                        if (string.IsNullOrWhiteSpace(pair.Item1) &&
                            string.IsNullOrWhiteSpace(pair.Item2))
                        {
                            continue;
                        }

                        _writer.WriteAttributeString(pair.Item1, pair.Item2);
                    }
                }

                if (this.Bounds != null)
                {
                    _writer.WriteRaw(this.Bounds.SerializeToXml());
                }

                _initialized = true;
            }
        }

        /// <summary>
        /// Gets or sets a list of extra root attributes.
        /// </summary>
        public List<Tuple<string, string>> ExtraRootAttributes { get; private set; } = new List<Tuple<string, string>>();

        /// <summary>
        /// Adds a node to the xml output stream.
        /// </summary>
        public override void AddNode(Node node)
        {
            _writer.WriteStartElement("node");
            if (node.Id > 0)
            {
                _writer.WriteAttribute("action", "modified");
            }
            _writer.WriteAttribute("id", node.Id);
            _writer.WriteAttribute("lat", node.Latitude);
            _writer.WriteAttribute("lon", node.Longitude);
            _writer.WriteAttribute("user", node.UserName);
            _writer.WriteAttribute("uid", node.UserId);
            _writer.WriteAttribute("visible", node.Visible);
            _writer.WriteAttribute("version", node.Version);
            _writer.WriteAttribute("changeset", node.ChangeSetId);
            _writer.WriteAttribute("timestamp", node.TimeStamp);

            if (node.Tags != null)
            {
                foreach (var tag in node.Tags)
                {
                    _writer.WriteStartElement("tag");
                    _writer.WriteAttributeString("k", tag.Key);
                    _writer.WriteAttributeString("v", tag.Value);
                    _writer.WriteEndElement();
                }
            }
            _writer.WriteEndElement();
        }

        /// <summary>
        /// Adds a way to this target.
        /// </summary>
        public override void AddWay(Way way)
        {
            _writer.WriteStartElement("way");
            if (way.Id > 0)
            {
                _writer.WriteAttribute("action", "modified");
            }
            _writer.WriteAttribute("id", way.Id);
            _writer.WriteAttribute("user", way.UserName);
            _writer.WriteAttribute("uid", way.UserId);
            _writer.WriteAttribute("visible", way.Visible);
            _writer.WriteAttribute("version", way.Version);
            _writer.WriteAttribute("changeset", way.ChangeSetId);
            _writer.WriteAttribute("timestamp", way.TimeStamp);

            if (way.Nodes != null)
            {
                for (var i = 0; i < way.Nodes.Length; i++)
                {
                    _writer.WriteStartElement("nd");
                    _writer.WriteAttribute("ref", way.Nodes[i]);
                    _writer.WriteEndElement();
                }
            }

            if (way.Tags != null)
            {
                foreach (var tag in way.Tags)
                {
                    _writer.WriteStartElement("tag");
                    _writer.WriteAttributeString("k", tag.Key);
                    _writer.WriteAttributeString("v", tag.Value);
                    _writer.WriteEndElement();
                }
            }
            _writer.WriteEndElement();
        }

        /// <summary>
        /// Adds a relation to this target.
        /// </summary>
        public override void AddRelation(Relation relation)
        {
            _writer.WriteStartElement("relation");
            if (relation.Id > 0)
            {
                _writer.WriteAttribute("action", "modified");
            }
            _writer.WriteAttribute("id", relation.Id);
            _writer.WriteAttribute("user", relation.UserName);
            _writer.WriteAttribute("uid", relation.UserId);
            _writer.WriteAttribute("visible", relation.Visible);
            _writer.WriteAttribute("version", relation.Version);
            _writer.WriteAttribute("changeset", relation.ChangeSetId);
            _writer.WriteAttribute("timestamp", relation.TimeStamp);

            if (relation.Members != null)
            {
                for (var i = 0; i < relation.Members.Length; i++)
                {
                    _writer.WriteStartElement("member");
                    (relation.Members[i] as IXmlSerializable).WriteXml(_writer);
                    _writer.WriteEndElement();
                }
            }

            if (relation.Tags != null)
            {
                foreach (var tag in relation.Tags)
                {
                    _writer.WriteStartElement("tag");
                    _writer.WriteAttributeString("k", tag.Key);
                    _writer.WriteAttributeString("v", tag.Value);
                    _writer.WriteEndElement();
                }
            }
            _writer.WriteEndElement();
        }

        private bool _closed = false;

        /// <summary>
        /// Closes this target.
        /// </summary>
        public override void Close()
        {
            base.Close();

            if (!_closed)
            {
                _writer.WriteRaw("</osm>");
                _writer.Flush();
                _closed = true;
            }
        }

        /// <summary>
        /// Disposes of all resource associated with this stream target.
        /// </summary>
        public void Dispose()
        {
            if (_disposeStream)
            {
#if !NET40
                _writer.Dispose();
#endif
            }
        }
    }
}