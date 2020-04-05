using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace TexTool
{
    internal class Program
    {
        static void PrintHelp()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            var procname = Process.GetCurrentProcess().ProcessName;
            
            var sb = new StringBuilder();
            sb.AppendLine($"TexTool {version}")
                .AppendLine()
                .AppendLine("Usage:")
                .AppendLine()
                .AppendLine($"{procname}.exe <files and folders>")
                .AppendLine()
                .AppendLine("You can also drag-and-drop folder/files onto the tool.")
                .AppendLine()
                .AppendLine("How it works:")
                .AppendLine()
                .AppendLine("All provided .tex files are converted to .png.")
                .AppendLine("All provided valid image files(png, jpg, etc.) are converted to PNG and saved as .tex version 1010.");
            MessageBox.Show(sb.ToString(), "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        
        public static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                PrintHelp();
                return;
            }
        }
    }
}