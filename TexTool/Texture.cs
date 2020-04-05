using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using Squish;

namespace TexTool
{
    public class Texture
    {
        private enum TextureFormat
        {
            Argb32 = 5,
            Rgb24 = 3,
            Dxt1 = 10,
            Dxt5 = 12
        }

        private const string TEX_TAG = "CM3D2_TEX";

        private static readonly Dictionary<TextureFormat, Action<string, byte[], int, int, TextureFormat>>
            TextureLoaders = new Dictionary<TextureFormat, Action<string, byte[], int, int, TextureFormat>>
            {
                [TextureFormat.Argb32] = ConvertFromPng,
                [TextureFormat.Rgb24] = ConvertFromPng,
                [TextureFormat.Dxt1] = ConvertFromDxt,
                [TextureFormat.Dxt5] = ConvertFromDxt
            };

        public static void Convert(string fileName)
        {
            if (!File.Exists(fileName))
                throw new FileNotFoundException("Texture file not found", fileName);

            var ext = Path.GetExtension(fileName);

            if (ext == ".tex")
                TexToImg(fileName);
            else
                ImgToTex(fileName);
        }

        private static void ImgToTex(string texFileName)
        {
            using var img = Image.FromFile(texFileName);

            var rects = new List<Rect>();
            var rectsPath = $"{texFileName}.uv.csv";

            if (File.Exists(rectsPath))
                foreach (var line in File.ReadAllLines(rectsPath))
                {
                    var textLine = line.Trim();
                    if (textLine.Length == 0)
                        continue;

                    var parts = textLine.Split(new[] {';'}, StringSplitOptions.RemoveEmptyEntries);

                    if (parts.Length != 4)
                        continue;

                    try
                    {
                        rects.Add(new Rect
                        {
                            X = float.Parse(parts[0], CultureInfo.InvariantCulture),
                            Y = float.Parse(parts[1], CultureInfo.InvariantCulture),
                            Width = float.Parse(parts[2], CultureInfo.InvariantCulture),
                            Height = float.Parse(parts[3], CultureInfo.InvariantCulture)
                        });
                    }
                    catch (Exception)
                    {
                        // ignored
                    }
                }

            using var ms = new MemoryStream();
            img.Save(ms, ImageFormat.Png);
            var data = ms.ToArray();

            var dirName = Path.GetDirectoryName(texFileName) ?? ".";
            var fileName = Path.GetFileNameWithoutExtension(texFileName);
            if (ShouldRenameTarget(fileName, "tex", out var movedFileName))
                File.Move(Path.Combine(dirName, $"{fileName}.tex"), Path.Combine(dirName, movedFileName));

            var outputName = Path.Combine(dirName, $"{fileName}.tex");
            using var bw = new BinaryWriter(File.Create(outputName));

            bw.Write(TEX_TAG);
            bw.Write(rects.Count > 0 ? 1011 : 1010);
            bw.Write(string.Empty);

            if (rects.Count > 0)
            {
                bw.Write(rects.Count);
                foreach (var rect in rects)
                {
                    bw.Write(rect.X);
                    bw.Write(rect.Y);
                    bw.Write(rect.Width);
                    bw.Write(rect.Height);
                }
            }

            bw.Write(img.Width);
            bw.Write(img.Height);
            bw.Write((int) TextureFormat.Argb32);
            bw.Write(data.Length);
            bw.Write(data);
        }

