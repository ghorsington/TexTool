#pragma once
#include "../squish/squish.h"

using namespace System::Drawing;
using namespace System::Drawing::Imaging;
using namespace System::Runtime::InteropServices;
using namespace System::IO;
using namespace System::Collections::Generic;
using namespace System;

namespace TexTool
{
	public enum class TextureFormat
	{
		ARGB32 = 5,
		RGB24 = 3,
		DXT1 = 10,
		DXT5 = 12
	};

	public ref class Texture
	{
		public:
			literal String^ TEX_EXTENSION = ".tex";
			delegate Texture^ TextureLoader(array<unsigned char>^ data, int width, int height, TextureFormat format);

		static Texture();

		~Texture();
		!Texture();

		void Save(String^ file);
		void SaveImage(String^ file);
		void SaveTex(String^ file);

		static Texture^ Open(String^ filename);

		property String^ InternalPath
		{
			String^ get()
			{
				return _internalPath;
			}

			void set(String^ val)
			{
				_internalPath = val;
			}
		}

		private:
			literal String^ TEX_TAG = "CM3D2_TEX";
			literal int OUTPUT_TEX_VERSION = 1010;
			static initonly Dictionary<TextureFormat, TextureLoader^>^ TextureLoaders;
			String^ _internalPath;
			Image ^ image;
			squish::u8* bgra;

			Texture(Image^ image);
			Texture(squish::u8* bgra, int width, int height);

			static Texture^ OpenTex(String^ filename);
			static Texture^ OpenImage(String^ filename);

			static Texture^ LoadFromMemoryDxt(array<unsigned char>^ data, int width, int height, TextureFormat format);
			static Texture^ LoadFromMemoryGdi(array<unsigned char>^ data, int width, int height, TextureFormat format);

	};
}
