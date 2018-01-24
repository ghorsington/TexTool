#include "stdafx.h"
#include "Texture.h"
#include "../squish/squish.h"

using namespace System::Drawing;
using namespace System::Drawing::Imaging;
using namespace System::Runtime::InteropServices;
using namespace System::IO;
using namespace System::Collections::Generic;
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
	}

	Texture::Texture(squish::u8* bgra, int width, int height)
	{
		this->_internalPath = String::Empty;
		this->bgra = bgra;
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

		if (version >= 1010)
		{
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
		}
		else
		{
			throw gcnew FileLoadException(String::Format("Loader for format {0} is not yet implemented.", texFormat));
		}

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
		default: throw gcnew FormatException("The texture format is not a DXT format.");
		}

		auto rgba = new squish::u8[squish::GetStorageRequirements(width, height, squishFlags)];
		pin_ptr<unsigned char> dPtr = &data[0];
		squish::DecompressImage(0, width, height, (void*)dPtr, squishFlags);

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
	}

	void Texture::SaveTex(String^ file)
	{
		MemoryStream^ ms = gcnew MemoryStream();
		image->Save(ms, ImageFormat::Png);
		array<unsigned char>^ data = ms->ToArray();

		BinaryWriter^ bw = gcnew BinaryWriter(File::Create(file));

		bw->Write(TEX_TAG);
		bw->Write(OUTPUT_TEX_VERSION);
		bw->Write(InternalPath);
		bw->Write(image->Width);
		bw->Write(image->Height);
		bw->Write((int)TextureFormat::ARGB32);
		bw->Write(data->Length);
		bw->Write(data);

		delete bw;
		delete ms;
	}
}