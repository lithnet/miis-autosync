using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Xml;

namespace Lithnet.Miiserver.AutoSync
{
    public static class Serializer
    {
        public static T Read<T>(string filename)
        {
            return Serializer.Read<T>(filename, null);
        }

        public static T Read<T>(string filename, IDataContractSurrogate surrogate)
        {
            T deserialized;

            using (FileStream stream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                XmlDictionaryReader xdr = XmlDictionaryReader.CreateTextReader(stream, new XmlDictionaryReaderQuotas() { MaxStringContentLength = 20971520 });
                deserialized = Serializer.Read<T>(xdr, surrogate);
                xdr.Close();
                stream.Close();
            }

            return deserialized;
        }

        public static T Read<T>(string filename, string nodeName, string nodeUri)
        {
            return Serializer.Read<T>(filename, nodeName, nodeUri, null);
        }

        public static T Read<T>(string filename, string nodeName, string nodeUri, IDataContractSurrogate surrogate)
        {
            T deserialized;

            using (FileStream stream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                XmlDictionaryReader xdr = XmlDictionaryReader.CreateTextReader(stream, new XmlDictionaryReaderQuotas() { MaxStringContentLength = 20971520 });
                xdr.ReadToFollowing(nodeName, nodeUri);
                deserialized = Serializer.Read<T>(xdr, surrogate);
                xdr.Close();
                stream.Close();
            }

            return deserialized;
        }

        public static T Read<T>(XmlDictionaryReader xdr)
        {
            return Serializer.Read<T>(xdr, null);
        }

        public static T Read<T>(XmlDictionaryReader xdr, IDataContractSurrogate surrogate)
        {
            DataContractSerializer serializer;

            if (surrogate == null)
            {
                serializer = new DataContractSerializer(typeof(T));
            }
            else
            {
                serializer = new DataContractSerializer(typeof(T), null, int.MaxValue, false, false, surrogate);
            }

            return (T)serializer.ReadObject(xdr);
        }

        public static void Save<T>(string filename, T obj)
        {
            Dictionary<string, string> namespaces = new Dictionary<string, string>();
            namespaces.Add("a", "http://schemas.microsoft.com/2003/10/Serialization/Arrays");

            Serializer.Save<T>(filename, obj, namespaces, null);

        }

        public static void Save<T>(string filename, T obj, Dictionary<string, string> namespacePrefixes)
        {
            Serializer.Save<T>(filename, obj, namespacePrefixes, null);
        }

        public static void Save<T>(string filename, T obj, Dictionary<string, string> namespacePrefixes, IDataContractSurrogate surrogate)
        {
            if (string.IsNullOrWhiteSpace(filename))
            {
                throw new ArgumentNullException(nameof(filename));
            }

            if (obj == null)
            {
                throw new ArgumentNullException(nameof(obj));
            }

            using (FileStream stream = new FileStream(filename, FileMode.Create, FileAccess.ReadWrite))
            {
                XmlWriterSettings writerSettings = new XmlWriterSettings();
                writerSettings.Indent = true;
                writerSettings.IndentChars = "  ";
                writerSettings.NewLineChars = Environment.NewLine;
                writerSettings.NamespaceHandling = NamespaceHandling.OmitDuplicates;
                
                XmlWriter writer = XmlWriter.Create(stream, writerSettings);
                
                DataContractSerializer serializer;

                if (surrogate == null)
                {
                    serializer = new DataContractSerializer(typeof(T));
                }
                else
                {
                    serializer = new DataContractSerializer(typeof(T), null, int.MaxValue, false, false, surrogate);
                }

                if (namespacePrefixes == null || namespacePrefixes.Count == 0)
                {
                    serializer.WriteObject(writer, obj);
                }
                else
                {
                    serializer.WriteStartObject(writer, obj);
                    foreach (KeyValuePair<string, string> prefix in namespacePrefixes)
                    {
                        writer.WriteAttributeString("xmlns", prefix.Key, null, prefix.Value);
                    }
                    serializer.WriteObjectContent(writer, obj);
                    serializer.WriteEndObject(writer);
                }

                writer.Flush();
                writer.Close();
            }
        }
    }
}