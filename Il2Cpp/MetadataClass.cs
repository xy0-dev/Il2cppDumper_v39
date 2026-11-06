using System;

namespace Il2CppDumper
{
    public class Il2CppGlobalMetadataHeader
    {
        public uint sanity;
        public int version;

        // v39 stores an element count after every (offset, size) pair.
        public uint stringLiteralOffset;
        public int stringLiteralSize;
        public int stringLiteralCount;
        public uint stringLiteralDataOffset;
        public int stringLiteralDataSize;
        public int stringLiteralDataCount;
        public uint stringOffset;
        public int stringSize;
        public int stringCount;
        public uint eventsOffset;
        public int eventsSize;
        public int eventsCount;
        public uint propertiesOffset;
        public int propertiesSize;
        public int propertiesCount;
        public uint methodsOffset;
        public int methodsSize;
        public int methodsCount;
        public uint parameterDefaultValuesOffset;
        public int parameterDefaultValuesSize;
        public int parameterDefaultValuesCount;
        public uint fieldDefaultValuesOffset;
        public int fieldDefaultValuesSize;
        public int fieldDefaultValuesCount;
        public uint fieldAndParameterDefaultValueDataOffset;
        public int fieldAndParameterDefaultValueDataSize;
        public int fieldAndParameterDefaultValueDataCount;
        public uint fieldMarshaledSizesOffset;
        public int fieldMarshaledSizesSize;
        public int fieldMarshaledSizesCount;
        public uint parametersOffset;
        public int parametersSize;
        public int parametersCount;
        public uint fieldsOffset;
        public int fieldsSize;
        public int fieldsCount;
        public uint genericParametersOffset;
        public int genericParametersSize;
        public int genericParametersCount;
        public uint genericParameterConstraintsOffset;
        public int genericParameterConstraintsSize;
        public int genericParameterConstraintsCount;
        public uint genericContainersOffset;
        public int genericContainersSize;
        public int genericContainersCount;
        public uint nestedTypesOffset;
        public int nestedTypesSize;
        public int nestedTypesCount;
        public uint interfacesOffset;
        public int interfacesSize;
        public int interfacesCount;
        public uint vtableMethodsOffset;
        public int vtableMethodsSize;
        public int vtableMethodsCount;
        public uint interfaceOffsetsOffset;
        public int interfaceOffsetsSize;
        public int interfaceOffsetsCount;
        public uint typeDefinitionsOffset;
        public int typeDefinitionsSize;
        public int typeDefinitionsCount;
        public uint imagesOffset;
        public int imagesSize;
        public int imagesCount;
        public uint assembliesOffset;
        public int assembliesSize;
        public int assembliesCount;
        public uint fieldRefsOffset;
        public int fieldRefsSize;
        public int fieldRefsCount;
        public int referencedAssembliesOffset;
        public int referencedAssembliesSize;
        public int referencedAssembliesCount;
        public uint attributeDataOffset;
        public int attributeDataSize;
        public int attributeDataCount;
        public uint attributeDataRangeOffset;
        public int attributeDataRangeSize;
        public int attributeDataRangeCount;
        public int unresolvedVirtualCallParameterTypesOffset;
        public int unresolvedVirtualCallParameterTypesSize;
        public int unresolvedVirtualCallParameterTypesCount;
        public int unresolvedVirtualCallParameterRangesOffset;
        public int unresolvedVirtualCallParameterRangesSize;
        public int unresolvedVirtualCallParameterRangesCount;
        public int windowsRuntimeTypeNamesOffset;
        public int windowsRuntimeTypeNamesSize;
        public int windowsRuntimeTypeNamesCount;
        public int windowsRuntimeStringsOffset;
        public int windowsRuntimeStringsSize;
        public int windowsRuntimeStringsCount;
        public int exportedTypeDefinitionsOffset;
        public int exportedTypeDefinitionsSize;
        public int exportedTypeDefinitionsCount;
    }

    public class Il2CppAssemblyDefinition
    {
        public int imageIndex;
        [Version(Min = 24.1)]
        public uint token;
        public uint moduleToken;
        [Version(Max = 24)]
        public int customAttributeIndex;
        [Version(Min = 20)]
        public int referencedAssemblyStart;
        [Version(Min = 20)]
        public int referencedAssemblyCount;
        public Il2CppAssemblyNameDefinition aname;
    }

