# TexTool

## Purpose

### Tool

This tool can be used to convert between CM3D2's and COM3D2's TEX image format and PNG.

The tool supports CM3D2 TEX version 1000 to 1010 format.
The following pixel formats are supported:

* ARGB32
* RGB24
* DXT1
* DXT5

These are the all formats used by current version of CM3D2 and COM3D2.
More formats are to be added as need be.

### Library

The one-class library/utility can be used to programmatically read and write CM3D2 TEX files.
The library (the `Texture` class) is a simple wrapper for the .NET's `Image` class.
The wrapper is used for memory management when loading raw pixel data.

Expect a simple documentation in the nearest future.

## How to use the tool

### Without command prompt

Dobule click to view a mini-guide.
Drag and drop files or folders onto the executable to convert.

All output is added to `output` folder.

### With command prompt

> `TexTool.exe <files or folders>`

Of course, replace `<files or folders>` with paths to files or folders separated by a space.
Use double quotes if a path contains spaces.

The output is put in the same folder as the input file.

Currently the tool does not contain any special flags, but that might change in future.
