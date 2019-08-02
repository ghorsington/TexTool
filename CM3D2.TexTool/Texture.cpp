#include "stdafx.h"
#include "Texture.h"
#include "../squish/squish.h"

using namespace System::Drawing;
using namespace System::Drawing::Imaging;
using namespace System::Runtime::InteropServices;
using namespace System::IO;
using namespace System::Collections::Generic;
using namespace System::Globalization;
using namespace System;

namespace TexTool
{
    static Texture::Texture()
    {
        TextureLoaders = gcnew Dictionary<TextureFormat, TextureLoader^>();

        TextureLoaders->Add(TextureFormat::ARGB32, gcnew TextureLoader(LoadFromMemoryGdi));
        TextureLoaders->Add(TextureFormat::RGB24, gcnew TextureLoader(LoadFromMemoryGdi));
        TextureLoaders->Add(TextureFormat::DXT1, gcnew TextureLoader(LoadFromMemoryDxt));
        TextureLoaders->Add(TextureFormat::DXT5, gcnew TextureLoader(LoadFromMemoryDxt));
    }

    Texture::Texture(Image^ image)
    {
        this->_internalPath = String::Empty;
        this->image = image;
		this->uvRects = nullptr;
    }

    Texture::Texture(squish::u8* bgra, int width, int height)
    {
        this->_internalPath = String::Empty;
        this->bgra = bgra;
		this->uvRects = nullptr;
        image = gcnew Bitmap(width, height, width * 4, PixelFormat::Format32bppArgb, IntPtr(bgra));
        InternalPath = String::Empty;
    }

    Texture::!Texture()
    {
        this->~Texture();
    }

    Texture::~Texture()
    {
        if (bgra != 0)
        {
            delete bgra;
            bgra = 0;
        }

        if (image != nullptr)
            delete image;
    }

    Texture^ Texture::Open(String^ filename)
    {
        if (!File::Exists(filename))
            throw gcnew FileNotFoundException(String::Format("The path {0} is not a valid file!", filename));

        String^ ext = Path::GetExtension(filename);
        return ext == TEX_EXTENSION ? OpenTex(filename) : OpenImage(filename);
    }

    Texture^ Texture::OpenImage(String^ filename)
    {
        return gcnew Texture(Image::FromFile(filename));
    }

    Texture^ Texture::OpenTex(String^ filename)
    {
        auto sw = File::OpenRead(filename);
        auto br = gcnew BinaryReader(sw);

        auto tag = br->ReadString();

        if (tag != TEX_TAG)
            throw gcnew FileLoadException(String::Format("File {0} is not a valid CM3D2_TEX texture", filename));

        auto version = br->ReadInt32();
        auto originalPath = br->ReadString();
        auto width = 0;
        auto height = 0;
        auto texFormat = TextureFormat::ARGB32;
		array<Rect^>^ rects = nullptr;

        if (version >= 1010)
        {
			if(version >= 1011)
			{
				auto rectCount = br->ReadInt32();

				rects = gcnew array<Rect^>(rectCount);

				for(int i = 0; i < rectCount; i++)
					rects[i] = gcnew Rect(br->ReadSingle(), br->ReadSingle(), br->ReadSingle(), br->ReadSingle());
			}
            width = br->ReadInt32();
            height = br->ReadInt32();
            texFormat = (TextureFormat)br->ReadInt32();
        }

        if (!Enum::IsDefined(TextureFormat::typeid, texFormat))
            throw gcnew FileLoadException(String::Format("TexTool does not support texture format {0}", (int)texFormat));

        auto size = br->ReadInt32();
        auto data = gcnew array<unsigned char>(size);
        br->Read(data, 0, size);

        if (version == 1000)
        {
            width = (data[16] << 24) | (data[17] << 16) | (data[18] << 8) | data[19];
            height = (data[20] << 24) | (data[21] << 16) | (data[22] << 8) | data[23];
        }

        Texture^ tex = nullptr;
        TextureLoader^ loader = nullptr;

        if (TextureLoaders->TryGetValue(texFormat, loader))
        {
            tex = loader(data, width, height, texFormat);
            tex->InternalPath = originalPath;
			tex->UvRects = rects;
        }
        else
            throw gcnew FileLoadException(String::Format("Loader for format {0} is not yet implemented.", texFormat));

        return tex;
    }

