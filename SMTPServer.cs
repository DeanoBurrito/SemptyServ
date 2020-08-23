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
        internal ISMTPServerHooks serverHooks;

        public List<ReceivedEmail> receivedEmails = new List<ReceivedEmail>();
        bool allowESMTP;
        int listeningPort;
        Dictionary<string, string> config = new Dictionary<string, string>() 
        {
            { "Domain", "example.com" },
            { "MaxMessageBytes", "1000000" },
            { "BufferSize", "4096" },
            { "TransactionLogLen", "16" },
        };

        TcpListener tcpListener;
        internal ConcurrentDictionary<int, RemoteConnection> currSessions = new ConcurrentDictionary<int, RemoteConnection>();
        ConcurrentQueue<int> closeQueue = new ConcurrentQueue<int>();

        ConcurrentQueue<string[]> transactionLog = new ConcurrentQueue<string[]>();
        
        public SMTPServer(string domain, int incomingPort = 25, bool supportExts = true)
        {
            listeningPort = incomingPort;
            allowESMTP = supportExts;
            config["Domain"] = domain;
            serverHooks = new DefaultServerHooks();

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

        public void SetServerHooks(ISMTPServerHooks hookInterface)
        {
            serverHooks = hookInterface;
            Logger.Debug?.WriteLine("SMTP server hooks updated.");
        }

        public void MessagePump()
        {
            int tryOut;
            while (closeQueue.TryDequeue(out tryOut))
            {
                Logger.Debug?.WriteLine("Closing session " + tryOut);
                
                RemoteConnection conn;
                currSessions.Remove(tryOut, out conn);
                conn?.Shutdown();
            }
        }

        public void Shutdown()
        {
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
            Console.WriteLine("Received at: " + email.receivedTime.ToString());
            Console.WriteLine("From: " + email.sender);
            Console.WriteLine("To: " + email.recipient);
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

        public Queue<string[]> GetTransactionLog()
        {
            string[][] copy = new string[transactionLog.Count][];
            transactionLog.CopyTo(copy, 0);
            return new Queue<string[]>(copy);
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
            RemoteConnection connection = new RemoteConnection(newClient, config, this, sessionId);
            while (!currSessions.TryAdd(sessionId, connection))
            {
                //something already exists with this key, reassign and then re-add
                connection.connId = r.Next();
            }
            Logger.Info?.WriteLine("New SMTP session created with local id: " + connection.connId);
        }

        public string GetStatus()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append("unread= " + receivedEmails.Count);
            sb.Append(", domain=" + config["Domain"]);
            sb.Append(", port=" + listeningPort);
            sb.Append(", sessions=" + currSessions.Count);
            sb.Append(", logLen=" + transactionLog.Count);
            return sb.ToString();
        }

        internal void QueueEndConnection(int localId)
        {
            closeQueue.Enqueue(localId);
        }

        internal void NotifyReceivedMail(ReceivedEmail mail)
        {
            Logger.Info?.WriteLine("Server received mail from: " + mail.sender);
            receivedEmails.Add(mail);
            serverHooks.NewMailReceived(mail.recipient, mail);
        }

        internal void AppendTransactionLog(string[] transDetails)
        {
            int logLength = int.Parse(config["TransactionLogLen"]);
            while (transactionLog.Count >= logLength)
            {
                transactionLog.TryDequeue(out _); //cull from the front of the queue
            }
            transactionLog.Enqueue(transDetails);
        }
    }
}