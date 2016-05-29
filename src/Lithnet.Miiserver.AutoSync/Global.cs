using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.IO;

namespace Lithnet.Miiserver.AutoSync
{
    public static class Global
    {
        private static Random random = new Random();

        //static Global()
        //{
        //    Global.ScriptDirectory = Global.AssemblyDirectory;
        //}

        //public static string ScriptDirectory { get; set; }

        public static string AssemblyDirectory
        {
            get
            {
                string codeBase = Assembly.GetExecutingAssembly().CodeBase;
                UriBuilder uri = new UriBuilder(codeBase);
                string path = Uri.UnescapeDataString(uri.Path);
                return Path.GetDirectoryName(path);
            }
        }

        public static string CleanMAName(string name)
        {
            string cleanName = name;

            foreach (char c in Path.GetInvalidFileNameChars())
            {
                cleanName = cleanName.Replace(c, '-');
            }

            return cleanName;
        }

        public static int RandomizeOffset(int number)
        {
            return RandomizeOffset(number, 10);
        }

        public static int RandomizeOffset(int number, int offsetPercent)
        {
            return random.Next(number - (number / offsetPercent), number + (number / offsetPercent));
        }
    }
}
