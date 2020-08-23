using System;

namespace SemptyServ
{
    public sealed class SMTPResult
    {
        SMTPResponseCode c;
        string message;
        bool useMultilineChar;

        public SMTPResult(SMTPResponseCode code, string details, bool isMultiline = false)
        { c = code; message = details; useMultilineChar = isMultiline; }

        public override string ToString()
        {
            if (useMultilineChar)
                return ((int)c).ToString() + "-" + message;
            return ((int)c).ToString() + " " + message;
        }
    }
}