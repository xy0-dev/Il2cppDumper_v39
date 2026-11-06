using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using static Il2CppDumper.Il2CppConstants;

namespace Il2CppDumper
{
    public class StructGenerator
    {
        private readonly Il2CppExecutor executor;
        private readonly Metadata metadata;
        private readonly Il2Cpp il2Cpp;
        private readonly Dictionary<Il2CppTypeDefinition, string> typeDefImageNames = new();
        private readonly HashSet<string> structNameHashSet = new(StringComparer.Ordinal);
        private readonly Dictionary<Il2CppTypeDefinition, string> structNameDic = new();
        private readonly Dictionary<ulong, string> genericClassStructNameDic = new();
        private readonly Dictionary<string, Il2CppType> nameGenericClassDic = new();
        private static readonly HashSet<string> keyword = new(StringComparer.Ordinal)
        { "klass", "monitor", "register", "_cs", "auto", "friend", "template", "flat", "default", "_ds", "interrupt",
            "unsigned", "signed", "asm", "if", "case", "break", "continue", "do", "new", "_", "short", "union", "class", "namespace"};
        private static readonly HashSet<string> specialKeywords = new(StringComparer.Ordinal)
        { "inline", "near", "far" };

        public StructGenerator(Il2CppExecutor il2CppExecutor)
        {
            executor = il2CppExecutor;
            metadata = il2CppExecutor.metadata;
            il2Cpp = il2CppExecutor.il2Cpp;
        }

