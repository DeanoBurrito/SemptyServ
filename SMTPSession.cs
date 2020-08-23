using System;
using System.Collections.Generic;

namespace SemptyServ
{
    public sealed class SMTPSession
    {
        internal string messageBuffer = "";
        internal AuthMethod authMethod = AuthMethod.None;
        internal bool readingBody = false;

        internal string sender = "";
        internal List<string> recipients = new List<string>();
        internal List<string> metadata = new List<string>();
        
        public SMTPSession()
        {}

        public SMTPResult ExecCommand(SMTPCommand cmd, string args)
        {
            switch (cmd)
            {
                case SMTPCommand.DATA:
                    if (sender.Length == 0 || recipients.Count < 1)
                        return new SMTPResult(SMTPResponseCode.CommandSequenceInvalid, "Expected MAIL FROM and RCPT TO commands before DATA.");
                    readingBody = true;
                    return new SMTPResult(SMTPResponseCode.ReadyForData, "Ready to receive data, end sequence with <CR><LF>.<CR><LF>");
                case SMTPCommand.MAIL_FROM:
                    recipients.Clear();
                    messageBuffer = "";
                    sender = args;
                    return new SMTPResult(SMTPResponseCode.CommandOK, "OK.");
                case SMTPCommand.RCPT_TO:
                    //if user does not exist, send response code 550 (NoUserMailbox)
                    recipients.Add(args);
                    if (recipients.Count > 1)
                        return new SMTPResult(SMTPResponseCode.CommandOK, "OK, will CC this recipient.");
                    return new SMTPResult(SMTPResponseCode.CommandOK, "OK.");
            }
            
            return new SMTPResult(SMTPResponseCode.CommandInvalid, "Command not found");
        }

        public List<ReceivedEmail> GetMail()
        {
            List<ReceivedEmail> mails = new List<ReceivedEmail>();
            messageBuffer = messageBuffer.Replace("\r", "");
            List<string> lines = new List<string>(messageBuffer.Split('\n'));
            foreach (string recipient in recipients)
            {
                mails.Add(new ReceivedEmail()
                {
                    messageBody = lines,
                    recipient = recipient,
                    sender = sender,
                    senderInfo = metadata,
                });
            }
            messageBuffer = sender = "";
            recipients.Clear();
            metadata.Clear();
            return mails;
        }
    }
}