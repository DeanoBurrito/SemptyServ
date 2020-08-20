using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using CliMod;

namespace SemptyServ
{
    public sealed class SMTPServer
    {   
        List<ReceivedEmail> receivedEmails = new List<ReceivedEmail>();
        bool allowESMTP;
        int listeningPort;
        string ownerDomain;

        TcpListener tcpListener;
        ConcurrentDictionary<int, ValidConnection> currSessions = new ConcurrentDictionary<int, ValidConnection>();
        ConcurrentQueue<int> closeQueue = new ConcurrentQueue<int>();

        internal static SMTPServer localInst;
        
        public SMTPServer(string domain, int incomingPort = 25, bool supportExts = true)
        {
            listeningPort = incomingPort;
            allowESMTP = supportExts;
            ownerDomain = domain;
            if (localInst == null)
                localInst = this;
            else
            {
                Logger.Critical?.WriteLine("SMTP server already running within this process. Please startup another process instance.");
                return;
            }

            try
            {
                tcpListener = new TcpListener(IPAddress.Any, listeningPort);
                tcpListener.Start();
                tcpListener.BeginAcceptTcpClient(HandleNewTcpConn, tcpListener);
                Logger.Info?.WriteLine("SMTP server now listening on: " + tcpListener.Server.LocalEndPoint.ToString());
            }
            catch (Exception e)
            {
                Logger.Critical?.WriteLine("Critical error occured in TcpListener: " + e.ToString());
            }
        }

        public void MessagePump()
        {
            int tryOut;
            while (closeQueue.TryDequeue(out tryOut))
            {
                Logger.Debug?.WriteLine("Closing session " + tryOut);
                currSessions.Remove(tryOut, out _);
            }
        }

        public void Shutdown()
        {
            //TODO: implement
            foreach (int id in currSessions.Keys)
                closeQueue.Enqueue(id);
            MessagePump(); //quick and dirty flush of current connections
            
        }

        public void DisplayEmail(int index = -1)
        {
            if (index == -1)
            {
                //print all
                for (int i = 0; i < receivedEmails.Count; i++)
                    DisplayEmail(i);
                return;
            }

            if (index < 0 || index > receivedEmails.Count - 1)
            {
                Logger.Critical?.WriteLine("Error: Cannot display that email, index out of bounds.");
                return;
            }
            ReceivedEmail email = receivedEmails[index];
            Console.WriteLine("-------- EMAIL BEGINS --------");
            Console.WriteLine("Recieved at: " + email.receivedTime.ToString());
            Console.WriteLine("From: " + email.sender);
            Console.WriteLine("To: " + email.recipient);
            Console.WriteLine("Subject: " + email.subject);
            Console.WriteLine("Additional info: ");
            for (int i = 0; i < email.senderInfo.Count; i++)
            {
                Console.WriteLine(" - " + email.sender[i]);
            }
            Console.WriteLine();
            for (int i = 0; i < email.messageBody.Count; i++)
                Console.WriteLine(email.messageBody[i]);

            Console.WriteLine("-------- EMAIL ENDS --------");
        }

        private void HandleNewTcpConn(IAsyncResult result)
        {
            TcpClient newClient;
            try
            {
                newClient = tcpListener.EndAcceptTcpClient(result);
                tcpListener.BeginAcceptTcpClient(HandleNewTcpConn, tcpListener);
            }
            catch (Exception e)
            {
                Logger.Critical?.WriteLine("Critical error occured during incoming tcp connect, details: " + e.ToString());
                return;
            }
            Logger.Info?.WriteLine("Server received new incoming connection from: " + newClient.Client.RemoteEndPoint.ToString());

            Random r = new Random();
            int sessionId = r.Next();
            while (currSessions.ContainsKey(sessionId))
                sessionId = r.Next();
            ValidConnection vc = new ValidConnection(newClient, allowESMTP, sessionId, ownerDomain);
            while (!currSessions.TryAdd(sessionId, vc))
            {
                //something already exists with this key, reassign and then re-add
                sessionId = r.Next();
                vc.localSessionId = sessionId;
            }
            Logger.Info?.WriteLine("New SMTP session created with local id: " + sessionId);
        }

        public string GetStatus()
        {
            return "SMTP server status: Doing fine, how about you?";
        }

        internal void QueueEndSession(int localId)
        {
            closeQueue.Enqueue(localId);
        }

        internal void NotifyReceivedMail(ReceivedEmail mail)
        {
            Logger.Info?.WriteLine("Server received mail from: " + mail.sender);
            receivedEmails.Add(mail);
        }
    }
}