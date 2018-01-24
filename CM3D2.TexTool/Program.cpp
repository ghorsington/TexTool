// CM3D2.TexTool.cpp : main project file.

#include "stdafx.h"
#include "Texture.h"
#include "help.h"

using namespace System;
using namespace System::Reflection;
using namespace TexTool;
using namespace System::Windows::Forms;

namespace TexTool
{
    public ref class Program
    {
        public:
            static String^ VersionNumber = Assembly::GetExecutingAssembly()->GetName()->Version->ToString();
            static String^ ProcessName = System::Diagnostics::Process::GetCurrentProcess()->ProcessName;

            void PrintHelp()
            {
                MessageBox::Show(String::Format(HELP, VersionNumber, ProcessName), "Info", MessageBoxButtons::OK, MessageBoxIcon::Information);
            }

            void Process(array<String^>^ paths)
            {
                for each(String^% path in paths)
                {
                    if (File::Exists(path))
                        ProcessFile(path);
                    else if (Directory::Exists(path))
                        ProcessDirectory(path);
                    else
                        Console::WriteLine(String::Format("[WARN] {0} is not a valid file nor directory (does it exist? can it be accessed?)", path));
                }
            }

            void ProcessFile(String^ file)
            {
                Texture^ tex;
                auto fileName = Path::GetFileNameWithoutExtension(file);
                auto ext = Path::GetExtension(file)->ToLowerInvariant();

                try
                {
                    tex = Texture::Open(file);
                }
                catch (OutOfMemoryException^)
                {
                    Console::WriteLine(String::Format("Skipping {0}{1}: Not an image (or format not supported)", fileName, ext));
                    return;
                }
                catch (Exception^ ex)
                {
                    Console::WriteLine(String::Format("[FAIL] Cannot parse {0}{1}: {2}", fileName, ext, ex->Message));
                    return;
                }

                auto newExt = gcnew String(ext == Texture::TEX_EXTENSION ? ".png" : ".tex");
                auto newName = fileName;
                int i = 0;

                while (File::Exists(String::Format("{0}{1}", newName, newExt)))
                {
                    i++;
                    newName = fileName + (i > 0 ? i.ToString() : "");
                }

                try
                {
                    tex->Save(String::Format("{0}{1}", newName, newExt));
                }
                catch (Exception^ e)
                {
                    Console::WriteLine(String::Format("[FAIL] Failed to save {0}{1}: {2}", newName, newExt, e->Message));
                }

                delete tex;
            }

            void ProcessDirectory(String^ path)
            {
                for each(String^% file in Directory::EnumerateFiles(path, "*", SearchOption::AllDirectories))
                    ProcessFile(file);
            }
    };

}

int main(array<System::String ^> ^args)
{
    Program^ prog = gcnew Program();

    if (args->Length == 0)
    {
        prog->PrintHelp();
        return 0;
    }

    prog->Process(args);
    return 0;
}




