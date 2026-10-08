# Third-party components

The new port code is MIT-licensed under the root LICENSE. Retained third-party license texts are in `packaging/licenses` in source and `licenses` in the installer. The port's MIT license does not replace those terms or license original game assets.

| Component | Version / source | Notice |
| --- | --- | --- |
| .NET runtime | .NET 8, SDK pinned to 8.0.424 | DotNet.txt, DotNet-ThirdParty.txt |
| MonoGame / mgfxc | 3.8.4.1 | MonoGame.txt, MonoGame-ThirdParty.txt |
| Mono.Cecil | 0.11.6 | Mono.Cecil.txt |
| SharpDX | NuGet lock files | SharpDX.txt |
| XenosRecomp | hedge-dev/XenosRecomp, commit 990d03b28a27b50277ee5d8d942e1c5f873869d1 | XenosRecomp.txt |
| Shader compiler and translator dependencies | Pinned XenosRecomp submodules | DirectXShaderCompiler.txt, fmt.txt, smol-v.txt, xxHash.txt, zstd.txt |
| STFS extraction reference | Technical reference consulted during implementation | LICENSE-Extract-STFS.txt |

MonoGame is modified locally during packaging: three XACT readers, two XNA SpriteBatch overloads and the SpriteBatch.Begin transform bridge are adapted by `tools/Tka.AssemblyTool/RuntimeAdapter.cs`. Its original hash is checked before edits. All patch code is included in source. NuGet lock files record managed dependencies. Shader compiler DLL placement is retained.

Game assets and the original game assembly are supplied by the user and transformed only inside their local installation. They are not part of either release archive.
