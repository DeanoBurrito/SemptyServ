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

        [CLICommand("view", "View a received email in all its glory")]
        public static void DisplayMail(string[] args)
        {
            if (localServer == null)
                return;
            
            if (args.Length > 0 && int.TryParse(args[0], out int idx))
                localServer.DisplayEmail(idx);
            else
                localServer.DisplayEmail();
        }

        [CLICommand("dump", "dumps any currently in progress session details.")]
        public static void DumpSessions(string[] args)
        {
            if (localServer == null)
            {
                Console.WriteLine("No local SMTP instance running.");
                return;
            }
            
            foreach (KeyValuePair<int, RemoteConnection> rc in localServer.currSessions)
            {
                try
                {
                    Console.WriteLine("[Session " + rc.Key + "]");
                    Console.WriteLine("AuthMethod: " + rc.Value.currSession?.authMethod.ToString());
                    Console.WriteLine("RemoteEndpoint: " + rc.Value.tcpClient.Client.RemoteEndPoint.ToString());
                    Console.WriteLine("BodyBuffer: " + rc.Value.currSession?.messageBuffer);
                    Console.WriteLine("SMTP-From: " + rc.Value.currSession?.sender);
                    foreach (string r in rc.Value.currSession?.recipients)
                    {
                        Console.WriteLine("SMTP-To: " + r);
                    }
                }
                catch
                { 
                    Console.WriteLine("[Error occured reading session data]");
                    continue; 
                }
            }
        }

        [CLICommand("log", "Shows a specific SMTP transaction. Negative numbers go backwards from latest.")]
        public static void ShowTransaction(string[] args)
        {
            if (localServer == null)
            {
                Console.WriteLine("No local SMTP instance running.");
                return;
            }

            Queue<string[]> logQueue = localServer.GetTransactionLog();

            while (logQueue.Count > 0)
            {
                string[] currTrans = logQueue.Dequeue();
                Console.WriteLine("-------- New Transaction ---------");
                for (int i = 0; i < currTrans.Length; i++)
                    Console.WriteLine(currTrans[i]);
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