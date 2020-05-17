using System;
using System.IO;
using System.Linq;

namespace ThumbnailCreatorBot
{
    internal static class LoggingUtilities
    {
        public static void LogFonts()
        {
            Console.WriteLine("Found fonts: ");
            Utilities.Fonts.Families.Select(font => font.Name).ToList().ForEach(Console.WriteLine);
            Console.WriteLine("-----");
        }

        public static void LogDir(string subDirectory = "")
        {
            string directoryToLog = Path.Combine(Directory.GetCurrentDirectory(), subDirectory);

            Console.WriteLine($"Files in '{directoryToLog}':");
            foreach (var file in Directory.EnumerateFiles(directoryToLog))
            {
                Console.WriteLine(file);
            }

            Console.WriteLine($"Directories in '{directoryToLog}':");
            foreach (var directory in Directory.EnumerateDirectories(directoryToLog))
            {
                Console.WriteLine(directory);
            }

            Console.WriteLine("-----");
        }
    }
}
