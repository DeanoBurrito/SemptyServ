using System;
using System.Collections.Generic;
using CliMod;

namespace SemptyServ
{
    [CLIModule("Sempty", 1)]
    public static class SemptyModule
    {
        static SMTPServer localServer;
        
        [CLICommand("about", "Displays info about SemptyServ")]
        public static void GetAbout(string[] args)
        {
            Console.WriteLine(@"--- Sempty C# ESMTP Mail Server ---
This server is based on the CliMod interface, and is designed as a simple C#-based
SMTP server. It does support various ESMTP features, but it is mainly intended for
use in other personal projects (GroovyMail).
This code is all for the purposes of self education and is not production ready.
Source is released under MIT license.
            ");
        }

        [CLICommand("start", "Starts a SemptyServ instance locally.")]
        public static void StartServer(string[] args)
        {
            Logger.SetLevel(Logger.InitLevel.Debug);

            if (args.Length < 1)
            {
                Console.WriteLine("Command error, expected at least domain name for server to use.");
                Console.WriteLine("Usage: start <domainName> <?portNum?> <?allowExtsBool?>");
                return;
            }
            if (localServer != null)
            {
                Console.WriteLine("Process local SMTP server is already running!");
                return;
            }

            if (args.Length == 1)
                localServer = new SMTPServer(args[0]);
            else if (args.Length == 2)
                localServer = new SMTPServer(args[0], int.Parse(args[1]));
            else if (args.Length == 3)
                localServer = new SMTPServer(args[0], int.Parse(args[1]), bool.Parse(args[2]));
            CLInterface.inst.AddPumpCallback("SMTP", localServer.MessagePump);
        }

        [CLICommand("stop", "Stops the sempty server running in this process.")]
        public static void StopServer(string[] args)
        {
            if (localServer == null)
            {
                Console.WriteLine("No SMTP server running.");
                return;
            }
            CLInterface.inst.RemovePumpCallback("SMTP");
            localServer.Shutdown();
            localServer = null;
        }

        [CLICommand("status", "Gets the current server status")]
        public static void GetServerStatus(string[] args)
        {
            if (localServer == null)
            {
                Console.WriteLine("Local SMTP server not running, use start command to bring up.");
                return;
            }
            Console.WriteLine("Local server status as follows:");
            Console.WriteLine(localServer.GetStatus());
        }

        public static void DisplayMail(string[] args)
        {
            if (localServer == null)
                return;
            
            const int DISPLAY_COUNT = 20;
            const int COLUMN_WIDTH = 20;
            List<ReceivedEmail> recvMail = new List<ReceivedEmail>(localServer.receivedEmails.ToArray()); //force it to copy to an array (separate copy in memory)
            int iteratorBase = Math.Clamp(recvMail.Count - DISPLAY_COUNT, 0, int.MaxValue - DISPLAY_COUNT);
            for (int i = iteratorBase; i < recvMail.Count; i++)
            {
                Console.WriteLine("TODO: Finish implementing this!");
            }
        }

        private static string FixStringWidth(string str, int width, string overflow = " ... ", bool preserveStart = true)
        {
            if (str.Length <= width)
                return str;
            if (preserveStart)
                str = str.Remove(str.Length - width - overflow.Length) + overflow;
            else //preserve the end of the string
                str = overflow + str.Remove(0, str.Length - width - overflow.Length);
            return str;
        }
    }
}