    public class Il2CppAssemblyNameDefinition
    {
        public uint nameIndex;
        public uint cultureIndex;
        [Version(Max = 24.3)]
        public int hashValueIndex;
        public uint publicKeyIndex;
        public uint hash_alg;
        public int hash_len;
        public uint flags;
        public int major;
        public int minor;
        public int build;
        public int revision;
        [ArrayLength(Length = 8)]
        public byte[] public_key_token;
    }

    public class Il2CppImageDefinition
    {
        public uint nameIndex;
        public int assemblyIndex;

        [VariableIndex(VariableIndexKind.TypeDefinition)]
        public int typeStart;
        [VariableIndex(VariableIndexKind.TypeDefinition)]
        public int typeCount;

        [Version(Min = 24)]
        public int exportedTypeStart;
        [Version(Min = 24)]
        public uint exportedTypeCount;

        public int entryPointIndex;
        [Version(Min = 19)]
        public uint token;

        [Version(Min = 24.1)]
        public int customAttributeStart;
        [Version(Min = 24.1)]
        public uint customAttributeCount;
    }

    public class Il2CppTypeDefinition
    {
        public uint nameIndex;
        public uint namespaceIndex;
        [Version(Max = 24)]
        public int customAttributeIndex;
        [VariableIndex(VariableIndexKind.Type)]
        public int byvalTypeIndex;
        [Version(Max = 24.5)]
        public int byrefTypeIndex;

        [VariableIndex(VariableIndexKind.Type)]
        public int declaringTypeIndex;
        [VariableIndex(VariableIndexKind.Type)]
        public int parentIndex;
        [Version(Max = 38)]
        public int elementTypeIndex; // not stored in v39, rebuilt from parentIndex for enums

        [Version(Max = 24.1)]
        public int rgctxStartIndex;
        [Version(Max = 24.1)]
        public int rgctxCount;

        [VariableIndex(VariableIndexKind.GenericContainer)]
        public int genericContainerIndex;

        [Version(Max = 22)]
        public int delegateWrapperFromManagedToNativeIndex;
        [Version(Max = 22)]
        public int marshalingFunctionsIndex;
        [Version(Min = 21, Max = 22)]
        public int ccwFunctionIndex;
        [Version(Min = 21, Max = 22)]
        public int guidIndex;

        public uint flags;

        public int fieldStart;
        public int methodStart;
        public int eventStart;
        public int propertyStart;
        public int nestedTypesStart;
        public int interfacesStart;
        public int vtableStart;
        public int interfaceOffsetsStart;

        public ushort method_count;
        public ushort property_count;
        public ushort field_count;
        public ushort event_count;
        public ushort nested_type_count;
        public ushort vtable_count;
        public ushort interfaces_count;
        public ushort interface_offsets_count;

        // bitfield to portably encode boolean values as single bits
        // 01 - valuetype;
        // 02 - enumtype;
        // 03 - has_finalize;
        // 04 - has_cctor;
        // 05 - is_blittable;
        // 06 - is_import_or_windows_runtime;
        // 07-10 - One of nine possible PackingSize values (0, 1, 2, 4, 8, 16, 32, 64, or 128)
        // 11 - PackingSize is default
        // 12 - ClassSize is default
        // 13-16 - One of nine possible PackingSize values (0, 1, 2, 4, 8, 16, 32, 64, or 128) - the specified packing size (even for explicit layouts)
        public uint bitfield;
        [Version(Min = 19)]
        public uint token;

        public bool IsValueType => (bitfield & 0x1) == 1;
        public bool IsEnum => ((bitfield >> 1) & 0x1) == 1;
    }

    public class Il2CppMethodDefinition
    {
        public uint nameIndex;
        [VariableIndex(VariableIndexKind.TypeDefinition)]
        public int declaringType;
        [VariableIndex(VariableIndexKind.Type)]
        public int returnType;
        [Version(Min = 31)]
        public int returnParameterToken;
        [VariableIndex(VariableIndexKind.Parameter)]
        public int parameterStart;
        [Version(Max = 24)]
        public int customAttributeIndex;
        [VariableIndex(VariableIndexKind.GenericContainer)]
        public int genericContainerIndex;
        [Version(Max = 24.1)]
        public int methodIndex;
        [Version(Max = 24.1)]
        public int invokerIndex;
        [Version(Max = 24.1)]
        public int delegateWrapperIndex;
        [Version(Max = 24.1)]
        public int rgctxStartIndex;
        [Version(Max = 24.1)]
        public int rgctxCount;
        public uint token;
        public ushort flags;
        public ushort iflags;
        public ushort slot;
        public ushort parameterCount;
    }

