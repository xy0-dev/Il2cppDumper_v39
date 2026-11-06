using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Il2CppDumper
{
    public sealed class Metadata : BinaryStream
    {
        public Il2CppGlobalMetadataHeader header;
        public Il2CppImageDefinition[] imageDefs;
        public Il2CppAssemblyDefinition[] assemblyDefs;
        public Il2CppTypeDefinition[] typeDefs;
        public Il2CppMethodDefinition[] methodDefs;
        public Il2CppParameterDefinition[] parameterDefs;
        public Il2CppFieldDefinition[] fieldDefs;
        private readonly Dictionary<int, Il2CppFieldDefaultValue> fieldDefaultValuesDic;
        private readonly Dictionary<int, Il2CppParameterDefaultValue> parameterDefaultValuesDic;
        public Il2CppPropertyDefinition[] propertyDefs;
        public Il2CppCustomAttributeTypeRange[] attributeTypeRanges;
        public Il2CppCustomAttributeDataRange[] attributeDataRanges;
        private readonly Dictionary<Il2CppImageDefinition, Dictionary<uint, int>> attributeTypeRangesDic;
        public Il2CppStringLiteral[] stringLiterals;
        public int[] attributeTypes;
        public int[] interfaceIndices;
        public Dictionary<Il2CppMetadataUsage, SortedDictionary<uint, uint>> metadataUsageDic;
        public long metadataUsagesCount;
        public int[] nestedTypeIndices;
        public Il2CppEventDefinition[] eventDefs;
        public Il2CppGenericContainer[] genericContainers;
        public Il2CppFieldRef[] fieldRefs;
        public Il2CppGenericParameter[] genericParameters;
        public int[] constraintIndices;
        public uint[] vtableMethods;
        public Il2CppRGCTXDefinition[] rgctxEntries;

        private readonly Dictionary<uint, string> stringCache = new();

        public Metadata(Stream stream) : base(stream)
        {
            var sanity = ReadUInt32();
            if (sanity != 0xFAB11BAF)
            {
                throw new InvalidDataException("ERROR: Metadata file supplied is not valid metadata file.");
            }
            var version = ReadInt32();
            if (version != 39)
            {
                throw new NotSupportedException($"ERROR: Metadata file supplied is not a supported version[{version}].");
            }
            Version = version;
            header = ReadClass<Il2CppGlobalMetadataHeader>(0);
            InitializeVariableIndexWidths();

            imageDefs = ReadClassArray<Il2CppImageDefinition>(header.imagesOffset, header.imagesCount);
            assemblyDefs = ReadClassArray<Il2CppAssemblyDefinition>(header.assembliesOffset, header.assembliesCount);
            typeDefs = ReadClassArray<Il2CppTypeDefinition>(header.typeDefinitionsOffset, header.typeDefinitionsCount);
            methodDefs = ReadClassArray<Il2CppMethodDefinition>(header.methodsOffset, header.methodsCount);
            parameterDefs = ReadClassArray<Il2CppParameterDefinition>(header.parametersOffset, header.parametersCount);
            fieldDefs = ReadClassArray<Il2CppFieldDefinition>(header.fieldsOffset, header.fieldsCount);
            var fieldDefaultValues = ReadClassArray<Il2CppFieldDefaultValue>(header.fieldDefaultValuesOffset, header.fieldDefaultValuesCount);
            var parameterDefaultValues = ReadClassArray<Il2CppParameterDefaultValue>(header.parameterDefaultValuesOffset, header.parameterDefaultValuesCount);
            fieldDefaultValuesDic = fieldDefaultValues.ToDictionary(x => x.fieldIndex);
            parameterDefaultValuesDic = parameterDefaultValues.ToDictionary(x => x.parameterIndex);
            propertyDefs = ReadClassArray<Il2CppPropertyDefinition>(header.propertiesOffset, header.propertiesCount);
            interfaceIndices = ReadVariableIndexArray(header.interfacesOffset, header.interfacesCount, VariableIndexKind.Type);
            nestedTypeIndices = ReadClassArray<int>(header.nestedTypesOffset, header.nestedTypesCount);
            eventDefs = ReadClassArray<Il2CppEventDefinition>(header.eventsOffset, header.eventsCount);
            genericContainers = ReadClassArray<Il2CppGenericContainer>(header.genericContainersOffset, header.genericContainersCount);
            genericParameters = ReadClassArray<Il2CppGenericParameter>(header.genericParametersOffset, header.genericParametersCount);
            constraintIndices = ReadVariableIndexArray(header.genericParameterConstraintsOffset, header.genericParameterConstraintsCount, VariableIndexKind.Type);
            vtableMethods = ReadClassArray<uint>(header.vtableMethodsOffset, header.vtableMethodsCount);
            stringLiterals = ReadClassArray<Il2CppStringLiteral>(header.stringLiteralOffset, header.stringLiteralCount);
            fieldRefs = ReadClassArray<Il2CppFieldRef>(header.fieldRefsOffset, header.fieldRefsCount);
            attributeDataRanges = ReadClassArray<Il2CppCustomAttributeDataRange>(header.attributeDataRangeOffset, header.attributeDataRangeCount);

            attributeTypeRangesDic = new Dictionary<Il2CppImageDefinition, Dictionary<uint, int>>();
            foreach (var imageDef in imageDefs)
            {
                var dic = new Dictionary<uint, int>();
                attributeTypeRangesDic[imageDef] = dic;
                var end = imageDef.customAttributeStart + imageDef.customAttributeCount;
                for (int i = imageDef.customAttributeStart; i < end; i++)
                {
                    dic.Add(attributeDataRanges[i].token, i);
                }
            }

            FixIndexesV39();
        }

        private void InitializeVariableIndexWidths()
        {
            var typeWidth = header.parametersCount > 0
                ? header.parametersSize / header.parametersCount - 8
                : header.fieldsCount > 0
                    ? header.fieldsSize / header.fieldsCount - 8
                    : header.interfaceOffsetsCount > 0
                        ? header.interfaceOffsetsSize / header.interfaceOffsetsCount - 4
                        : sizeof(int);
            SetVariableIndexWidth(VariableIndexKind.Type, typeWidth);
            SetVariableIndexWidth(VariableIndexKind.TypeDefinition, GetIndexWidth(header.typeDefinitionsCount));
            SetVariableIndexWidth(VariableIndexKind.GenericContainer, GetIndexWidth(header.genericContainersCount));
            SetVariableIndexWidth(VariableIndexKind.Parameter, GetIndexWidth(header.parametersCount));
        }

        private static int GetIndexWidth(int elementCount)
        {
            if (elementCount < 0)
            {
                throw new InvalidDataException($"Invalid metadata element count: {elementCount}.");
            }
            if (elementCount <= byte.MaxValue)
            {
                return sizeof(byte);
            }
            if (elementCount <= ushort.MaxValue)
            {
                return sizeof(ushort);
            }
            return sizeof(int);
        }

        private int[] ReadVariableIndexArray(uint addr, int count, VariableIndexKind kind)
        {
            Position = addr;
            var values = new int[count];
            for (var i = 0; i < values.Length; i++)
            {
                values[i] = ReadVariableIndex(kind);
            }
            return values;
        }

        private void FixIndexesV39()
        {
            var systemEnumTypeIndex = -1;
            foreach (var typeDef in typeDefs)
            {
                if (GetStringFromIndex(typeDef.nameIndex) == "Enum" && GetStringFromIndex(typeDef.namespaceIndex) == "System")
                {
                    systemEnumTypeIndex = typeDef.byvalTypeIndex;
                    break;
                }
            }
            if (systemEnumTypeIndex < 0)
            {
                Console.WriteLine("WARNING: System.Enum not found; enum base types will be wrong.");
            }
            foreach (var typeDef in typeDefs)
            {
                if (typeDef.IsEnum)
                {
                    // v39 stores the underlying type in parentIndex and dropped elementTypeIndex.
                    typeDef.elementTypeIndex = typeDef.parentIndex;
                    if (systemEnumTypeIndex >= 0)
                    {
                        typeDef.parentIndex = systemEnumTypeIndex;
                    }
                }
                else
                {
                    typeDef.elementTypeIndex = -1;
                }
            }
        }

        public bool GetFieldDefaultValueFromIndex(int index, out Il2CppFieldDefaultValue value)
        {
            return fieldDefaultValuesDic.TryGetValue(index, out value);
        }

        public bool GetParameterDefaultValueFromIndex(int index, out Il2CppParameterDefaultValue value)
        {
            return parameterDefaultValuesDic.TryGetValue(index, out value);
        }

        public uint GetDefaultValueFromIndex(int index)
        {
            return (uint)(header.fieldAndParameterDefaultValueDataOffset + index);
        }

        public string GetStringFromIndex(uint index)
        {
            if (!stringCache.TryGetValue(index, out var result))
            {
                result = ReadStringToNull(header.stringOffset + index);
                stringCache.Add(index, result);
            }
            return result;
        }

        public int GetCustomAttributeIndex(Il2CppImageDefinition imageDef, int customAttributeIndex, uint token)
        {
            if (Version > 24)
            {
                if (attributeTypeRangesDic[imageDef].TryGetValue(token, out var index))
                {
                    return index;
                }
                else
                {
                    return -1;
                }
            }
            else
            {
                return customAttributeIndex;
            }
        }

        public string GetStringLiteralFromIndex(uint index)
        {
            var stringLiteral = stringLiterals[index];
            var start = (uint)(header.stringLiteralDataOffset + stringLiteral.dataIndex);
            var end = index + 1 < stringLiterals.Length
                ? (uint)(header.stringLiteralDataOffset + stringLiterals[index + 1].dataIndex)
                : (uint)(header.stringLiteralDataOffset + header.stringLiteralDataSize);
            Position = start;
            return Encoding.UTF8.GetString(ReadBytes(checked((int)(end - start))));
        }

        public static uint GetEncodedIndexType(uint index)
        {
            return (index & 0xE0000000) >> 29;
        }

        public uint GetDecodedMethodIndex(uint index)
        {
            if (Version >= 27)
            {
                return (index & 0x1FFFFFFEU) >> 1;
            }
            return index & 0x1FFFFFFFU;
        }

        public int SizeOf(Type type)
        {
            var size = 0;
            foreach (var i in type.GetFields())
            {
                var versionAttributes = i.GetCustomAttributes<VersionAttribute>().ToArray();
                if (versionAttributes.Length > 0 && !versionAttributes.Any(attr => Version >= attr.Min && Version <= attr.Max))
                {
                    continue;
                }
                var fieldType = i.FieldType;
                var variableIndex = i.GetCustomAttribute<VariableIndexAttribute>();
                if (variableIndex != null)
                {
                    size += GetVariableIndexWidth(variableIndex.Kind);
                }
                else if (fieldType.IsPrimitive)
                {
                    size += GetPrimitiveTypeSize(fieldType.Name);
                }
                else if (fieldType.IsEnum)
                {
                    var e = fieldType.GetField("value__").FieldType;
                    size += GetPrimitiveTypeSize(e.Name);
                }
                else if (fieldType.IsArray)
                {
                    var arrayLengthAttribute = i.GetCustomAttribute<ArrayLengthAttribute>();
                    size += arrayLengthAttribute.Length;
                }
                else
                {
                    size += SizeOf(fieldType);
                }
            }
            return size;

            static int GetPrimitiveTypeSize(string name)
            {
                return name switch
                {
                    "Int32" or "UInt32" => 4,
                    "Int16" or "UInt16" => 2,
                    _ => 0,
                };
            }
        }
    }
}
