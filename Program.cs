using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using System.Text;

namespace hkxcnv
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                System.Console.Error.WriteLine("Usage: hktcnv [source] [dest]");
                System.Console.Error.WriteLine("       Converts a Havok Tag XML file from 2011.3 to 2010.2 format to be converted by hkxcmd");
                return;
            }
            string infilename = args[0];
            if (!System.IO.File.Exists(infilename))
            {
                System.Console.Error.WriteLine("File '{0}' does not appear to exist", infilename);
                return;
            }
            string outfilename = args.Length > 1 ? args[1] : null;
            if (string.IsNullOrEmpty(outfilename))
            {
                outfilename = String.Format("{0}-out{1}", Path.Combine(Path.GetDirectoryName(infilename), Path.GetFileNameWithoutExtension(infilename)), Path.GetExtension(infilename));
            }
            var settings = new XmlReaderSettings();
            settings.IgnoreComments = false;
            settings.CloseInput = false;
            settings.IgnoreWhitespace = true;

            System.Xml.Linq.XDocument doc = null;
            using (var stream = System.IO.File.OpenRead(args[0]))
            using (var reader = System.Xml.XmlReader.Create(stream, settings))
            {
                doc = System.Xml.Linq.XDocument.Load(reader, System.Xml.Linq.LoadOptions.None);
            }

            if (doc != null)
            {
                var root = doc.Element("hktagfile");
                if (root == null)
                {
                    System.Console.Error.WriteLine("'{0}' does not appear to be a valid Havok Tag Xml file.", infilename);
                }


                // find max object id
                int maxObjectId =
                    (from o in root.Elements("object")
                    let attr = o.Attribute("id")
                    where attr != null && attr.Value.StartsWith("#")
                    select int.Parse(attr.Value.Substring(1))).Max();

                var mappings = new Dictionary<string, string>
                {
                    {"hkRootLevelContainerNamedVariant", "0"},
                    {"hkaAnimation", "1"},
                    {"hkaBoneAttachment", "1"},
                    {"hkaMeshBinding", "1"},                    
                    {"hkxMeshSection", "1"},
                    {"hkxAttributeHolder", "1"},                    
                    {"hkxAttribute", "0"},                    
                    {"hkxMaterial", "1"},
                    {"hkxMaterialTextureStage", "0"},
                };

                var removeMember = new Dictionary<string, string>
                {
                    {"hkaMeshBinding", "name"},
                };

                var structToRefMember = new Dictionary<string, string>
                {
                    {"hkaMeshBinding", "mappings"},
                    {"hkaAnimation", "annotationTracks"},
                    {"hkxAttributeHolder", "attributeGroups"},
                };

                // set version
                foreach (var kvp in mappings)
                {
                    foreach (var e in root.Elements("class").Where(x => x.Attribute("name").Value == kvp.Key))
                        e.Attribute("version").SetValue(kvp.Value);
                }

                // drop  members
                foreach (var kvp in removeMember)
                {
                    foreach (var e in root.Elements("class").Where(x => x.Attribute("name").Value == kvp.Key))
                    {
                        foreach (var m in from m in e.Elements("member") where m.Attribute("name").Value == kvp.Value select m)
                            m.Remove();
                    }
                }

                foreach (var kvp in structToRefMember)
                {
                    foreach (var e in root.Elements("class").Where(x => x.Attribute("name").Value == kvp.Key))
                    {
                        foreach (var m in from m in e.Elements("member")
                                          where m.Attribute("name").Value == kvp.Value && m.Attribute("type").Value == "struct"
                                          select m)
                            m.Attribute("type").SetValue("ref");
                    }

                    // for each annotation track in animations
                    var classNames = new List<string>();
                    classNames.Add(kvp.Key);
                    while (true)
                    {
                        var classes = from o in root.Elements("class")
                                          let p = o.Attribute("parent")
                                          let n = o.Attribute("name").Value
                                          where p != null && !classNames.Contains(n) && classNames.Contains(p.Value)
                                          select n;
                        if (!classes.Any())
                            break;
                        classNames.AddRange(classes);
                    };
                    classNames.Remove(kvp.Key);

                    foreach (var c in from o in root.Elements("object")
                                      where classNames.Contains(o.Attribute("type").Value)
                                      from m in o.Elements("array")
                                      where m.Attribute("name").Value == "annotationTracks"
                                      select m)
                    {

                        foreach (var e in c.Elements("struct").ToArray())
                        {
                            e.Remove();
                            e.RemoveAttributes();
                            e.Name = "object";
                            var objID = string.Format("#{0:D4}", ++maxObjectId);
                            e.SetAttributeValue("id", objID);
                            e.SetAttributeValue("type", "hkaAnnotationTrack");
                            c.Add(new XElement("ref", objID));
                            root.Add(e);
                        }
                    }
                }

                var writeSettings = new XmlWriterSettings();
                writeSettings.Indent = true;
                writeSettings.CloseOutput = false;
                writeSettings.NewLineOnAttributes = false;
                writeSettings.Encoding = System.Text.Encoding.ASCII;
                using (var outfile = System.IO.File.Create(outfilename))
                using (var xmlwriter = XmlWriter.Create(outfile, writeSettings))
                {
                    doc.WriteTo(xmlwriter);
                }
            }
        }
    }
}
