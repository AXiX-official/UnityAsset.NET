# UnityAsset.NET

[![MIT](https://img.shields.io/github/license/AXiX-official/UnityAsset.NET)](https://github.com/AXiX-official/UnityAsset.NET/master/LICENSE)
[![NuGet Stats](https://img.shields.io/nuget/v/UnityAsset.NET.svg)](https://www.nuget.org/packages/UnityAsset.NET)

A .NET library for reading and modifying Unity assets and bundles.

## Features

For now, it can only do a few simple things in the outer layer of the bundlefile

- Read and write uncompressed/lz4-compressed/lzma-compressed Unity bundlefile
- Handling UnityCN encryption
- Calculate and fix the CRC32 value of bundlefile

## Acknowledgements

This project uses code from the following open source projects:

- [Studio](https://github.com/RazTools/Studio) by [Razmoth](https://github.com/RazTools)
- [AssetStudio](https://github.com/aelurum/AssetStudio) by [aelurum](https://github.com/aelurum)
- [AssetsTools.NET](https://github.com/nesrak1/AssetsTools.NET) by [nesrak1](https://github.com/nesrak1)
- [UnityPy](https://github.com/K0lb3/UnityPy) by [K0lb3](https://github.com/K0lb3)

We are grateful to the developers of these projects for their work.