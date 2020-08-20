using System;
using System.Collections.Generic;

namespace SemptyServ
{
    public sealed class ReceivedEmail
    {
        public string sender = "null";
        public string recipient = "null";
        public DateTime receivedTime = DateTime.Now;
        public string subject = "null";
        public List<string> messageBody = new List<string>();
        public List<string> senderInfo = new List<string>();

        public ReceivedEmail()
        {}
    }
}