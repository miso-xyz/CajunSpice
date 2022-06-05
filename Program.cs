using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CajunSpice
{
    class Program
    {
        public static string path; // used by 'Updater.cs'

        static void Main(string[] args)
        {
            Console.Title = "CajunSpice";
            if (args.Length == 0) { Console.ForegroundColor = ConsoleColor.Red; Console.Write("No input file set!"); Console.ReadKey(); }
            else
            {
                path = args[0];
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write("CajunSpice v1.0 - ");
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine("(9Rays.net) Spices.net Deobfuscator");
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write(" |- ");
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("https://github.com/miso-xyz/CajunSpice/\n");
                new Deobfuscator(args[0]).Fix();
            }
        }
    }
}
