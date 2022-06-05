using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Threading;

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
                Console.WriteLine("Checking for updates...");
                Updater update = new Updater(Updater.GetUpdate());
                if (!update.IsRunningLatest())
                {
                    string changeLogString = "";
                    foreach (string text in update.ChangeLog) { changeLogString += text + "\n"; }
                    MessageBox.Show("New update available!\n\nVersion: " + update.LatestVersion + "\nDownload Size: " + update.UpdateSize + "\nChangelog:\n\n" + changeLogString);
                }
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("No updates found!");
                Thread.Sleep(1000);
                Console.Clear();
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
                Console.ReadKey();
            }
        }
    }
}
