using System;
using System.Collections.Generic;
using System.Net.Sockets;
using CliMod;

namespace SemptyServ
{
    public sealed class RemoteConnection
    {
        readonly Dictionary<string, string> serverConfig;
        readonly SMTPServer ownerServer;
        internal int connId;
        
        readonly internal TcpClient tcpClient;
        readonly NetworkStream tcpStream;
        byte[] recvBuff;
        internal SMTPSession currSession;

        bool shutdownQueued = false;
        List<string> connLog = new List<string>();

        public RemoteConnection(TcpClient client, Dictionary<string, string> config, SMTPServer owner, int connectionId)
        {
            tcpClient = client;
            serverConfig = config;
            ownerServer = owner;
            connId = connectionId;

            //setting up defaults in config
            if (!config.ContainsKey("Domain"))
                config.Add("Domain", "NoDomain");
            if (!config.ContainsKey("MessageMaxBytes"))
                config.Add("MessageMaxBytes", "1000000");

            recvBuff = new byte[config.ContainsKey("BufferSize") ? int.Parse(config["BufferSize"]) : 4096];
            try
            {
                tcpClient.ReceiveBufferSize = recvBuff.Length;
                tcpClient.SendBufferSize = recvBuff.Length;
                tcpStream = tcpClient.GetStream();
                tcpStream.BeginRead(recvBuff, 0, recvBuff.Length, ProcessRecvData, null);
                SendResponse(new SMTPResult(SMTPResponseCode.ServerReady, config["Domain"] + " SEMPTY Server is ready for, welcome."));
            }
            catch (Exception e)
            {
                Logger.Critical?.WriteLine("TCP critical error, cannot establish connection: " + e.ToString());
                shutdownQueued = true;
                ownerServer.QueueEndConnection(connId);
            }
        }

        public void Shutdown()
        {
            ownerServer.AppendTransactionLog(connLog.ToArray());
            SendResponse(new SMTPResult(SMTPResponseCode.ServerDisconnecting, serverConfig["Domain"] + " disconnecting. Goodbye."));
        }

        private void ProcessRecvData(IAsyncResult result)
        {
            if (shutdownQueued)
                return; //suppress any errors during shutdown
            
            try
            {
                int recvLen = tcpStream.EndRead(result);
                if (recvLen <= 0)
                {
                    throw new Exception("RecvLength less than or equal to zero.");
                }
                string recvText = System.Text.Encoding.ASCII.GetString(recvBuff, 0, recvLen);
                tcpStream.BeginRead(recvBuff, 0, recvBuff.Length, ProcessRecvData, null);
                Logger.Debug?.WriteLine("[<- RECV] " + recvText.Replace("\r\n", ""));
                connLog.Add("[<- RECV] " + recvText.Replace("\r\n", ""));

                //try executing the received text as a SMTP command
                TryExecCommand(recvText);
            }
            catch (Exception e)
            {
                Logger.Critical?.WriteLine("TCP read critical error, details: " + e.ToString());
                shutdownQueued = true;
                ownerServer.QueueEndConnection(connId);

            }
        }

        private void SendResponse(SMTPResult result)
        {
            if (shutdownQueued)
                return;
            
            try
            {
                Logger.Debug?.WriteLine("[SEND ->] " + result.ToString());
                connLog.Add("[SEND ->] " + result.ToString());

                byte[] encodedReply = System.Text.Encoding.ASCII.GetBytes(result.ToString() + "\r\n");
                tcpStream.BeginWrite(encodedReply, 0, encodedReply.Length, null, null);
            }
            catch (Exception e)
            {
                Logger.Critical?.WriteLine("TCP critical error, cannot send: " + e.ToString());
                shutdownQueued = true;
                ownerServer.QueueEndConnection(connId);
            }
        }

        private bool UserHasMailbox(string username)
        {
            return ownerServer.serverHooks.UserHasMailbox(username); //possible cross-threading issues, may need to implement a wait queue?
        }

