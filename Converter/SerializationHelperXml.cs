using System;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace Converter
{
    /// <summary>
    /// Contains generic methods allowing for objects to be serialized and deserialized to/from disk.
    /// </summary>
    public static partial class SerializationHelper
    {
        /// <summary>
        /// Serializes the specified object to XML, and stores it with the specified filename.
        /// </summary>
        /// <param name="obj">The object to be serialized.</param>
        /// <param name="filename">The name that will be used to save the serialized object.</param>
        /// <returns>Returns <c>true</c> if the object was serialized successfully, otherwise <c>false</c></returns>
        public static Boolean XmlSerialize<T>(T obj, String filename)
        {
            try
            {
                Boolean success;
                String serializedObject = TryXmlSerialize<T>(obj, out success);
                if (success)
                {
                    var streamWriter = new StreamWriter(filename);
                    streamWriter.Write(serializedObject);
                    streamWriter.Flush();
                    streamWriter.Close();
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        // <summary>
        /// Serializes the specified object to XML, and stores it with the specified filename.
        /// </summary>
        /// <param name="obj">The object to be serialized.</param>
        /// <param name="filename">The name that will be used to save the serialized object.</param>
        /// <returns>Returns <c>true</c> if the object was serialized successfully, otherwise <c>false</c></returns>
        public static Boolean TryXmlSerialize<T>(T obj, String filename)
        {
            try
            {
                Boolean success = XmlSerialize<T>(obj, filename);
                return success;
            }
            catch (Exception)
            {
                // This is a "Try_ method, so we'll just silence this exception.
                return false;
            }
        }

        /// <summary>
        /// Serializes the specified object to an XML string.
        /// </summary>
        /// <returns>If <c>success</c> is <c>true</c>, returns a string containing the serialized object.  Otherwise, returns <c>null</c></returns>
        public static String TryXmlSerialize<T>(T obj, out Boolean success)
        {
            try
            {
                var serializer = new XmlSerializer(typeof(T));
                var stringWriter = new StringWriter();
                serializer.Serialize(stringWriter, obj);
                String serializedObject = stringWriter.ToString();
                stringWriter.Dispose();
                success = true;
                return serializedObject;
            }
            catch (Exception ex)
            {
                success = false;
                return null;
            }
        }

        /// <summary>
        /// Deserializes the XML file with the specified filename.
        /// </summary>
        /// <param name="filename">The name of the file that will be deserialized.</param>
        /// <returns>Returns the deserialized object.</returns>
        /// <exception cref="System.Exception">
        /// The file to be deserialized was not able to be opened for an unspecified reason.
        /// or
        /// Object could not be deserialized.
        /// </exception>
        public static T XmlDeserialize<T>(String filename)
        {
            try
            {
                var streamReader = new StreamReader(filename);
                String serializedObject = streamReader.ReadToEnd();
                streamReader.Close();
                streamReader.Dispose();
                Boolean success;
                Object deserializedObject = TryXmlDeserialize<T>(serializedObject, out success);
                if (success)
                {
                    return (T)deserializedObject;
                }
            }
            catch (Exception ex)
            {
                Exception e = new Exception("The file to be deserialized was not able to be opened for an unspecified reason.  The InnerException may have more information.", ex);
                throw e;
            }
            throw new Exception("The file could not be deserialized.");
        }

        /// <summary>
        /// Deserializes the specified XML-serialized object.
        /// </summary>
        /// <returns>If <c>success</c> is <c>true</c>, returns the deserialized object.  Otherwise, returns a default instance of <c>T</c></returns>
        public static T TryXmlDeserialize<T>(String serializedObject, out Boolean success)
        {
            var stringReader = new StringReader(serializedObject);
            XmlSerializer serializer;
            XmlReader xmlReader;
            try
            {
                serializer = new XmlSerializer(typeof(T));
                xmlReader = XmlReader.Create(stringReader);
                if (serializer.CanDeserialize(xmlReader))
                {
                    object obj = serializer.Deserialize(xmlReader);
                    if (obj is T)
                    {
                        var castedObj = (T)obj;
                        success = true;
                        return castedObj;
                    }
                }
            }
            catch (Exception)
            {
                success = false;
                return default(T);
            }
            success = false;
            return default(T);
        }

        /// <summary>
        /// Deserializes the XML file with the specified filename.
        /// </summary>
        /// <param name="filename">The name of the file that will be deserialized.</param>
        /// <param name="obj">The deserialized object instance.</param>
        /// <returns>Returns <c>true</c> if the object was deserialized successfully, otherwise <c>false</c></returns>
        public static Boolean TryXmlDeserialize<T>(String filename, out T obj)
        {
            try
            {
                obj = XmlDeserialize<T>(filename);
                return true;
            }
            catch (Exception)
            {
                obj = default(T);
                // Silence the exception.
                return false;
            }
        }
    }
}