        public void WriteScript(string outputDir)
        {
            var json = new ScriptJson();
            // 生成唯一名称
            for (var imageIndex = 0; imageIndex < metadata.imageDefs.Length; imageIndex++)
            {
                var imageDef = metadata.imageDefs[imageIndex];
                var imageName = metadata.GetStringFromIndex(imageDef.nameIndex);
                var typeEnd = imageDef.typeStart + imageDef.typeCount;
                for (int typeIndex = imageDef.typeStart; typeIndex < typeEnd; typeIndex++)
                {
                    var typeDef = metadata.typeDefs[typeIndex];
                    typeDefImageNames.Add(typeDef, imageName);
                    CreateStructNameDic(typeDef);
                }
            }
            // 生成后面处理泛型实例要用到的字典
            foreach (var il2CppType in il2Cpp.types.Where(x => x.type == Il2CppTypeEnum.IL2CPP_TYPE_GENERICINST))
            {
                var genericClass = il2Cpp.MapVATR<Il2CppGenericClass>(il2CppType.data.generic_class);
                var typeDef = executor.GetGenericClassTypeDefinition(genericClass);
                if (typeDef == null)
                {
                    continue;
                }
                var typeBaseName = structNameDic[typeDef];
                var typeToReplaceName = FixName(executor.GetTypeDefName(typeDef, true, true));
                var typeReplaceName = FixName(executor.GetTypeName(il2CppType, true, false));
                var typeStructName = typeBaseName.Replace(typeToReplaceName, typeReplaceName);
                nameGenericClassDic[typeStructName] = il2CppType;
                genericClassStructNameDic[il2CppType.data.generic_class] = typeStructName;
            }
            // 处理函数
            foreach (var imageDef in metadata.imageDefs)
            {
                var imageName = metadata.GetStringFromIndex(imageDef.nameIndex);
                var typeEnd = imageDef.typeStart + imageDef.typeCount;
                for (int typeIndex = imageDef.typeStart; typeIndex < typeEnd; typeIndex++)
                {
                    var typeDef = metadata.typeDefs[typeIndex];
                    var typeName = executor.GetTypeDefName(typeDef, true, true);
                    var methodEnd = typeDef.methodStart + typeDef.method_count;
                    for (var i = typeDef.methodStart; i < methodEnd; ++i)
                    {
                        var methodDef = metadata.methodDefs[i];
                        var methodName = metadata.GetStringFromIndex(methodDef.nameIndex);
                        var methodPointer = il2Cpp.GetMethodPointer(imageName, methodDef);
                        if (methodPointer > 0)
                        {
                            var methodTypeSignature = new List<Il2CppTypeEnum>();
                            var scriptMethod = new ScriptMethod();
                            json.ScriptMethod.Add(scriptMethod);
                            scriptMethod.Address = il2Cpp.GetRVA(methodPointer);
                            var methodFullName = typeName + "$$" + methodName;
                            scriptMethod.Name = methodFullName;

                            var methodReturnType = il2Cpp.types[methodDef.returnType];
                            var returnType = ParseType(methodReturnType);
                            if (methodReturnType.byref == 1)
                            {
                                returnType += "*";
                            }
                            methodTypeSignature.Add(methodReturnType.byref == 1 ? Il2CppTypeEnum.IL2CPP_TYPE_PTR : methodReturnType.type);
                            var signature = $"{returnType} {FixName(methodFullName)} (";
                            var parameterStrs = new List<string>();
                            if ((methodDef.flags & METHOD_ATTRIBUTE_STATIC) == 0)
                            {
                                var thisType = ParseType(il2Cpp.types[typeDef.byvalTypeIndex]);
                                methodTypeSignature.Add(il2Cpp.types[typeDef.byvalTypeIndex].type);
                                parameterStrs.Add($"{thisType} __this");
                            }
                            else if (il2Cpp.Version <= 24)
                            {
                                methodTypeSignature.Add(Il2CppTypeEnum.IL2CPP_TYPE_PTR);
                                parameterStrs.Add($"Il2CppObject* __this");
                            }
                            for (var j = 0; j < methodDef.parameterCount; j++)
                            {
                                var parameterDef = metadata.parameterDefs[methodDef.parameterStart + j];
                                var parameterName = metadata.GetStringFromIndex(parameterDef.nameIndex);
                                var parameterType = il2Cpp.types[parameterDef.typeIndex];
                                var parameterCType = ParseType(parameterType);
                                if (parameterType.byref == 1)
                                {
                                    parameterCType += "*";
                                }
                                methodTypeSignature.Add(parameterType.byref == 1 ? Il2CppTypeEnum.IL2CPP_TYPE_PTR : parameterType.type);
                                parameterStrs.Add($"{parameterCType} {FixName(parameterName)}");
                            }
                            methodTypeSignature.Add(Il2CppTypeEnum.IL2CPP_TYPE_PTR);
                            parameterStrs.Add("const MethodInfo* method");
                            signature += string.Join(", ", parameterStrs);
                            signature += ");";
                            scriptMethod.Signature = signature;
                            scriptMethod.TypeSignature = GetMethodTypeSignature(methodTypeSignature);
                        }
                        //泛型实例函数
                        if (il2Cpp.methodDefinitionMethodSpecs.TryGetValue(i, out var methodSpecs))
                        {
                            foreach (var methodSpec in methodSpecs)
                            {
                                var genericMethodPointer = il2Cpp.methodSpecGenericMethodPointers[methodSpec];
                                if (genericMethodPointer > 0)
                                {
                                    var methodTypeSignature = new List<Il2CppTypeEnum>();
                                    var scriptMethod = new ScriptMethod();
                                    json.ScriptMethod.Add(scriptMethod);
                                    scriptMethod.Address = il2Cpp.GetRVA(genericMethodPointer);
                                    var methodInfoName = $"MethodInfo_{scriptMethod.Address:X}";
                                    (var methodSpecTypeName, var methodSpecMethodName) = executor.GetMethodSpecName(methodSpec, true);
                                    var methodFullName = methodSpecTypeName + "$$" + methodSpecMethodName;
                                    scriptMethod.Name = methodFullName;

                                    var genericContext = executor.GetMethodSpecGenericContext(methodSpec);
                                    var methodReturnType = il2Cpp.types[methodDef.returnType];
                                    var returnType = ParseType(methodReturnType, genericContext);
                                    if (methodReturnType.byref == 1)
                                    {
                                        returnType += "*";
                                    }
                                    methodTypeSignature.Add(methodReturnType.byref == 1 ? Il2CppTypeEnum.IL2CPP_TYPE_PTR : methodReturnType.type);
                                    var signature = $"{returnType} {FixName(methodFullName)} (";
                                    var parameterStrs = new List<string>();
                                    if ((methodDef.flags & METHOD_ATTRIBUTE_STATIC) == 0)
                                    {
                                        string thisType;
                                        if (methodSpec.classIndexIndex != -1)
                                        {
                                            var typeBaseName = structNameDic[typeDef];
                                            var typeToReplaceName = FixName(typeName);
                                            var typeReplaceName = FixName(methodSpecTypeName);
                                            var typeStructName = typeBaseName.Replace(typeToReplaceName, typeReplaceName);
                                            if (nameGenericClassDic.TryGetValue(typeStructName, out var il2CppType))
                                            {
                                                thisType = ParseType(il2CppType);
                                                methodTypeSignature.Add(il2CppType.type);
                                            }
                                            else
                                            {
                                                //没有单独的泛型实例类
                                                thisType = ParseType(il2Cpp.types[typeDef.byvalTypeIndex]);
                                                methodTypeSignature.Add(il2Cpp.types[typeDef.byvalTypeIndex].type);
                                            }
                                        }
                                        else
                                        {
                                            thisType = ParseType(il2Cpp.types[typeDef.byvalTypeIndex]);
                                            methodTypeSignature.Add(il2Cpp.types[typeDef.byvalTypeIndex].type);
                                        }
                                        parameterStrs.Add($"{thisType} __this");
                                    }
                                    else if (il2Cpp.Version <= 24)
                                    {
                                        methodTypeSignature.Add(Il2CppTypeEnum.IL2CPP_TYPE_PTR);
                                        parameterStrs.Add($"Il2CppObject* __this");
                                    }
                                    for (var j = 0; j < methodDef.parameterCount; j++)
                                    {
                                        var parameterDef = metadata.parameterDefs[methodDef.parameterStart + j];
                                        var parameterName = metadata.GetStringFromIndex(parameterDef.nameIndex);
                                        var parameterType = il2Cpp.types[parameterDef.typeIndex];
                                        var parameterCType = ParseType(parameterType, genericContext);
                                        if (parameterType.byref == 1)
                                        {
                                            parameterCType += "*";
                                        }
                                        methodTypeSignature.Add(parameterType.byref == 1 ? Il2CppTypeEnum.IL2CPP_TYPE_PTR : parameterType.type);
                                        parameterStrs.Add($"{parameterCType} {FixName(parameterName)}");
                                    }
                                    methodTypeSignature.Add(Il2CppTypeEnum.IL2CPP_TYPE_PTR);
                                    parameterStrs.Add($"const {methodInfoName}* method");
                                    signature += string.Join(", ", parameterStrs);
                                    signature += ");";
                                    scriptMethod.Signature = signature;
                                    scriptMethod.TypeSignature = GetMethodTypeSignature(methodTypeSignature);
                                }
                            }
                        }
                    }
                }
            }
            //处理函数范围
            List<ulong> orderedPointers;
            if (il2Cpp.Version >= 24.2)
            {
                orderedPointers = new List<ulong>();
                foreach (var pair in il2Cpp.codeGenModuleMethodPointers)
                {
                    orderedPointers.AddRange(pair.Value);
                }
            }
            else
            {
                orderedPointers = il2Cpp.methodPointers.ToList();
            }
            orderedPointers.AddRange(il2Cpp.genericMethodPointers);
            orderedPointers.AddRange(il2Cpp.invokerPointers);
            if (il2Cpp.Version < 29)
            {
                orderedPointers.AddRange(executor.customAttributeGenerators);
            }
            if (il2Cpp.Version >= 22)
            {
                if (il2Cpp.reversePInvokeWrappers != null)
                    orderedPointers.AddRange(il2Cpp.reversePInvokeWrappers);
                if (il2Cpp.unresolvedVirtualCallPointers != null)
                    orderedPointers.AddRange(il2Cpp.unresolvedVirtualCallPointers);
            }
            //TODO interopData内也包含函数
            orderedPointers = orderedPointers.Where(il2Cpp.IsValidPointer).Distinct().OrderBy(x => x).ToList();
            orderedPointers.Remove(0);
            json.Addresses = new ulong[orderedPointers.Count];
            for (int i = 0; i < orderedPointers.Count; i++)
            {
                json.Addresses[i] = il2Cpp.GetRVA(orderedPointers[i]);
            }
            // 处理MetadataUsage
            if (il2Cpp.Version >= 27)
            {
                var sectionHelper = executor.GetSectionHelper();
                foreach (var sec in sectionHelper.Data)
                {
                    il2Cpp.Position = sec.offset;
                    var end = Math.Min(sec.offsetEnd, il2Cpp.Length) - il2Cpp.PointerSize;
                    while (il2Cpp.Position < end)
                    {
                        var addr = il2Cpp.Position;
                        var metadataValue = il2Cpp.ReadUIntPtr();
                        var position = il2Cpp.Position;
                        if (metadataValue < uint.MaxValue)
                        {
                            var encodedToken = (uint)metadataValue;
                            var usage = Metadata.GetEncodedIndexType(encodedToken);
                            if (usage > 0 && usage <= 6)
                            {
                                var decodedIndex = metadata.GetDecodedMethodIndex(encodedToken);
                                if (metadataValue == ((usage << 29) | (decodedIndex << 1)) + 1)
                                {
                                    var va = il2Cpp.MapRTVA(addr);
                                    if (va > 0)
                                    {
                                        switch ((Il2CppMetadataUsage)usage)
                                        {
                                            case Il2CppMetadataUsage.kIl2CppMetadataUsageInvalid:
                                                break;
                                            case Il2CppMetadataUsage.kIl2CppMetadataUsageTypeInfo:
                                                if (decodedIndex < il2Cpp.types.Length)
                                                {
                                                    AddMetadataUsageTypeInfo(json, decodedIndex, va);
                                                }
                                                break;
                                            case Il2CppMetadataUsage.kIl2CppMetadataUsageIl2CppType:
                                                if (decodedIndex < il2Cpp.types.Length)
                                                {
                                                    AddMetadataUsageIl2CppType(json, decodedIndex, va);
                                                }
                                                break;
                                            case Il2CppMetadataUsage.kIl2CppMetadataUsageMethodDef:
                                                if (decodedIndex < metadata.methodDefs.Length)
                                                {
                                                    AddMetadataUsageMethodDef(json, decodedIndex, va);
                                                }
                                                break;
                                            case Il2CppMetadataUsage.kIl2CppMetadataUsageFieldInfo:
                                                if (decodedIndex < metadata.fieldRefs.Length)
                                                {
                                                    AddMetadataUsageFieldInfo(json, decodedIndex, va);
                                                }
                                                break;
                                            case Il2CppMetadataUsage.kIl2CppMetadataUsageStringLiteral:
                                                if (decodedIndex < metadata.stringLiterals.Length)
                                                {
                                                    AddMetadataUsageStringLiteral(json, decodedIndex, va);
                                                }
                                                break;
                                            case Il2CppMetadataUsage.kIl2CppMetadataUsageMethodRef:
                                                if (decodedIndex < il2Cpp.methodSpecs.Length)
                                                {
                                                    AddMetadataUsageMethodRef(json, decodedIndex, va);
                                                }
                                                break;
                                        }
                                        if (il2Cpp.Position != position)
                                        {
                                            il2Cpp.Position = position;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            else if (il2Cpp.Version > 16 && il2Cpp.Version < 27)
            {
                foreach (var i in metadata.metadataUsageDic[Il2CppMetadataUsage.kIl2CppMetadataUsageTypeInfo])
                {
                    AddMetadataUsageTypeInfo(json, i.Value, il2Cpp.metadataUsages[i.Key]);
                }
                foreach (var i in metadata.metadataUsageDic[Il2CppMetadataUsage.kIl2CppMetadataUsageIl2CppType])
                {
                    AddMetadataUsageIl2CppType(json, i.Value, il2Cpp.metadataUsages[i.Key]);
                }
                foreach (var i in metadata.metadataUsageDic[Il2CppMetadataUsage.kIl2CppMetadataUsageMethodDef])
                {
                    AddMetadataUsageMethodDef(json, i.Value, il2Cpp.metadataUsages[i.Key]);
                }
                foreach (var i in metadata.metadataUsageDic[Il2CppMetadataUsage.kIl2CppMetadataUsageFieldInfo])
                {
                    AddMetadataUsageFieldInfo(json, i.Value, il2Cpp.metadataUsages[i.Key]);
                }
                foreach (var i in metadata.metadataUsageDic[Il2CppMetadataUsage.kIl2CppMetadataUsageStringLiteral])
                {
                    AddMetadataUsageStringLiteral(json, i.Value, il2Cpp.metadataUsages[i.Key]);
                }
                foreach (var i in metadata.metadataUsageDic[Il2CppMetadataUsage.kIl2CppMetadataUsageMethodRef])
                {
                    AddMetadataUsageMethodRef(json, i.Value, il2Cpp.metadataUsages[i.Key]);
                }
            }
            var jsonOptions = new JsonSerializerOptions() { WriteIndented = true, IncludeFields = true };
            File.WriteAllText(outputDir + "script.json", JsonSerializer.Serialize(json, jsonOptions));
        }

        private void AddMetadataUsageTypeInfo(ScriptJson json, uint index, ulong address)
        {
            var type = il2Cpp.types[index];
            var typeName = executor.GetTypeName(type, true, false);
            var scriptMetadata = new ScriptMetadata();
            json.ScriptMetadata.Add(scriptMetadata);
            scriptMetadata.Address = il2Cpp.GetRVA(address);
            scriptMetadata.Name = typeName + "_TypeInfo";
            var signature = GetIl2CppStructName(type);
            if (signature.EndsWith("_array"))
            {
                scriptMetadata.Signature = "Il2CppClass*";
            }
            else
            {
                scriptMetadata.Signature = FixName(signature) + "_c*";
            }
        }

        private void AddMetadataUsageIl2CppType(ScriptJson json, uint index, ulong address)
        {
            var type = il2Cpp.types[index];
            var typeName = executor.GetTypeName(type, true, false);
            var scriptMetadata = new ScriptMetadata();
            json.ScriptMetadata.Add(scriptMetadata);
            scriptMetadata.Address = il2Cpp.GetRVA(address);
            scriptMetadata.Name = typeName + "_var";
            scriptMetadata.Signature = "Il2CppType*";
        }

        private void AddMetadataUsageMethodDef(ScriptJson json, uint index, ulong address)
        {
            var methodDef = metadata.methodDefs[index];
            var typeDef = metadata.typeDefs[methodDef.declaringType];
            var typeName = executor.GetTypeDefName(typeDef, true, true);
            var methodName = typeName + "." + metadata.GetStringFromIndex(methodDef.nameIndex) + "()";
            var scriptMetadataMethod = new ScriptMetadataMethod();
            json.ScriptMetadataMethod.Add(scriptMetadataMethod);
            scriptMetadataMethod.Address = il2Cpp.GetRVA(address);
            scriptMetadataMethod.Name = "Method$" + methodName;
            var imageName = typeDefImageNames[typeDef];
            var methodPointer = il2Cpp.GetMethodPointer(imageName, methodDef);
            if (methodPointer > 0)
            {
                scriptMetadataMethod.MethodAddress = il2Cpp.GetRVA(methodPointer);
            }
        }

        private void AddMetadataUsageFieldInfo(ScriptJson json, uint index, ulong address)
        {
            var fieldRef = metadata.fieldRefs[index];
            var type = il2Cpp.types[fieldRef.typeIndex];
            var typeDef = GetTypeDefinition(type);
            var fieldDef = metadata.fieldDefs[typeDef.fieldStart + fieldRef.fieldIndex];
            var fieldName = executor.GetTypeName(type, true, false) + "." + metadata.GetStringFromIndex(fieldDef.nameIndex);
            var scriptMetadata = new ScriptMetadata();
            json.ScriptMetadata.Add(scriptMetadata);
            scriptMetadata.Address = il2Cpp.GetRVA(address);
            scriptMetadata.Name = "Field$" + fieldName;
        }

        private void AddMetadataUsageStringLiteral(ScriptJson json, uint index, ulong address)
        {
            var scriptString = new ScriptString();
            json.ScriptString.Add(scriptString);
            scriptString.Address = il2Cpp.GetRVA(address);
            scriptString.Value = metadata.GetStringLiteralFromIndex(index);
        }

        private void AddMetadataUsageMethodRef(ScriptJson json, uint index, ulong address)
        {
            var methodSpec = il2Cpp.methodSpecs[index];
            var scriptMetadataMethod = new ScriptMetadataMethod();
            json.ScriptMetadataMethod.Add(scriptMetadataMethod);
            scriptMetadataMethod.Address = il2Cpp.GetRVA(address);
            (var methodSpecTypeName, var methodSpecMethodName) = executor.GetMethodSpecName(methodSpec, true);
            scriptMetadataMethod.Name = "Method$" + methodSpecTypeName + "." + methodSpecMethodName + "()";
            if (il2Cpp.methodSpecGenericMethodPointers.ContainsKey(methodSpec))
            {
                var genericMethodPointer = il2Cpp.methodSpecGenericMethodPointers[methodSpec];
                if (genericMethodPointer > 0)
                {
                    scriptMetadataMethod.MethodAddress = il2Cpp.GetRVA(genericMethodPointer);
                }
            }
        }

        private static string FixName(string str)
        {
            if (keyword.Contains(str))
            {
                str = "_" + str;
            }
            else if (specialKeywords.Contains(str))
            {
                str = "_" + str + "_";
            }

            if (Regex.IsMatch(str, "^[0-9]"))
            {
                return "_" + str;
            }
            else
            {
                return Regex.Replace(str, "[^a-zA-Z0-9_]", "_");
            }
        }

        private string ParseType(Il2CppType il2CppType, Il2CppGenericContext context = null)
        {
            switch (il2CppType.type)
            {
                case Il2CppTypeEnum.IL2CPP_TYPE_VOID:
                    return "void";
                case Il2CppTypeEnum.IL2CPP_TYPE_BOOLEAN:
                    return "bool";
                case Il2CppTypeEnum.IL2CPP_TYPE_CHAR:
                    return "uint16_t"; //Il2CppChar
                case Il2CppTypeEnum.IL2CPP_TYPE_I1:
                    return "int8_t";
                case Il2CppTypeEnum.IL2CPP_TYPE_U1:
                    return "uint8_t";
                case Il2CppTypeEnum.IL2CPP_TYPE_I2:
                    return "int16_t";
                case Il2CppTypeEnum.IL2CPP_TYPE_U2:
                    return "uint16_t";
                case Il2CppTypeEnum.IL2CPP_TYPE_I4:
                    return "int32_t";
                case Il2CppTypeEnum.IL2CPP_TYPE_U4:
                    return "uint32_t";
                case Il2CppTypeEnum.IL2CPP_TYPE_I8:
                    return "int64_t";
                case Il2CppTypeEnum.IL2CPP_TYPE_U8:
                    return "uint64_t";
                case Il2CppTypeEnum.IL2CPP_TYPE_R4:
                    return "float";
                case Il2CppTypeEnum.IL2CPP_TYPE_R8:
                    return "double";
                case Il2CppTypeEnum.IL2CPP_TYPE_STRING:
                    return "System_String_o*";
                case Il2CppTypeEnum.IL2CPP_TYPE_PTR:
                    {
                        var oriType = il2Cpp.GetIl2CppType(il2CppType.data.type);
                        return ParseType(oriType) + "*";
                    }
                case Il2CppTypeEnum.IL2CPP_TYPE_VALUETYPE:
                    {
                        var typeDef = executor.GetTypeDefinitionFromIl2CppType(il2CppType);
                        if (typeDef.IsEnum)
                        {
                            return ParseType(il2Cpp.types[typeDef.elementTypeIndex]);
                        }
                        return structNameDic[typeDef] + "_o";
                    }
                case Il2CppTypeEnum.IL2CPP_TYPE_CLASS:
                    {
                        var typeDef = executor.GetTypeDefinitionFromIl2CppType(il2CppType);
                        return structNameDic[typeDef] + "_o*";
                    }
                case Il2CppTypeEnum.IL2CPP_TYPE_VAR:
                    {
                        if (context != null)
                        {
                            var genericParameter = executor.GetGenericParameteFromIl2CppType(il2CppType);
                            var genericInst = il2Cpp.MapVATR<Il2CppGenericInst>(context.class_inst);
                            var pointers = il2Cpp.MapVATR<ulong>(genericInst.type_argv, genericInst.type_argc);
                            var pointer = pointers[genericParameter.num];
                            var type = il2Cpp.GetIl2CppType(pointer);
                            return ParseType(type);
                        }
                        return "Il2CppObject*";
                    }
                case Il2CppTypeEnum.IL2CPP_TYPE_ARRAY:
                    {
                        var arrayType = il2Cpp.MapVATR<Il2CppArrayType>(il2CppType.data.array);
                        var elementType = il2Cpp.GetIl2CppType(arrayType.etype);
                        var elementStructName = GetIl2CppStructName(elementType, context);
                        var typeStructName = elementStructName + "_array";
                        if (structNameHashSet.Add(typeStructName))
                        {
                        }
                        return typeStructName + "*";
                    }
                case Il2CppTypeEnum.IL2CPP_TYPE_GENERICINST:
                    {
                        var genericClass = il2Cpp.MapVATR<Il2CppGenericClass>(il2CppType.data.generic_class);
                        var typeDef = executor.GetGenericClassTypeDefinition(genericClass);
                        var typeStructName = genericClassStructNameDic[il2CppType.data.generic_class];
                        structNameHashSet.Add(typeStructName);
                        if (typeDef.IsValueType)
                        {
                            if (typeDef.IsEnum)
                            {
                                return ParseType(il2Cpp.types[typeDef.elementTypeIndex]);
                            }
                            return typeStructName + "_o";
                        }
                        return typeStructName + "_o*";
                    }
                case Il2CppTypeEnum.IL2CPP_TYPE_TYPEDBYREF:
                    return "Il2CppObject*";
                case Il2CppTypeEnum.IL2CPP_TYPE_I:
                    return "intptr_t";
                case Il2CppTypeEnum.IL2CPP_TYPE_U:
                    return "uintptr_t";
                case Il2CppTypeEnum.IL2CPP_TYPE_OBJECT:
                    return "Il2CppObject*";
                case Il2CppTypeEnum.IL2CPP_TYPE_SZARRAY:
                    {
                        var elementType = il2Cpp.GetIl2CppType(il2CppType.data.type);
                        var elementStructName = GetIl2CppStructName(elementType, context);
                        var typeStructName = elementStructName + "_array";
                        if (structNameHashSet.Add(typeStructName))
                        {
                        }
                        return typeStructName + "*";
                    }
                case Il2CppTypeEnum.IL2CPP_TYPE_MVAR:
                    {
                        if (context != null)
                        {
                            var genericParameter = executor.GetGenericParameteFromIl2CppType(il2CppType);
                            //https://github.com/Perfare/Il2CppDumper/issues/687
                            if (context.method_inst == 0 && context.class_inst != 0)
                            {
                                goto case Il2CppTypeEnum.IL2CPP_TYPE_VAR;
                            }
                            var genericInst = il2Cpp.MapVATR<Il2CppGenericInst>(context.method_inst);
                            var pointers = il2Cpp.MapVATR<ulong>(genericInst.type_argv, genericInst.type_argc);
                            var pointer = pointers[genericParameter.num];
                            var type = il2Cpp.GetIl2CppType(pointer);
                            return ParseType(type);
                        }
                        return "Il2CppObject*";
                    }
                default:
                    throw new NotSupportedException();
            }
        }
        public static string GetMethodTypeSignature(List<Il2CppTypeEnum> types)
        {
            string signature = string.Empty;
            foreach (Il2CppTypeEnum type in types)
            {
                signature += type switch
                {
                    Il2CppTypeEnum.IL2CPP_TYPE_VOID => "v",
                    Il2CppTypeEnum.IL2CPP_TYPE_BOOLEAN or Il2CppTypeEnum.IL2CPP_TYPE_CHAR or Il2CppTypeEnum.IL2CPP_TYPE_I1 or Il2CppTypeEnum.IL2CPP_TYPE_U1 or Il2CppTypeEnum.IL2CPP_TYPE_I2 or Il2CppTypeEnum.IL2CPP_TYPE_U2 or Il2CppTypeEnum.IL2CPP_TYPE_I4 or Il2CppTypeEnum.IL2CPP_TYPE_U4 => "i",
                    Il2CppTypeEnum.IL2CPP_TYPE_I8 or Il2CppTypeEnum.IL2CPP_TYPE_U8 => "j",
                    Il2CppTypeEnum.IL2CPP_TYPE_R4 => "f",
                    Il2CppTypeEnum.IL2CPP_TYPE_R8 => "d",
                    Il2CppTypeEnum.IL2CPP_TYPE_STRING or Il2CppTypeEnum.IL2CPP_TYPE_PTR or Il2CppTypeEnum.IL2CPP_TYPE_VALUETYPE or Il2CppTypeEnum.IL2CPP_TYPE_CLASS or Il2CppTypeEnum.IL2CPP_TYPE_VAR or Il2CppTypeEnum.IL2CPP_TYPE_ARRAY or Il2CppTypeEnum.IL2CPP_TYPE_GENERICINST or Il2CppTypeEnum.IL2CPP_TYPE_TYPEDBYREF or Il2CppTypeEnum.IL2CPP_TYPE_I or Il2CppTypeEnum.IL2CPP_TYPE_U or Il2CppTypeEnum.IL2CPP_TYPE_OBJECT or Il2CppTypeEnum.IL2CPP_TYPE_SZARRAY or Il2CppTypeEnum.IL2CPP_TYPE_MVAR => "i",
                    _ => throw new NotSupportedException(),
                };
            }
            return signature;
        }

        private Il2CppTypeDefinition GetTypeDefinition(Il2CppType il2CppType)
        {
            switch (il2CppType.type)
            {
                case Il2CppTypeEnum.IL2CPP_TYPE_VOID:
                case Il2CppTypeEnum.IL2CPP_TYPE_BOOLEAN:
                case Il2CppTypeEnum.IL2CPP_TYPE_CHAR:
                case Il2CppTypeEnum.IL2CPP_TYPE_I1:
                case Il2CppTypeEnum.IL2CPP_TYPE_U1:
                case Il2CppTypeEnum.IL2CPP_TYPE_I2:
                case Il2CppTypeEnum.IL2CPP_TYPE_U2:
                case Il2CppTypeEnum.IL2CPP_TYPE_I4:
                case Il2CppTypeEnum.IL2CPP_TYPE_U4:
                case Il2CppTypeEnum.IL2CPP_TYPE_I8:
                case Il2CppTypeEnum.IL2CPP_TYPE_U8:
                case Il2CppTypeEnum.IL2CPP_TYPE_R4:
                case Il2CppTypeEnum.IL2CPP_TYPE_R8:
                case Il2CppTypeEnum.IL2CPP_TYPE_STRING:
                case Il2CppTypeEnum.IL2CPP_TYPE_TYPEDBYREF:
                case Il2CppTypeEnum.IL2CPP_TYPE_I:
                case Il2CppTypeEnum.IL2CPP_TYPE_U:
                case Il2CppTypeEnum.IL2CPP_TYPE_VALUETYPE:
                case Il2CppTypeEnum.IL2CPP_TYPE_CLASS:
                case Il2CppTypeEnum.IL2CPP_TYPE_OBJECT:
                    return executor.GetTypeDefinitionFromIl2CppType(il2CppType);
                case Il2CppTypeEnum.IL2CPP_TYPE_GENERICINST:
                    var genericClass = il2Cpp.MapVATR<Il2CppGenericClass>(il2CppType.data.generic_class);
                    return executor.GetGenericClassTypeDefinition(genericClass);
                default:
                    throw new NotSupportedException();
            }
        }

        private void CreateStructNameDic(Il2CppTypeDefinition typeDef)
        {
            var typeName = executor.GetTypeDefName(typeDef, true, true);
            var typeStructName = FixName(typeName);
            var uniqueName = GetUniqueName(typeStructName);
            structNameDic.Add(typeDef, uniqueName);
        }

        private string GetUniqueName(string name)
        {
            var fixName = name;
            int i = 1;
            while (!structNameHashSet.Add(fixName))
            {
                fixName = $"{name}_{i++}";
            }
            return fixName;
        }

        private string GetIl2CppStructName(Il2CppType il2CppType, Il2CppGenericContext context = null)
        {
            switch (il2CppType.type)
            {
                case Il2CppTypeEnum.IL2CPP_TYPE_VOID:
                case Il2CppTypeEnum.IL2CPP_TYPE_BOOLEAN:
                case Il2CppTypeEnum.IL2CPP_TYPE_CHAR:
                case Il2CppTypeEnum.IL2CPP_TYPE_I1:
                case Il2CppTypeEnum.IL2CPP_TYPE_U1:
                case Il2CppTypeEnum.IL2CPP_TYPE_I2:
                case Il2CppTypeEnum.IL2CPP_TYPE_U2:
                case Il2CppTypeEnum.IL2CPP_TYPE_I4:
                case Il2CppTypeEnum.IL2CPP_TYPE_U4:
                case Il2CppTypeEnum.IL2CPP_TYPE_I8:
                case Il2CppTypeEnum.IL2CPP_TYPE_U8:
                case Il2CppTypeEnum.IL2CPP_TYPE_R4:
                case Il2CppTypeEnum.IL2CPP_TYPE_R8:
                case Il2CppTypeEnum.IL2CPP_TYPE_STRING:
                case Il2CppTypeEnum.IL2CPP_TYPE_TYPEDBYREF:
                case Il2CppTypeEnum.IL2CPP_TYPE_I:
                case Il2CppTypeEnum.IL2CPP_TYPE_U:
                case Il2CppTypeEnum.IL2CPP_TYPE_VALUETYPE:
                case Il2CppTypeEnum.IL2CPP_TYPE_CLASS:
                case Il2CppTypeEnum.IL2CPP_TYPE_OBJECT:
                    {
                        var typeDef = executor.GetTypeDefinitionFromIl2CppType(il2CppType);
                        return structNameDic[typeDef];
                    }
                case Il2CppTypeEnum.IL2CPP_TYPE_PTR:
                    {
                        var oriType = il2Cpp.GetIl2CppType(il2CppType.data.type);
                        return GetIl2CppStructName(oriType);
                    }
                case Il2CppTypeEnum.IL2CPP_TYPE_ARRAY:
                    {
                        var arrayType = il2Cpp.MapVATR<Il2CppArrayType>(il2CppType.data.array);
                        var elementType = il2Cpp.GetIl2CppType(arrayType.etype);
                        var elementStructName = GetIl2CppStructName(elementType, context);
                        var typeStructName = elementStructName + "_array";
                        structNameHashSet.Add(typeStructName);
                        return typeStructName;
                    }
                case Il2CppTypeEnum.IL2CPP_TYPE_SZARRAY:
                    {
                        var elementType = il2Cpp.GetIl2CppType(il2CppType.data.type);
                        var elementStructName = GetIl2CppStructName(elementType, context);
                        var typeStructName = elementStructName + "_array";
                        structNameHashSet.Add(typeStructName);
                        return typeStructName;
                    }
                case Il2CppTypeEnum.IL2CPP_TYPE_GENERICINST:
                    {
                        var typeStructName = genericClassStructNameDic[il2CppType.data.generic_class];
                        structNameHashSet.Add(typeStructName);
                        return typeStructName;
                    }
                case Il2CppTypeEnum.IL2CPP_TYPE_VAR:
                    {
                        if (context != null)
                        {
                            var genericParameter = executor.GetGenericParameteFromIl2CppType(il2CppType);
                            var genericInst = il2Cpp.MapVATR<Il2CppGenericInst>(context.class_inst);
                            var pointers = il2Cpp.MapVATR<ulong>(genericInst.type_argv, genericInst.type_argc);
                            var pointer = pointers[genericParameter.num];
                            var type = il2Cpp.GetIl2CppType(pointer);
                            return GetIl2CppStructName(type);
                        }
                        return "System_Object";
                    }
                case Il2CppTypeEnum.IL2CPP_TYPE_MVAR:
                    {
                        if (context != null)
                        {
                            var genericParameter = executor.GetGenericParameteFromIl2CppType(il2CppType);
                            var genericInst = il2Cpp.MapVATR<Il2CppGenericInst>(context.method_inst);
                            var pointers = il2Cpp.MapVATR<ulong>(genericInst.type_argv, genericInst.type_argc);
                            var pointer = pointers[genericParameter.num];
                            var type = il2Cpp.GetIl2CppType(pointer);
                            return GetIl2CppStructName(type);
                        }
                        return "System_Object";
                    }
                default:
                    throw new NotSupportedException();
            }
        }

    }
}
