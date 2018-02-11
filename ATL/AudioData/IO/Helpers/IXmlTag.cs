﻿using Commons;
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using static ATL.AudioData.IO.MetaDataIO;

namespace ATL.AudioData.IO
{
    public static class IXmlTag
    {
        public const string CHUNK_IXML = "iXML";

        private static string getPosition(IList<string> position)
        {
            string result = "";
            bool first = true;

            foreach (string s in position)
            {
                if (first)
                {
                    first = false;
                } else
                {
                    result += ".";
                }
                result += s;
            }

            return result;
        }

        public static void FromStream(Stream source, MetaDataIO meta, ReadTagParams readTagParams, uint chunkSize)
        {
            IList<string> position = new List<string>();
            bool inList = false;
            int listDepth = 0;
            int listCounter = 1;
            position.Add("ixml");

            using (MemoryStream mem = new MemoryStream((int)chunkSize))
            { 
                StreamUtils.CopyStream(source, mem, (int)chunkSize); // Isolate XML structure in a clean memory chunk
                mem.Seek(0, SeekOrigin.Begin);

                using (XmlReader reader = XmlReader.Create(mem))
                {
                    while (reader.Read())
                    {
                        switch (reader.NodeType)
                        {
                            case XmlNodeType.Element: // Element start
                                string key = reader.Name;
                                if (inList && reader.Depth == listDepth + 1 && !key.EndsWith("COUNT", StringComparison.OrdinalIgnoreCase))
                                {
                                    key = key + "[" + listCounter + "]";
                                    listCounter++;
                                }
                                if (!key.Equals("BWFXML", StringComparison.OrdinalIgnoreCase)) position.Add(key);
                                if (!inList && reader.Name.EndsWith("LIST", StringComparison.OrdinalIgnoreCase))
                                {
                                    inList = true;
                                    listDepth = reader.Depth;
                                    listCounter = 1;
                                }
                                break;

                            case XmlNodeType.Text:
                                if (reader.Value != null && reader.Value.Length > 0)
                                {
                                    meta.SetMetaField(getPosition(position), reader.Value, readTagParams.ReadAllMetaFrames);
                                }
                                break;

                            case XmlNodeType.EndElement: // Element end
                                position.RemoveAt(position.Count-1);
                                if (inList && reader.Name.EndsWith("LIST", StringComparison.OrdinalIgnoreCase))
                                {
                                    inList = false;
                                }
                                break;
                        }
                    }
                }
            }
        }

        public static bool IsDataEligible(MetaDataIO meta)
        {
            foreach (string key in meta.AdditionalFields.Keys)
            {
                if (key.StartsWith("ixml.")) return true;
            }

            return false;
        }

        public static int ToStream(BinaryWriter w, bool isLittleEndian, MetaDataIO meta)
        {
            IDictionary<string, string> additionalFields = meta.AdditionalFields;
            w.Write(Utils.Latin1Encoding.GetBytes(CHUNK_IXML));

            long sizePos = w.BaseStream.Position;
            w.Write((int)0); // Placeholder for chunk size that will be rewritten at the end of the method


            string shortKey;
            foreach(string key in additionalFields.Keys)
            {
                if (key.StartsWith("info."))
                {
                    shortKey = key.Substring(5, key.Length - 5);
//                    writeSizeAndNullTerminatedString(shortKey, additionalFields[key], w);
                }
            }


            long finalPos = w.BaseStream.Position;
            w.BaseStream.Seek(sizePos, SeekOrigin.Begin);
            if (isLittleEndian)
            {
                w.Write((int)(finalPos - sizePos - 4));
            }
            else
            {
                w.Write(StreamUtils.EncodeBEInt32((int)(finalPos - sizePos - 4)));
            }

            return 14;
        }

    }
}
