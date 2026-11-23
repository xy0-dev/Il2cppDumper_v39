# Il2cppDumper_v39

A trimmed Il2CppDumper fork. **Metadata version 39 only** - any other
`global-metadata.dat` version is rejected.

It produces exactly two files:

- `dump.cs` (C# pseudo-code of all types/methods/fields)
- `script.json` (addresses and signatures for IDA/Ghidra)

No dummy DLL output yet.

## Usage

```
Il2CppDumper.exe <executable-or-libil2cpp> <global-metadata.dat> [output-dir]
```

## Build

```
dotnet build -c Release
```

Requires .NET 8