        private static void TexToImg(string texFileName)
        {
            using var br = new BinaryReader(File.OpenRead(texFileName));
            var tag = br.ReadString();

            if (tag != TEX_TAG)
            {
                Console.WriteLine($"File {texFileName} is not a valid TEX file!");
                return;
            }

            var version = br.ReadInt32();
            br.ReadString();
            var width = 0;
            var height = 0;
            Rect[] rects = null;

            var format = TextureFormat.Argb32;

            if (version >= 1010)
            {
                if (version >= 1011)
                {
                    var rectCount = br.ReadInt32();
                    rects = new Rect[rectCount];

                    for (var i = 0; i < rectCount; i++)
                        rects[i] = new Rect
                        {
                            X = br.ReadSingle(),
                            Y = br.ReadSingle(),
                            Width = br.ReadSingle(),
                            Height = br.ReadSingle()
                        };
                }

                width = br.ReadInt32();
                height = br.ReadInt32();
                format = (TextureFormat) br.ReadInt32();
            }

            if (!Enum.IsDefined(typeof(TextureFormat), format))
            {
                Console.WriteLine($"File {texFileName} has unsupported texture format: {format}");
                return;
            }

            var size = br.ReadInt32();
            var data = new byte[size];
            br.Read(data, 0, size);

            if (version == 1000)
            {
                width = (data[16] << 24) | (data[17] << 16) | (data[18] << 8) | data[19];
                height = (data[20] << 24) | (data[21] << 16) | (data[22] << 8) | data[23];
            }

            var dirName = Path.GetDirectoryName(texFileName) ?? ".";
            var fileName = Path.GetFileNameWithoutExtension(texFileName);
            if (ShouldRenameTarget(fileName, "png", out var movedFileName))
            {
                var oldTarget = Path.Combine(dirName, $"{fileName}.png");
                var newTarget = Path.Combine(dirName, movedFileName);
                File.Move(oldTarget, newTarget);
                var uvRectsPath = $"{fileName}.uv.csv";
                if (File.Exists(uvRectsPath)) File.Move(uvRectsPath, $"{newTarget}.uv.csv");
            }

            var outputName = Path.Combine(dirName, $"{fileName}.png");

            if (TextureLoaders.TryGetValue(format, out var saveTex))
                saveTex(outputName, data, width, height, format);
            else
                Console.WriteLine($"File {texFileName} uses format {format} that is not supported!");

            if (rects == null)
                return;
            using var uvFile = File.CreateText($"{outputName}.uv.csv");

            foreach (var rect in rects)
                uvFile.WriteLine($"{rect.X}; {rect.Y}; {rect.Width}; {rect.Height}");
        }

        private static bool ShouldRenameTarget(string fileNameNoExt, string newExt, out string movedFileName)
        {
            var ext = Path.GetExtension(fileNameNoExt);

            if (!string.IsNullOrEmpty(ext) && int.TryParse(ext.Substring(1), out _))
                fileNameNoExt = Path.GetFileNameWithoutExtension(fileNameNoExt);

            var attempt = 0;
            movedFileName = $"{fileNameNoExt}.{newExt}";

            while (File.Exists(movedFileName))
                movedFileName = $"{fileNameNoExt}.{++attempt}.{newExt}";

            return attempt != 0;
        }

        private static void ConvertFromPng(string file, byte[] data, int width, int height, TextureFormat format)
        {
            using var ms = new MemoryStream(data);
            using var img = Image.FromStream(ms);
            img.Save(file);
        }

        private static void ConvertFromDxt(string file, byte[] data, int width, int height, TextureFormat format)
        {
            var squishFlags =
                format switch
                {
                    TextureFormat.Dxt1 => SquishFlags.kDxt1,
                    TextureFormat.Dxt5 => SquishFlags.kDxt5,
                    _ => throw new ArgumentException($"Invalid DXT texture format: {format}", nameof(format))
                };

            var outData = new byte[width * height * 4];
            Squish.Squish.DecompressImage(outData, width, height, ref data, squishFlags);

            for (var i = 0; i < width * height; i++)
            {
                var r = outData[i * 4];
                outData[i * 4] = outData[i * 4 + 2];
                outData[i * 4 + 2] = r;
            }

            var gch = GCHandle.Alloc(outData, GCHandleType.Pinned);
            var img = new Bitmap(width, height, width * 4, PixelFormat.Format32bppArgb, gch.AddrOfPinnedObject());
            img.RotateFlip(RotateFlipType.RotateNoneFlipY);

            img.Save(file);

            img.Dispose();
            gch.Free();
        }

        public class Rect
        {
            public float X { get; set; }
            public float Y { get; set; }
            public float Width { get; set; }
            public float Height { get; set; }
        }
    }
}