    Texture^ Texture::LoadFromMemoryDxt(array<unsigned char>^ data, int width, int height, TextureFormat format)
    {
        int squishFlags = 0;

        switch (format)
        {
            case TextureFormat::DXT1:
                squishFlags |= squish::kDxt1;
                break;

            case TextureFormat::DXT5:
                squishFlags |= squish::kDxt5;
				break;

            default:
                throw gcnew FormatException("The texture format is not a DXT format.");
        }

        auto rgba = new squish::u8[width * height * 4];
        pin_ptr<unsigned char> dPtr = &data[0];
        squish::DecompressImage(rgba, width, height, (void*)dPtr, squishFlags);

        for (int i = 0; i < width * height; i++)
        {
            auto r = rgba[i * 4];
            rgba[i * 4] = rgba[i * 4 + 2];
            rgba[i * 4 + 2] = r;
        }

        Texture^ tex = gcnew Texture(rgba, width, height);
        tex->image->RotateFlip(RotateFlipType::RotateNoneFlipY);
        return tex;
    }

    Texture^ Texture::LoadFromMemoryGdi(array<unsigned char>^ data, int width, int height, TextureFormat format)
    {
        auto ms = gcnew MemoryStream(data);
        auto img = Image::FromStream(ms);
        delete ms;

        return gcnew Texture(img);
    }

    void Texture::Save(String^ file)
    {
        if (image == nullptr)
            throw gcnew InvalidOperationException("There is no image bound to the object!");

        String^ ext = Path::GetExtension(file);

        if (ext == TEX_EXTENSION)
            SaveTex(file);
        else
            SaveImage(file);
    }

    void Texture::SaveImage(String^ file)
    {
        image->Save(file);

		if(uvRects != nullptr)
		{
			auto uvFilePath = Path::Combine(Path::GetDirectoryName(file), String::Format("{0}.uv.csv", Path::GetFileNameWithoutExtension(file)));
			auto uvFile = File::CreateText(uvFilePath);

			for each (auto rect in uvRects)
				uvFile->WriteLine(String::Format("{0}; {1}; {2}; {3}", rect->x, rect->y, rect->w, rect->h));
			uvFile->Flush();

			delete uvFile;
		}
    }

    void Texture::SaveTex(String^ file)
    {
        auto uvFilePath = Path::Combine(Path::GetDirectoryName(file), String::Format("{0}.uv.csv", Path::GetFileNameWithoutExtension(file)));

		auto uvList = gcnew List<Rect^>();

		if(File::Exists(uvFilePath))
		{
			for each (auto line in File::ReadAllLines(uvFilePath))
			{
				line = line->Trim();
				if (line->Length == 0)
					continue;

				auto parts = line->Split(gcnew array<wchar_t>{';'}, StringSplitOptions::RemoveEmptyEntries);

				if (parts->Length != 4)
					continue;

				try
				{
					auto ci = CultureInfo::InvariantCulture;
					uvList->Add(gcnew Rect(float::Parse(parts[0], ci), float::Parse(parts[1], ci), float::Parse(parts[2], ci), float::Parse(parts[3], ci)));
				} 
				catch(Exception^ e)
				{
					continue;
				}
			}
		}

    	MemoryStream^ ms = gcnew MemoryStream();
        image->Save(ms, ImageFormat::Png);
        array<unsigned char>^ data = ms->ToArray();

        BinaryWriter^ bw = gcnew BinaryWriter(File::Create(file));

        bw->Write(TEX_TAG);
        bw->Write(uvList->Count > 0 ? 1011 : 1010);
        bw->Write(InternalPath);
		if(uvList->Count > 0)
		{
			bw->Write(uvList->Count);
			for each (auto rect in uvList)
			{
				bw->Write(rect->x);
				bw->Write(rect->y);
				bw->Write(rect->w);
				bw->Write(rect->h);
			}
		}
        bw->Write(image->Width);
        bw->Write(image->Height);
        bw->Write((int)TextureFormat::ARGB32);
        bw->Write(data->Length);
        bw->Write(data);

        delete bw;
        delete ms;
    }
}
