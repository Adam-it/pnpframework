﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


using PnP.Framework.SystemResources;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Xml;

namespace System.Resources.NetStandard
{
    public sealed class ResXDataNode : ISerializable
    {
        private static readonly char[] SpecialChars = new char[] { ' ', '\r', '\n' };

        private DataNodeInfo nodeInfo;

        private string name;
        private string comment;

        private string typeName; // is only used when we create a resxdatanode manually with an object and contains the FQN

        private string fileRefFullPath;
        private string fileRefType;
        private string fileRefTextEncoding;

        private object value;
        private ResXFileRef fileRef;

        private IFormatter binaryFormatter = null;

        // this is going to be used to check if a ResXDataNode is of type ResXFileRef
        private static readonly ITypeResolutionService internalTypeResolver = new AssemblyNamesTypeResolutionService(new AssemblyName[] { new AssemblyName("System.Windows.Forms") });

        // call back function to get type name for multitargeting.
        // No public property to force using constructors for the following reasons:
        // 1. one of the constructors needs this field (if used) to initialize the object, make it consistent with the other ctrs to avoid errors.
        // 2. once the object is constructed the delegate should not be changed to avoid getting inconsistent results.
        private Func<Type, string> typeNameConverter;

        private ResXDataNode()
        {
        }

        internal ResXDataNode DeepClone()
        {
            return new ResXDataNode
            {
                // nodeinfo is just made up of immutable objects, we don't need to clone it
                nodeInfo = nodeInfo?.Clone(),
                name = name,
                comment = comment,
                typeName = typeName,
                fileRefFullPath = fileRefFullPath,
                fileRefType = fileRefType,
                fileRefTextEncoding = fileRefTextEncoding,
                // we don't clone the value, because we don't know how
                value = value,
                fileRef = fileRef?.Clone(),
                typeNameConverter = typeNameConverter
            };
        }

        public ResXDataNode(string name, object value) : this(name, value, null)
        {
        }

        public ResXDataNode(string name, object value, Func<Type, string> typeNameConverter)
        {
            if (name == null)
            {
                throw (new ArgumentNullException(nameof(name)));
            }
            if (name.Length == 0)
            {
                throw (new ArgumentException(nameof(name)));
            }

            this.typeNameConverter = typeNameConverter;

            Type valueType = (value == null) ? typeof(object) : value.GetType();
            if (value != null && !valueType.IsSerializable)
            {
                throw new InvalidOperationException(string.Format(SR.NotSerializableType, name, valueType.FullName));
            }
            if (value != null)
            {
                typeName = MultitargetUtil.GetAssemblyQualifiedName(valueType, this.typeNameConverter);
            }

            this.name = name;
            this.value = value;
        }

        public ResXDataNode(string name, ResXFileRef fileRef) : this(name, fileRef, null)
        {
        }

        public ResXDataNode(string name, ResXFileRef fileRef, Func<Type, string> typeNameConverter)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException(nameof(name));
            }

