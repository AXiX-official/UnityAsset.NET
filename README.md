# UnityAsset.NET

---

[![MIT](https://img.shields.io/github/license/AXiX-official/UnityAsset.NET)](https://github.com/AXiX-official/UnityAsset.NET/master/LICENSE)
[![NuGet Stats](https://img.shields.io/nuget/v/UnityAsset.NET.svg)](https://www.nuget.org/packages/UnityAsset.NET)

> 🚨 Major Breaking Changes in v0.2.0 🚨
>
> This version introduces a complete, low-level refactoring with major breaking changes.
> The API is not compatible with older versions (v0.1.x). 
> This update aims to build a foundation that is more performant, type-safe, and provides a cleaner API.
> Please read the notes below carefully before upgrading.

A .NET library undergoing active refactoring, currently focused on high-performance parsing and reading of Unity Engine asset files.

Only support Unity 2017.x or later.

## Features

---

### BundleFile

- [x] Parsing and Reading
- [ ] ~~Serialization~~ (Temporarily removed, will be re-introduced with a new API in a future version)
- [ ] ~~Patching~~ (Will be re-introduced in a future version)
- [ ] Calculate/Patch crc32

### SerializedFile

- [x] Parsing and Reading
- [ ] ~~Serialization~~ (Temporarily removed)
- [ ] ~~Patching~~ (Temporarily removed)

### Asset

- [x] Parsing based on TypeTree
- [ ] ~~Serialization~~ (Temporarily removed)
- [ ] ~~Patching~~ (Temporarily removed)

## Examples

---

### Unity CN Decryption

To load `BundleFile` with Unity CN Encryption, there are two ways.
```csharp
// Hex string format (32 characters)
Setting.DefaultUnityCNKey = "587865636f6472506547616b61326536";  // Represents "XxecodrPeGaka2e6" in hex
BundleFile bf = new BundleFile( @"path to your bundlefile");
```
or
```csharp
// Plain string format (16 characters)
BundleFile bf = new BundleFile( @"path to your bundlefile", "XxecodrPeGaka2e6");
```

~~To remove Unity CN Encryption form File, you can simply save `BundleFile` without key~~
```csharp
// Temporarily removed
bf.Serialize(@"path to save file", CompressionType.Lz4HC, CompressionType.Lz4HC);
```

### Stripped Version

Some `BundleFile`'s version may be stripped, to load those file you can set a specific version
```csharp
Setting.DefaultUnityVerion = "2020.3.48f1"
```

## Roadmap

- [ ] v0.3: More Asset Class Interface.
- [ ] v0.4: Re-architect and re-implement a robust and flexible serialization API.
- [ ] v0.5: Re-introduce patching capabilities based on the new object model.

## Credits

---

This project stands on the shoulders of these amazing open-source projects:

- [Studio](https://github.com/RazTools/Studio) by [Razmoth](https://github.com/RazTools)
- [AssetStudio](https://github.com/aelurum/AssetStudio) by [aelurum](https://github.com/aelurum)
- [AssetsTools.NET](https://github.com/nesrak1/AssetsTools.NET) by [nesrak1](https://github.com/nesrak1)
- [UnityPy](https://github.com/K0lb3/UnityPy) by [K0lb3](https://github.com/K0lb3)
- [RustyAssetBundleEXtractor](https://github.com/UniversalGameExtraction/RustyAssetBundleEXtractor) by [UniversalGameExtraction](https://github.com/UniversalGameExtraction)