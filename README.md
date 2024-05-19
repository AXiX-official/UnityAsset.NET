# UnityAsset.NET

A .NET library for reading and modifying Unity assets and bundles.

## Features

For now, it can only do a few simple things in the outer layer of the bundlefile

- Read and write uncompressed/lz4-compressed/lzma-compressed Unity bundlefile
- Handling UnityCN encryption
- Calculate and fix the CRC32 value of bundlefile

## Acknowledgements

This project uses code from the following open source projects:

- [Studio](https://github.com/RazTools/Studio) by Razmoth: Modded AssetStudio with new features.
- [AssetStudio](https://github.com/aelurum/AssetStudio) by aelurum: modified version of Perfare's AssetStudio.

We are grateful to the developers of these projects for their work.