            this.name = name;
            this.fileRef = fileRef ?? throw new ArgumentNullException(nameof(fileRef));
            this.typeNameConverter = typeNameConverter;
        }

        internal ResXDataNode(DataNodeInfo nodeInfo, string basePath)
        {
            this.nodeInfo = nodeInfo;
            InitializeDataNode(basePath);
        }

        private void InitializeDataNode(string basePath)
        {
            // we can only use our internal type resolver here
            // because we only want to check if this is a ResXFileRef node
            // and we can't be sure that we have a typeResolutionService that can
            // recognize this. It's not very clean but this should work.
            Type nodeType = null;
            if (!string.IsNullOrEmpty(nodeInfo.TypeName)) // can be null if we have a string (default for string is TypeName == null)
            {
                nodeType = internalTypeResolver.GetType(nodeInfo.TypeName, false, true);
            }

            if (nodeType != null && nodeType.Equals(typeof(ResXFileRef)))
            {
                // we have a fileref, split the value data and populate the fields
                string[] fileRefDetails = ResXFileRef.Converter.ParseResxFileRefString(nodeInfo.ValueData);
                if (fileRefDetails != null && fileRefDetails.Length > 1)
                {
                    if (!Path.IsPathRooted(fileRefDetails[0]) && basePath != null)
                    {
                        fileRefFullPath = Path.Combine(basePath, fileRefDetails[0]);
                    }
                    else
                    {
                        fileRefFullPath = fileRefDetails[0];
                    }
                    fileRefType = fileRefDetails[1];
                    if (fileRefDetails.Length > 2)
                    {
                        fileRefTextEncoding = fileRefDetails[2];
                    }
                }
            }
        }

        /// <summary>
        /// </summary>
        public string Comment
        {
            get
            {
                string result = comment;
                if (result == null && nodeInfo != null)
                {
                    result = nodeInfo.Comment;
                }
                return result ?? string.Empty;
            }
            set
            {
                comment = value;
            }
        }

        /// <summary>
        /// </summary>
        public string Name
        {
            get
            {
                string result = name;
                if (result == null && nodeInfo != null)
                {
                    result = nodeInfo.Name;
                }
                return result;
            }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(Name));
                }
                if (value.Length == 0)
                {
                    throw new ArgumentException(nameof(Name));
                }
                name = value;
            }
        }

        public ResXFileRef FileRef
        {
            get
            {
                if (FileRefFullPath == null)
                {
                    return null;
                }
                if (fileRef == null)
                {
                    fileRef =
                        string.IsNullOrEmpty(fileRefTextEncoding)
                            ? new ResXFileRef(FileRefFullPath, FileRefType)
                            : new ResXFileRef(FileRefFullPath, FileRefType, Encoding.GetEncoding(FileRefTextEncoding));
                }
                return fileRef;
            }
        }

        private string FileRefFullPath
        {
            get
            {
                return fileRef?.FileName ?? fileRefFullPath;
            }
        }

        private string FileRefType
        {
            get
            {
                return fileRef?.TypeName ?? fileRefType;
            }
        }

        private string FileRefTextEncoding
        {
            get
            {
                return fileRef?.TextFileEncoding?.BodyName ?? fileRefTextEncoding;
            }
        }

        private static string ToBase64WrappedString(byte[] data)
        {
            const int lineWrap = 80;
            const string crlf = "\r\n";
            const string prefix = "        ";
            string raw = Convert.ToBase64String(data);
            if (raw.Length > lineWrap)
            {
                StringBuilder output = new StringBuilder(raw.Length + (raw.Length / lineWrap) * 3); // word wrap on lineWrap chars, \r\n
                int current = 0;
                for (; current < raw.Length - lineWrap; current += lineWrap)
                {
                    output.Append(crlf);
                    output.Append(prefix);
                    output.Append(raw, current, lineWrap);
                }
                output.Append(crlf);
                output.Append(prefix);
                output.Append(raw, current, raw.Length - current);
                output.Append(crlf);
                return output.ToString();
            }

            return raw;
        }

        private void FillDataNodeInfoFromObject(DataNodeInfo nodeInfo, object value)
        {
            if (value is CultureInfo ci)
            { // special-case CultureInfo, cannot use CultureInfoConverter for serialization
                nodeInfo.ValueData = ci.Name;
                nodeInfo.TypeName = MultitargetUtil.GetAssemblyQualifiedName(typeof(CultureInfo), typeNameConverter);
            }
            else if (value is string str)
            {
                nodeInfo.ValueData = str;
            }
            else if (value is byte[] bytes)
            {
                nodeInfo.ValueData = ToBase64WrappedString(bytes);
                nodeInfo.TypeName = MultitargetUtil.GetAssemblyQualifiedName(typeof(byte[]), typeNameConverter);
            }
            else
            {
                Type valueType = (value == null) ? typeof(object) : value.GetType();
                if (value != null && !valueType.IsSerializable)
                {
                    throw new InvalidOperationException(string.Format(SR.NotSerializableType, name, valueType.FullName));
                }
                TypeConverter tc = TypeDescriptor.GetConverter(valueType);
                bool toString = tc.CanConvertTo(typeof(string));
                bool fromString = tc.CanConvertFrom(typeof(string));
                try
                {
                    if (toString && fromString)
                    {
                        nodeInfo.ValueData = tc.ConvertToInvariantString(value);
                        nodeInfo.TypeName = MultitargetUtil.GetAssemblyQualifiedName(valueType, typeNameConverter);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    // Some custom type converters will throw in ConvertTo(string)
                    // to indicate that this object should be serialized through ISeriazable
                    // instead of as a string. This is semi-wrong, but something we will have to
                    // live with to allow user created Cursors to be serializable.
                    if (ClientUtils.IsSecurityOrCriticalException(ex))
                    {
                        throw;
                    }
                }

                bool toByteArray = tc.CanConvertTo(typeof(byte[]));
                bool fromByteArray = tc.CanConvertFrom(typeof(byte[]));
                if (toByteArray && fromByteArray)
                {
                    byte[] data = (byte[])tc.ConvertTo(value, typeof(byte[]));
                    nodeInfo.ValueData = ToBase64WrappedString(data);
                    nodeInfo.MimeType = ResXResourceWriter.ByteArraySerializedObjectMimeType;
                    nodeInfo.TypeName = MultitargetUtil.GetAssemblyQualifiedName(valueType, typeNameConverter);
                    return;
                }

                if (value == null)
                {
                    nodeInfo.ValueData = string.Empty;
                    nodeInfo.TypeName = MultitargetUtil.GetAssemblyQualifiedName(typeof(ResXNullRef), typeNameConverter);
                }
                else
                {
                    if (binaryFormatter == null)
                    {
                        binaryFormatter = new BinaryFormatter
                        {
                            Binder = new ResXSerializationBinder(typeNameConverter)
                        };
                    }

                    using (MemoryStream ms = new MemoryStream())
                    {
#pragma warning disable SYSLIB0011 // Type or member is obsolete
                        binaryFormatter.Serialize(ms, value);
#pragma warning restore SYSLIB0011 // Type or member is obsolete
                        nodeInfo.ValueData = ToBase64WrappedString(ms.ToArray());
                    }

                    nodeInfo.MimeType = ResXResourceWriter.DefaultSerializedObjectMimeType;
                }
            }
        }

        private object GenerateObjectFromDataNodeInfo(DataNodeInfo dataNodeInfo, ITypeResolutionService typeResolver)
        {
            object result = null;
            string mimeTypeName = dataNodeInfo.MimeType;
            // default behavior: if we dont have a type name, it's a string
            string typeName =
                string.IsNullOrEmpty(dataNodeInfo.TypeName)
                    ? MultitargetUtil.GetAssemblyQualifiedName(typeof(string), typeNameConverter)
                    : dataNodeInfo.TypeName;

            if (!string.IsNullOrEmpty(mimeTypeName))
            {
                if (string.Equals(mimeTypeName, ResXResourceWriter.BinSerializedObjectMimeType))
                {
                    string text = dataNodeInfo.ValueData;
                    byte[] serializedData = FromBase64WrappedString(text);

                    if (binaryFormatter == null)
                    {
                        binaryFormatter = new BinaryFormatter
                        {
                            Binder = new ResXSerializationBinder(typeResolver)
                        };
                    }
                    IFormatter formatter = binaryFormatter;
                    if (serializedData != null && serializedData.Length > 0)
                    {
#pragma warning disable SYSLIB0011 // Type or member is obsolete
                        result = formatter.Deserialize(new MemoryStream(serializedData));
#pragma warning restore SYSLIB0011 // Type or member is obsolete
                        if (result is ResXNullRef)
                        {
                            result = null;
                        }
                    }
                }

                else if (string.Equals(mimeTypeName, ResXResourceWriter.ByteArraySerializedObjectMimeType))
                {
                    if (!string.IsNullOrEmpty(typeName))
                    {
                        Type type = ResolveType(typeName, typeResolver);
                        if (type != null)
                        {
                            TypeConverter tc = TypeDescriptor.GetConverter(type);
                            if (tc.CanConvertFrom(typeof(byte[])))
                            {
                                string text = dataNodeInfo.ValueData;
                                byte[] serializedData = FromBase64WrappedString(text);

                                if (serializedData != null)
                                {
                                    result = tc.ConvertFrom(serializedData);
                                }
                            }
                        }
                        else
                        {
                            string newMessage = string.Format(SR.TypeLoadException, typeName, dataNodeInfo.ReaderPosition.Y, dataNodeInfo.ReaderPosition.X);
                            XmlException xml = new XmlException(newMessage, null, dataNodeInfo.ReaderPosition.Y, dataNodeInfo.ReaderPosition.X);
                            TypeLoadException newTle = new TypeLoadException(newMessage, xml);

                            throw newTle;
                        }
                    }
                }
            }
            else if (!string.IsNullOrEmpty(typeName))
            {
                Type type = ResolveType(typeName, typeResolver);
                if (type != null)
                {
                    if (type == typeof(ResXNullRef))
                    {
                        result = null;
                    }
                    else if (typeName.IndexOf("System.Byte[]") != -1 && typeName.IndexOf("mscorlib") != -1)
                    {
                        // Handle byte[]'s, which are stored as base-64 encoded strings.
                        // We can't hard-code byte[] type name due to version number
                        // updates & potential whitespace issues with ResX files.
                        result = FromBase64WrappedString(dataNodeInfo.ValueData);
                    }
                    else
                    {
                        TypeConverter tc = TypeDescriptor.GetConverter(type);
                        if (tc.CanConvertFrom(typeof(string)))
                        {
                            string text = dataNodeInfo.ValueData;
                            try
                            {
                                result = tc.ConvertFromInvariantString(text);
                            }
                            catch (NotSupportedException nse)
                            {
                                string newMessage = string.Format(SR.NotSupported, typeName, dataNodeInfo.ReaderPosition.Y, dataNodeInfo.ReaderPosition.X, nse.Message);
                                XmlException xml = new XmlException(newMessage, nse, dataNodeInfo.ReaderPosition.Y, dataNodeInfo.ReaderPosition.X);
                                NotSupportedException newNse = new NotSupportedException(newMessage, xml);
                                throw newNse;
                            }
                        }
                        else
                        {
                            Debug.WriteLine("Converter for " + type.FullName + " doesn't support string conversion");
                        }
                    }
                }
                else
                {
                    string newMessage = string.Format(SR.TypeLoadException, typeName, dataNodeInfo.ReaderPosition.Y, dataNodeInfo.ReaderPosition.X);
                    XmlException xml = new XmlException(newMessage, null, dataNodeInfo.ReaderPosition.Y, dataNodeInfo.ReaderPosition.X);
                    TypeLoadException newTle = new TypeLoadException(newMessage, xml);

                    throw newTle;
                }
            }
            else
            {
                // if mimeTypeName and typeName are not filled in, the value must be a string
                Debug.Assert(value is string, "Resource entries with no Type or MimeType must be encoded as strings");
            }
            return result;
        }

        internal DataNodeInfo GetDataNodeInfo()
        {
            bool shouldSerialize = true;
            if (nodeInfo != null)
            {
                shouldSerialize = false;
            }
            else
            {
                nodeInfo = new DataNodeInfo();
            }
            nodeInfo.Name = Name;
            nodeInfo.Comment = Comment;

            // We always serialize if this node represents a FileRef. This is because FileRef is a public property,
            // so someone could have modified it.
            if (shouldSerialize || FileRefFullPath != null)
            {
                // if we dont have a datanodeinfo it could be either
                // a direct object OR a fileref
                if (FileRefFullPath != null)
                {
                    nodeInfo.ValueData = FileRef.ToString();
                    nodeInfo.MimeType = null;
                    nodeInfo.TypeName = MultitargetUtil.GetAssemblyQualifiedName(typeof(ResXFileRef), typeNameConverter);
                }
                else
                {
                    // serialize to string inside the nodeInfo
                    FillDataNodeInfoFromObject(nodeInfo, value);
                }
            }
            return nodeInfo;
        }

        /// <summary>
        ///  Might return the position in the resx file of the current node, if known
        ///  otherwise, will return Point(0,0) since point is a struct
        /// </summary>
        public Point GetNodePosition()
        {
            return nodeInfo?.ReaderPosition ?? new Point();
        }

        /// <summary>
        ///  Get the FQ type name for this datanode.
        ///  We return typeof(object) for ResXNullRef
        /// </summary>
        public string GetValueTypeName(ITypeResolutionService typeResolver)
        {
            // the type name here is always a FQN
            if (!string.IsNullOrEmpty(typeName))
            {
                return
                    typeName == MultitargetUtil.GetAssemblyQualifiedName(typeof(ResXNullRef), typeNameConverter)
                        ? MultitargetUtil.GetAssemblyQualifiedName(typeof(object), typeNameConverter)
                        : typeName;
            }
            string result = FileRefType;
            Type objectType = null;
            // do we have a fileref?
            if (result != null)
            {
                // try to resolve this type
                objectType = ResolveType(FileRefType, typeResolver);
            }
            else if (nodeInfo != null)
            {
                // we dont have a fileref, try to resolve the type of the datanode
                result = nodeInfo.TypeName;
                // if typename is null, the default is just a string
                if (string.IsNullOrEmpty(result))
                {
                    // we still dont know... do we have a mimetype? if yes, our only option is to
                    // deserialize to know what we're dealing with... very inefficient...
                    if (!string.IsNullOrEmpty(nodeInfo.MimeType))
                    {
                        object insideObject = null;

                        try
                        {
                            insideObject = GenerateObjectFromDataNodeInfo(nodeInfo, typeResolver);
                        }
                        catch (Exception ex)
                        { // it'd be better to catch SerializationException but the underlying type resolver
                          // can throw things like FileNotFoundException which is kinda confusing, so I am catching all here..
                            if (ClientUtils.IsCriticalException(ex))
                            {
                                throw;
                            }
                            // something went wrong, type is not specified at all or stream is corrupted
                            // return system.object
                            result = MultitargetUtil.GetAssemblyQualifiedName(typeof(object), typeNameConverter);
                        }

                        if (insideObject != null)
                        {
                            result = MultitargetUtil.GetAssemblyQualifiedName(insideObject.GetType(), typeNameConverter);
                        }
                    }
                    else
                    {
                        // no typename, no mimetype, we have a string...
                        result = MultitargetUtil.GetAssemblyQualifiedName(typeof(string), typeNameConverter);
                    }
                }
                else
                {
                    objectType = ResolveType(nodeInfo.TypeName, typeResolver);
                }
            }
            if (objectType != null)
            {
                if (objectType == typeof(ResXNullRef))
                {
                    result = MultitargetUtil.GetAssemblyQualifiedName(typeof(object), typeNameConverter);
                }
                else
                {
                    result = MultitargetUtil.GetAssemblyQualifiedName(objectType, typeNameConverter);
                }
            }
            return result;
        }

        /// <summary>
        ///  Get the FQ type name for this datanode
        /// </summary>
        public string GetValueTypeName(AssemblyName[] names)
        {
            return GetValueTypeName(new AssemblyNamesTypeResolutionService(names));
        }

        /// <summary>
        ///  Get the value contained in this datanode
        /// </summary>
        public object GetValue(ITypeResolutionService typeResolver)
        {
            if (value != null)
            {
                return value;
            }

            object result = null;
            if (FileRefFullPath != null)
            {
                Type objectType = ResolveType(FileRefType, typeResolver);
                if (objectType != null)
                {
                    // we have the FQN for this type
                    fileRef =
                        FileRefTextEncoding != null
                            ? new ResXFileRef(FileRefFullPath, FileRefType, Encoding.GetEncoding(FileRefTextEncoding))
                            : new ResXFileRef(FileRefFullPath, FileRefType);
                    TypeConverter tc = TypeDescriptor.GetConverter(typeof(ResXFileRef));
                    result = tc.ConvertFrom(fileRef.ToString());
                }
                else
                {
                    string newMessage = string.Format(SR.TypeLoadExceptionShort, FileRefType);
                    TypeLoadException newTle = new TypeLoadException(newMessage);
                    throw (newTle);
                }
            }
            else if (nodeInfo.ValueData != null)
            {
                // it's embedded, we deserialize it
                result = GenerateObjectFromDataNodeInfo(nodeInfo, typeResolver);
            }
            else
            {
                // schema is wrong and say minOccur for Value is 0,
                // but it's too late to change it...
                // we need to return null here
                return null;
            }
            return result;
        }

        /// <summary>
        ///  Get the value contained in this datanode
        /// </summary>
        public object GetValue(AssemblyName[] names)
        {
            return GetValue(new AssemblyNamesTypeResolutionService(names));
        }

        private static byte[] FromBase64WrappedString(string text)
        {
            if (text.IndexOfAny(SpecialChars) != -1)
            {
                StringBuilder sb = new StringBuilder(text.Length);
                foreach (var ch in text)
                {
                    switch (ch)
                    {
                        case ' ':
                        case '\r':
                        case '\n':
                            break;
                        default:
                            sb.Append(ch);
                            break;
                    }
                }
                return Convert.FromBase64String(sb.ToString());
            }

            return Convert.FromBase64String(text);
        }

        private Type ResolveType(string typeName, ITypeResolutionService typeResolver)
        {
            Type resolvedType = null;
            if (typeResolver != null)
            {
                // If we cannot find the strong-named type, then try to see
                // if the TypeResolver can bind to partial names. For this,
                // we will strip out the partial names and keep the rest of the
                // strong-name information to try again.

                resolvedType = typeResolver.GetType(typeName, false);
                if (resolvedType == null)
                {
                    string[] typeParts = typeName.Split(',');

                    // Break up the type name from the rest of the assembly strong name.
                    if (typeParts != null && typeParts.Length >= 2)
                    {
                        string partialName = typeParts[0].Trim();
                        string assemblyName = typeParts[1].Trim();
                        partialName = partialName + ", " + assemblyName;
                        resolvedType = typeResolver.GetType(partialName, false);
                    }
                }
            }

            if (resolvedType == null)
            {
                resolvedType = Type.GetType(typeName, false);
            }

            return resolvedType;
        }

        /// <summary>
        ///  Get the value contained in this datanode
        /// </summary>
        void ISerializable.GetObjectData(SerializationInfo si, StreamingContext context)
        {
            throw new PlatformNotSupportedException();
        }
    }

    internal class DataNodeInfo
    {
        internal string Name;
        internal string Comment;
        internal string TypeName;
        internal string MimeType;
        internal string ValueData;
        internal Point ReaderPosition; //only used to track position in the reader

        internal DataNodeInfo Clone()
        {
            return new DataNodeInfo
            {
                Name = Name,
                Comment = Comment,
                TypeName = TypeName,
                MimeType = MimeType,
                ValueData = ValueData,
                ReaderPosition = new Point(ReaderPosition.X, ReaderPosition.Y)
            };
        }
    }

    // This class implements a partial type resolver for the BinaryFormatter.
    // This is needed to be able to read binary serialized content from older
    // NDP types and map them to newer versions.
    //
    internal class ResXSerializationBinder : SerializationBinder
    {
        private readonly ITypeResolutionService typeResolver;
        private readonly Func<Type, string> typeNameConverter;

        internal ResXSerializationBinder(ITypeResolutionService typeResolver)
        {
            this.typeResolver = typeResolver;
        }

        internal ResXSerializationBinder(Func<Type, string> typeNameConverter)
        {
            this.typeNameConverter = typeNameConverter;
        }

        public override Type BindToType(string assemblyName, string typeName)
        {
            if (typeResolver == null)
            {
                return null;
            }

            typeName = typeName + ", " + assemblyName;

            Type type = typeResolver.GetType(typeName);
            if (type == null)
            {
                string[] typeParts = typeName.Split(',');

                // Break up the assembly name from the rest of the assembly strong name.
                // we try 1) FQN 2) FQN without a version 3) just the short name
                if (typeParts != null && typeParts.Length > 2)
                {
                    string partialName = typeParts[0].Trim();

                    for (int i = 1; i < typeParts.Length; ++i)
                    {
                        string typePart = typeParts[i].Trim();
                        if (!typePart.StartsWith("Version=") && !typePart.StartsWith("version="))
                        {
                            partialName = partialName + ", " + typePart;
                        }
                    }
                    type = typeResolver.GetType(partialName);
                    if (type == null)
                    {
                        type = typeResolver.GetType(typeParts[0].Trim());
                    }
                }
            }

            // Binder couldn't handle it, let the default loader take over.
            return type;
        }

        //
        // Get the multitarget-aware string representation for the give type.
        public override void BindToName(Type serializedType, out string assemblyName, out string typeName)
        {
            // Normally we don't change typeName when changing the target framework,
            // only assembly version or assembly name might change, thus we are setting
            // typeName only if it changed with the framework version.
            // If binder passes in a null, BinaryFormatter will use the original value or
            // for un-serializable types will redirect to another type.
            // For example:
            //
            // Encoding = Encoding.GetEncoding("shift_jis");
            // public Encoding Encoding { get; set; }
            // property type (Encoding) is abstract, but the value is instantiated to a specific class,
            // and should be serialized as a specific class in order to be able to instantiate the result.
            //
            // another example are singleton objects like DBNull.Value which are serialized by System.UnitySerializationHolder
            typeName = null;
            if (typeNameConverter != null)
            {
                string assemblyQualifiedTypeName = MultitargetUtil.GetAssemblyQualifiedName(serializedType, typeNameConverter);
                if (!string.IsNullOrEmpty(assemblyQualifiedTypeName))
                {
                    int pos = assemblyQualifiedTypeName.IndexOf(',');
                    if (pos > 0 && pos < assemblyQualifiedTypeName.Length - 1)
                    {
                        assemblyName = assemblyQualifiedTypeName.Substring(pos + 1).TrimStart();
                        string newTypeName = assemblyQualifiedTypeName.Substring(0, pos);
                        if (!string.Equals(newTypeName, serializedType.FullName, StringComparison.InvariantCulture))
                        {
                            typeName = newTypeName;
                        }
                        return;
                    }
                }
            }

            base.BindToName(serializedType, out assemblyName, out typeName);
        }
    }

    internal class AssemblyNamesTypeResolutionService : ITypeResolutionService
    {
        private AssemblyName[] names;
        private Hashtable cachedAssemblies;
        private Hashtable cachedTypes;

        private static readonly string s_dotNetPath = Path.Combine(Environment.GetEnvironmentVariable("ProgramFiles"), "dotnet\\shared");
        private static readonly string s_dotNetPathX86 = Path.Combine(Environment.GetEnvironmentVariable("ProgramFiles(x86)"), "dotnet\\shared");

        internal AssemblyNamesTypeResolutionService(AssemblyName[] names)
        {
            this.names = names;
        }

        public Assembly GetAssembly(AssemblyName name)
        {
            return GetAssembly(name, true);
        }

        //
        public Assembly GetAssembly(AssemblyName name, bool throwOnError)
        {
            Assembly result = null;

            if (cachedAssemblies == null)
            {
                cachedAssemblies = Hashtable.Synchronized(new Hashtable());
            }

            if (cachedAssemblies.Contains(name))
            {
                result = cachedAssemblies[name] as Assembly;
            }

            if (result == null)
            {
                result = Assembly.Load(name.FullName);
                if (result != null)
                {
                    cachedAssemblies[name] = result;
                }
                else if (names != null)
                {
                    foreach (AssemblyName asmName in names.Where(an => an.Equals(name)))
                    {
                        try
                        {
                            result = Assembly.LoadFrom(GetPathOfAssembly(asmName));
                            if (result != null)
                            {
                                cachedAssemblies[asmName] = result;
                            }
                        }
                        catch
                        {
                            if (throwOnError)
                            {
                                throw;
                            }
                        }
                    }
                }
            }

            return result;
        }

        public string GetPathOfAssembly(AssemblyName name)
        {
            return name.CodeBase;
        }

        public Type GetType(string name)
        {
            return GetType(name, true);
        }

        public Type GetType(string name, bool throwOnError)
        {
            return GetType(name, throwOnError, false);
        }

        public Type GetType(string name, bool throwOnError, bool ignoreCase)
        {
            Type result = null;

            // Check type cache first
            if (cachedTypes == null)
            {
                cachedTypes = Hashtable.Synchronized(new Hashtable(StringComparer.Ordinal));
            }

            if (cachedTypes.Contains(name))
            {
                result = cachedTypes[name] as Type;
                return result;
            }

            // Missed in cache, try to resolve the type from the reference assemblies.
            if (name.IndexOf(',') != -1)
            {
                result = Type.GetType(name, false, ignoreCase);
            }

            if (result == null && names != null)
            {
                //
                // If the type is assembly qualified name, we sort the assembly names
                // to put assemblies with same name in the front so that they can
                // be searched first.
                int pos = name.IndexOf(',');
                if (pos > 0 && pos < name.Length - 1)
                {
                    string fullName = name.Substring(pos + 1).Trim();
                    AssemblyName assemblyName = null;
                    try
                    {
                        assemblyName = new AssemblyName(fullName);
                    }
                    catch
                    {
                    }

                    if (assemblyName != null)
                    {
                        List<AssemblyName> assemblyList = new List<AssemblyName>(names.Length);
                        foreach (AssemblyName asmName in names)
                        {
                            if (string.Compare(assemblyName.Name, asmName.Name, StringComparison.OrdinalIgnoreCase) == 0)
                            {
                                assemblyList.Insert(0, asmName);
                            }
                            else
                            {
                                assemblyList.Add(asmName);
                            }
                        }
                        names = assemblyList.ToArray();
                    }
                }

                // Search each reference assembly
                foreach (AssemblyName asmName in names)
                {
                    Assembly asm = GetAssembly(asmName, false);
                    if (asm != null)
                    {
                        result = asm.GetType(name, false, ignoreCase);
                        if (result == null)
                        {
                            int indexOfComma = name.IndexOf(',');
                            if (indexOfComma != -1)
                            {
                                string shortName = name.Substring(0, indexOfComma);
                                result = asm.GetType(shortName, false, ignoreCase);
                            }
                        }
                    }

                    if (result != null)
                    {
                        break;
                    }
                }
            }

            if (result == null && throwOnError)
            {
                throw new ArgumentException(string.Format(SR.InvalidResXNoType, name));
            }

            if (result != null)
            {
                // Only cache types from the shared framework  because they don't need to update.
                // For simplicity, don't cache custom types
                if (IsDotNetAssembly(result.Assembly.Location))
                {
                    cachedTypes[name] = result;
                }
            }

            return result;
        }

        /// <summary>
        ///  This is matching %windir%\Microsoft.NET\Framework*, so both 32bit and 64bit framework will be covered.
        /// </summary>
        private bool IsDotNetAssembly(string assemblyPath)
        {
            return assemblyPath != null && (assemblyPath.StartsWith(s_dotNetPath, StringComparison.OrdinalIgnoreCase) || assemblyPath.StartsWith(s_dotNetPathX86, StringComparison.OrdinalIgnoreCase));
        }

        public void ReferenceAssembly(AssemblyName name)
        {
            throw new NotSupportedException();
        }
    }
}
