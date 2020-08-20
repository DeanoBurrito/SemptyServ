using System;
using System.Net.Sockets;
using CliMod;

namespace SemptyServ
{
    public class ValidConnection
    {
        public TcpClient tcpClient;
        public bool allowESMTP;
        public int localSessionId;
        public string domain;

        string foreignDomain = "";
        ReceivedEmail currMail = null;
        bool recievingBody = false;

        byte[] recvBuffer;
        
        public ValidConnection(TcpClient c, bool allowExts, int sessionId, string fqdn)
        { 
            tcpClient = c; 
            allowESMTP = allowExts; 
            localSessionId = sessionId; 
            domain = fqdn;

            recvBuffer = new byte[4096];
            tcpClient.Client.ReceiveBufferSize = 4096;
            tcpClient.Client.SendBufferSize = 4096;
            try
            {
                tcpClient.GetStream().BeginRead(recvBuffer, 0, 4096, ProcessRecvData, null);
                SendResponse(SMTPResponseCode.ServerReady, domain + " Server is ready, send the goods");
            }
            catch (Exception e)
            {
                Logger.Critical?.WriteLine("Critical error in recieving tcp data: " + e.ToString());
                SMTPServer.localInst.QueueEndSession(sessionId);
            }
        }

        internal void Shutdown()
        {
            try
            {
                SendResponse(SMTPResponseCode.ServerDisconnecting, "Server is shutting down.");
            }
            catch (Exception e)
            {} //dont care about it since we're closing the connection anyway.
        }

        private void ProcessRecvData(IAsyncResult result)
        {
            try
            {
                int recvLen = tcpClient.GetStream().EndRead(result);
                if (recvLen <= 0)
                {
                    throw new Exception("Received less than or 0 bytes!");
                }
                //convert text, reset buffer and setup for next network read.
                string recvText = System.Text.Encoding.ASCII.GetString(recvBuffer, 0, recvLen);
                recvBuffer = new byte[4096];
                tcpClient.GetStream().BeginRead(recvBuffer, 0, 4096, ProcessRecvData, null);

                ProcessCommandStr(recvText);
            }
            catch (Exception e)
            {
                Logger.Critical?.WriteLine("Critical error during tcp read: " + e.ToString());
                SMTPServer.localInst.QueueEndSession(localSessionId);
                return;
            }
        }

        private void ProcessCommandStr(string cmdStr)
        {
            if (recievingBody)
            {
                if (currMail.messageBody.Count > 10000)
                {
                    SendResponse(SMTPResponseCode.ServerDisconnecting, "Send over 10,000 lines in message, as if.");
                    SMTPServer.localInst.QueueEndSession(localSessionId);
                    return;
                }
            

                currMail.messageBody.Add(cmdStr);
                if (cmdStr.Contains("\r\n.\r\n"))
                {
                    recievingBody = false;
                    SendResponse(SMTPResponseCode.CommandOK, "OK, message data accepted.");
                    SMTPServer.localInst.NotifyReceivedMail(currMail);
                    currMail = new ReceivedEmail();
                    return;
                }
                return;
            }
            
            cmdStr = cmdStr.Replace("\r", "").Replace("\n", "");
            string[] cmdParts;
            if (!cmdStr.Contains(" "))
                cmdParts = new string[] { cmdStr };
            else
                cmdParts = cmdStr.Split(" ");
            
            //gaurd statement against empty messages
            if (cmdParts.Length < 1)
            {
                Logger.Info?.WriteLine($"{tcpClient.Client.RemoteEndPoint.ToString()} send data that contained no tokens at all. Ignoring message.");
                return;
            }

            Logger.Debug?.WriteLine($"Host ({tcpClient.Client.RemoteEndPoint.ToString()}) sent command: " + cmdStr);
            switch (cmdParts[0].ToUpper())
            {
                //SMTP methods
                case "HELO":
                    foreignDomain = cmdParts[1];
                    currMail = new ReceivedEmail();
                    SendResponse(SMTPResponseCode.CommandOK, "Hello there, server called " + foreignDomain);
                    break;
                case "MAIL": //parts[1] should be "FROM"
                    if (currMail == null)
                        SendResponse(SMTPResponseCode.CommandSequenceInvalid, "Expected a HELO/EHLO first you uncultured piece of software.");
                    else
                    {
                        currMail.sender = cmdParts[1].Remove(0, cmdParts[0].IndexOf(':') + 1);
                        SendResponse(SMTPResponseCode.CommandOK, "OK");
                    }
                    break;
                case "RCPT": //parts[1] should be "TO"
                    if (currMail == null)
                        SendResponse(SMTPResponseCode.CommandSequenceInvalid, "Expected a HELO/EHLO first you uncultured piece of software.");
                    else
                    {
                        currMail.recipient = cmdParts[1].Remove(0, cmdParts[0].IndexOf(':') + 1);
                        SendResponse(SMTPResponseCode.CommandOK, "OK");
                    }
                    break;
                case "DATA":
                    if (currMail == null)
                        SendResponse(SMTPResponseCode.CommandSequenceInvalid, "Expected a HELO/EHLO first you uncultured piece of software.");
                    else
                    {
                        SendResponse(SMTPResponseCode.ReadyForData, "End data with <CR><LF><CR><LF>.<CR><LF><CR><LF>");
                        recievingBody = true;
                    }
                    break;
                case "VRFY":
                    break;
                case "NOOP":
                    SendResponse(SMTPResponseCode.CommandOK, "NOOP OK.");
                    break;
                case "QUIT":
                    SendResponse(SMTPResponseCode.ServerDisconnecting, "bye bye.");
                    SMTPServer.localInst.QueueEndSession(localSessionId);
                    break;
                case "RSET":
                    currMail = new ReceivedEmail();
                    SendResponse(SMTPResponseCode.CommandOK, "Session reset successfully.");
                    break;

                //ESMTP methods                
                case "EHLO":
                    if (!allowESMTP)
                        goto INVALID_COMMAND;
                    foreignDomain = cmdParts[1];
                    currMail = new ReceivedEmail();
                    SendResponse(SMTPResponseCode.CommandOK, "Hello there, server called " + foreignDomain);
                    break;
                case "AUTH":
                    if (!allowESMTP)
                        goto INVALID_COMMAND;
                    break;
                case "SIZE":
                    if (!allowESMTP)
                        goto INVALID_COMMAND;
                    break;
                case "STARTTLS":
                    if (!allowESMTP)
                        goto INVALID_COMMAND;
                    break;
                default:
                INVALID_COMMAND:
                    Logger.Info?.WriteLine($"Host {tcpClient.Client.RemoteEndPoint.ToString()} sent an invalid command: " + cmdParts[0]);
                    SendResponse(SMTPResponseCode.CommandInvalid, "Invalid command, please try again.");
                    break;
            }
        }

        private void SendResponse(SMTPResponseCode code, string details)
        {
            details = ((int)code).ToString() + " " + details;
            Logger.Debug?.WriteLine("Server sending reply: " + details);
            details += "\r\n";

            byte[] encodedReply = System.Text.Encoding.ASCII.GetBytes(details);
            tcpClient.GetStream().BeginWrite(encodedReply, 0, encodedReply.Length, null, null);
        }
    }
}