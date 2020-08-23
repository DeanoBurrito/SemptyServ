using System;

namespace SemptyServ
{
    public interface ISMTPServerHooks
    {
        void NewMailReceived(string username, ReceivedEmail mail);
        bool UserHasMailbox(string username);
        bool UserLoginValid(string username, string password, AuthMethod method);
    }

    internal class DefaultServerHooks : ISMTPServerHooks
    {
        public void NewMailReceived(string username, ReceivedEmail mail)
        {}

        public bool UserHasMailbox(string username)
        { return true; }

        public bool UserLoginValid(string username, string password, AuthMethod method)
        { return true; }
    }
}