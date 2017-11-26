using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using ManagedSquish;

namespace TexTool
{
    public enum TextureFormat
    {
        ARGB32 = 5,
        RGB24 = 3,
        DXT1 = 10,
        DXT5 = 12
    }

    public class Texture : IDisposable
    {
        public delegate Texture TextureLoader(byte[] data, int width, int height, TextureFormat format);

        private const int OUTPUT_TEX_VERSION = 1010;

        public const string TEX_EXTENSION = ".tex";
        private const string TEX_TAG = "CM3D2_TEX";

        private static readonly Dictionary<TextureFormat, TextureLoader> TextureLoaders = new Dictionary<TextureFormat, TextureLoader>
        {
            {TextureFormat.ARGB32, LoadFromMemoryGdi},
            {TextureFormat.RGB24, LoadFromMemoryGdi},
            {TextureFormat.DXT1, LoadFromMemoryDxt},
            {TextureFormat.DXT5, LoadFromMemoryDxt}
        };

        // ReSharper disable once PrivateFieldCanBeConvertedToLocalVariable
        // ReSharper disable once FieldCanBeMadeReadOnly.Local
        private byte[] bgra;
        private GCHandle handle;
        private readonly Image image;

        private Texture(Image img)
        {
            image = img;
            InternalPath = string.Empty;
        }

        private Texture(byte[] bgra, int width, int height)
        {
            this.bgra = bgra;
            handle = GCHandle.Alloc(this.bgra, GCHandleType.Pinned);
            image = new Bitmap(width, height, width * 4, PixelFormat.Format32bppArgb, handle.AddrOfPinnedObject());
            InternalPath = string.Empty;
        }

        public string InternalPath { get; set; }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public void Save(string file)
        {
            if(image == null)
                throw new InvalidOperationException("There is no image bound to the object!");

            string ext = Path.GetExtension(file);
            if (ext == TEX_EXTENSION)
                SaveTex(file);
            else
                SaveImage(file);
        }

        public void SaveImage(string file)
        {
            image.Save(file);
        }

        public void SaveTex(string file)
        {
            MemoryStream ms = new MemoryStream();
            image.Save(ms, ImageFormat.Png);
            byte[] data = ms.ToArray();

            using (BinaryWriter bw = new BinaryWriter(File.Create(file)))
            {
                bw.Write(TEX_TAG);
                bw.Write(OUTPUT_TEX_VERSION);
                bw.Write(InternalPath);
                bw.Write(image.Width);
                bw.Write(image.Height);
                bw.Write((int) TextureFormat.ARGB32);
                bw.Write(data.Length);
                bw.Write(data);
            }
        }

        public static Texture Open(string filename)
        {
            if (!File.Exists(filename))
                throw new FileNotFoundException($"The path {filename} is not a valid file!");

            string ext = Path.GetExtension(filename);
            return ext == TEX_EXTENSION ? OpenTex(filename) : OpenImage(filename);
        }

        private static Texture OpenTex(string filename)
        {
            using (FileStream sw = File.OpenRead(filename))
            {
                using (BinaryReader br = new BinaryReader(sw))
                {
                    string text = br.ReadString();
                    if (text != TEX_TAG)
                        throw new FileLoadException($"File {filename} is not a valid CM3D2_TEX texture");

                    int version = br.ReadInt32();
                    string originalPath = br.ReadString();
                    int width = 0;
                    int height = 0;
                    TextureFormat texFormat = TextureFormat.ARGB32;

                    if (version >= 1010)
                    {
                        width = br.ReadInt32();
                        height = br.ReadInt32();
                        texFormat = (TextureFormat) br.ReadInt32();
                    }

                    if (!Enum.IsDefined(typeof(TextureFormat), texFormat))
                        throw new FileLoadException($"TexTool does not support texture format {(int) texFormat}");

                    int size = br.ReadInt32();
                    byte[] data = new byte[size];
                    br.Read(data, 0, size);

                    if (version == 1000)
                    {
                        width = (data[16] << 24) | (data[17] << 16) | (data[18] << 8) | data[19];
                        height = (data[20] << 24) | (data[21] << 16) | (data[22] << 8) | data[23];
                    }

                    Texture tex;
                    if (TextureLoaders.TryGetValue(texFormat, out var loader))
                    {
                        tex = loader(data, width, height, texFormat);
                        tex.InternalPath = originalPath;
                    }
                    else
                    {
                        throw new FileLoadException($"Loader for format {texFormat} is not yet implemented.");
                    }
                    return tex;
                }
            }
        }

        private static Texture LoadFromMemoryDxt(byte[] data, int width, int height, TextureFormat format)
        {
            SquishFlags flags;
            switch (format)
            {
                case TextureFormat.DXT1:
                    flags = SquishFlags.Dxt1;
                    break;
                case TextureFormat.DXT5:
                    flags = SquishFlags.Dxt5;
                    break;
                default: throw new FormatException("The texture format is not a DXT format.");
            }
            byte[] rgba = Squish.DecompressImage(data, width, height, flags);
            // Fix to BGRA
            for (int i = 0; i < width * height; i++)
            {
                byte r = rgba[i * 4];
                rgba[i * 4] = rgba[i * 4 + 2];
                rgba[i * 4 + 2] = r;
            }

            Texture tex = new Texture(rgba, width, height);
            // CM3D2 textures should be flipped
            tex.image.RotateFlip(RotateFlipType.RotateNoneFlipY);
            return tex;
        }

        private static Texture LoadFromMemoryGdi(byte[] data, int width, int height, TextureFormat format)
        {
            Image i;
            using (MemoryStream ms = new MemoryStream(data))
                i = Image.FromStream(ms);

            return new Texture(i);
        }

        private static Texture OpenImage(string filename)
        {
            return new Texture(Image.FromFile(filename));
        }

        private void ReleaseUnmanagedResources()
        {
            if (handle.IsAllocated)
                handle.Free();
        }

        private void Dispose(bool disposing)
        {
            ReleaseUnmanagedResources();
            if (disposing)
                image?.Dispose();
        }

        ~Texture()
        {
            Dispose(false);
        }
    }
}