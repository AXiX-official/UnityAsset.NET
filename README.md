# UnityAsset.NET

---

[![MIT](https://img.shields.io/github/license/AXiX-official/UnityAsset.NET)](https://github.com/AXiX-official/UnityAsset.NET/master/LICENSE)
[![NuGet Stats](https://img.shields.io/nuget/v/UnityAsset.NET.svg)](https://www.nuget.org/packages/UnityAsset.NET)

A work-in-progress .NET library for parsing/serializing/patching Unity Engine asset files.

Only support Unity 2017.x or later.

## Features

---

### BundleFile

- [x] Parse/Serialize
- [ ] Patch
- [x] Calculate/Patch crc32

### SerializedFile

- [x] Parse/Serialize
- [ ] Patch

### Asset

- [x] Parse/Serialize
- [ ] Patch

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

To remove Unity CN Encryption form File, you can simply save `BundleFile` without key
```csharp
bf.Serialize(@"path to save file", CompressionType.Lz4HC, CompressionType.Lz4HC);
```

### Stripped Version

Some `BundleFile`'s version may be stripped, to load those file you can set a specific version
```csharp
Setting.DefaultUnityVerion = "2020.3.48f1"
```

## Credits

---

This project stands on the shoulders of these amazing open-source projects:

- [Studio](https://github.com/RazTools/Studio) by [Razmoth](https://github.com/RazTools)
- [AssetStudio](https://github.com/aelurum/AssetStudio) by [aelurum](https://github.com/aelurum)
- [AssetsTools.NET](https://github.com/nesrak1/AssetsTools.NET) by [nesrak1](https://github.com/nesrak1)
- [UnityPy](https://github.com/K0lb3/UnityPy) by [K0lb3](https://github.com/K0lb3)
- [RustyAssetBundleEXtractor](https://github.com/UniversalGameExtraction/RustyAssetBundleEXtractor) by [UniversalGameExtraction](https://github.com/UniversalGameExtraction)