    public class Il2CppParameterDefinition
    {
        public uint nameIndex;
        public uint token;
        [Version(Max = 24)]
        public int customAttributeIndex;
        [VariableIndex(VariableIndexKind.Type)]
        public int typeIndex;
    }

    public class Il2CppFieldDefinition
    {
        public uint nameIndex;
        [VariableIndex(VariableIndexKind.Type)]
        public int typeIndex;
        [Version(Max = 24)]
        public int customAttributeIndex;
        [Version(Min = 19)]
        public uint token;
    }

    public class Il2CppFieldDefaultValue
    {
        public int fieldIndex;
        [VariableIndex(VariableIndexKind.Type)]
        public int typeIndex;
        public int dataIndex;
    }

    public class Il2CppPropertyDefinition
    {
        public uint nameIndex;
        public int get;
        public int set;
        public uint attrs;
        [Version(Max = 24)]
        public int customAttributeIndex;
        [Version(Min = 19)]
        public uint token;
    }

    public class Il2CppCustomAttributeTypeRange
    {
        [Version(Min = 24.1)]
        public uint token;
        public int start;
        public int count;
    }

    public class Il2CppMetadataUsageList
    {
        public uint start;
        public uint count;
    }

    public class Il2CppMetadataUsagePair
    {
        public uint destinationIndex;
        public uint encodedSourceIndex;
    }

    public class Il2CppStringLiteral
    {
        [Version(Max = 38)]
        public uint length;
        public int dataIndex;
    }

    public class Il2CppParameterDefaultValue
    {
        [VariableIndex(VariableIndexKind.Parameter)]
        public int parameterIndex;
        [VariableIndex(VariableIndexKind.Type)]
        public int typeIndex;
        public int dataIndex;
    }

    public class Il2CppEventDefinition
    {
        public uint nameIndex;
        [VariableIndex(VariableIndexKind.Type)]
        public int typeIndex;
        public int add;
        public int remove;
        public int raise;
        [Version(Max = 24)]
        public int customAttributeIndex;
        [Version(Min = 19)]
        public uint token;
    }

    public class Il2CppGenericContainer
    {
        /* index of the generic type definition or the generic method definition corresponding to this container */
        public int ownerIndex; // either index into Il2CppClass metadata array or Il2CppMethodDefinition array
        public int type_argc;
        /* If true, we're a generic method, otherwise a generic type definition. */
        public int is_method;
        /* Our type parameters. */
        public int genericParameterStart;
    }

    public class Il2CppFieldRef
    {
        [VariableIndex(VariableIndexKind.Type)]
        public int typeIndex;
        public int fieldIndex; // local offset into type fields
    }

    public class Il2CppGenericParameter
    {
        [VariableIndex(VariableIndexKind.GenericContainer)]
        public int ownerIndex;  /* Type or method this parameter was defined in. */
        public uint nameIndex;
        public short constraintsStart;
        public short constraintsCount;
        public ushort num;
        public ushort flags;
    }

    public enum Il2CppRGCTXDataType
    {
        IL2CPP_RGCTX_DATA_INVALID,
        IL2CPP_RGCTX_DATA_TYPE,
        IL2CPP_RGCTX_DATA_CLASS,
        IL2CPP_RGCTX_DATA_METHOD,
        IL2CPP_RGCTX_DATA_ARRAY,
        IL2CPP_RGCTX_DATA_CONSTRAINED,
    }

    public class Il2CppRGCTXDefinitionData
    {
        public int rgctxDataDummy;
        public int methodIndex => rgctxDataDummy;
        public int typeIndex => rgctxDataDummy;
    }

    public class Il2CppRGCTXDefinition
    {
        public Il2CppRGCTXDataType type => type_post29 == 0 ? (Il2CppRGCTXDataType)type_pre29 : (Il2CppRGCTXDataType)type_post29;
        [Version(Max = 27.1)]
        public int type_pre29;
        [Version(Min = 29)]
        public ulong type_post29;
        [Version(Max = 27.1)]
        public Il2CppRGCTXDefinitionData data;
        [Version(Min = 27.2)]
        public ulong _data;
    }

    public enum Il2CppMetadataUsage
    {
        kIl2CppMetadataUsageInvalid,
        kIl2CppMetadataUsageTypeInfo,
        kIl2CppMetadataUsageIl2CppType,
        kIl2CppMetadataUsageMethodDef,
        kIl2CppMetadataUsageFieldInfo,
        kIl2CppMetadataUsageStringLiteral,
        kIl2CppMetadataUsageMethodRef,
    };

    public class Il2CppCustomAttributeDataRange
    {
        public uint token;
        public uint startOffset;
    }
}