        private void TryExecCommand(string str)
        {
            //receiving body data
            if (currSession != null && currSession.readingBody)
            {
                currSession.messageBuffer += str;
                if (str.Contains("\r\n.\r\n"))
                {
                    //end of DATA transmission
                    currSession.messageBuffer = currSession.messageBuffer.Remove(currSession.messageBuffer.Length - 5); //trim the last 5 character (the end of message seq)
                    currSession.readingBody = false;
                    SendResponse(new SMTPResult(SMTPResponseCode.CommandOK, "OK, message queued successfully."));

                    List<ReceivedEmail> receivedEmails = currSession.GetMail();
                    foreach (ReceivedEmail re in receivedEmails)
                        ownerServer.NotifyReceivedMail(re);
                }
                return;
            }
            //str = str.Replace("\r\n", ""); //strip any tailing newlines
            str = str.Remove(str.Length - 2);

            //anytime commands (no session state needed)
            if (str.StartsWith("RSET", true, null))
            {
                currSession = new SMTPSession();
                SendResponse(new SMTPResult(SMTPResponseCode.CommandOK, "OK, transfer state reset."));
                return;
            }
            else if (str.StartsWith("VRFY", true, null))
            {
                string userAddr = str.Remove(0, 5);
                if (UserHasMailbox(userAddr))
                    SendResponse(new SMTPResult(SMTPResponseCode.CommandOK, "User mailbox exists."));
                else
                    SendResponse(new SMTPResult(SMTPResponseCode.UserDoesNotExist, "Unable to verify user account."));
                return;

            } 
            else if (str.StartsWith("NOOP", true, null))
            {
                SendResponse(new SMTPResult(SMTPResponseCode.CommandOK, "OK."));
                return;
            }
            else if (str.StartsWith("QUIT", true, null))
            {
                currSession = null;
                SendResponse(new SMTPResult(SMTPResponseCode.ServerDisconnecting, serverConfig["Domain"] + " is closing this connection, goodbye."));
                shutdownQueued = true;
                ownerServer.QueueEndConnection(connId);
                return;
            }

            //pre-session commands
            if (currSession == null)
            {
                if (str.StartsWith("HELO"))
                {
                    currSession = new SMTPSession();
                    string foreignDomain = str.Split(" ")[1].Replace("\n", "").Replace("\r", "");
                    currSession.metadata.Add("Foreign domain: " + foreignDomain);
                    currSession.metadata.Add("Foreign endpoint: " + tcpClient.Client.RemoteEndPoint.ToString());
                    SendResponse(new SMTPResult(SMTPResponseCode.CommandOK, serverConfig["Domain"] + " Hello " + foreignDomain));
                    return;
                }
                else if (str.StartsWith("EHLO"))
                {
                    currSession = new SMTPSession();
                    string foreignDomain = str.Split(" ")[1];
                    currSession.metadata.Add("Foreign domain: " + foreignDomain);
                    currSession.metadata.Add("Foreign endpoint: " + tcpClient.Client.RemoteEndPoint.ToString());
                    currSession.metadata.Add("Using SMTP Extensions");
                    SendResponse(new SMTPResult(SMTPResponseCode.CommandOK, serverConfig["Domain"] + " greets " + foreignDomain, true));
                    SendResponse(new SMTPResult(SMTPResponseCode.CommandOK, "8BITMIME", true)); //announce that we support 8bit data
                    SendResponse(new SMTPResult(SMTPResponseCode.CommandOK, "SIZE " + serverConfig["MessageMaxBytes"], true));
                    SendResponse(new SMTPResult(SMTPResponseCode.CommandOK, "PIPELINING"));
                    return;
                }
            }
            //session-required commands
            else if (currSession != null)
            {
                if (str.StartsWith("MAIL FROM", true, null))
                {
                    SendResponse(currSession.ExecCommand(SMTPCommand.MAIL_FROM, str.Remove(0, str.IndexOf(':') + 1)));
                    return;
                }
                else if (str.StartsWith("RCPT TO", true, null))
                {
                    SendResponse(currSession.ExecCommand(SMTPCommand.RCPT_TO, str.Remove(0, str.IndexOf(':') + 1)));
                    return;
                }
                else if (str.StartsWith("DATA", true, null))
                {
                    SendResponse(currSession.ExecCommand(SMTPCommand.DATA, str.Remove(0, str.IndexOf(':') + 1)));
                    return;
                }
            }

            //default response (means it wasnt caught by anything above)
            SendResponse(new SMTPResult(SMTPResponseCode.CommandInvalid, "Could not execute command, try again."));
        }
    }
}