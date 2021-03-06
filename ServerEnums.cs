using System;

public enum SMTPResponseCode : int
{
    NonStandardSuccess = 200,
    SystemStatusOrHelp = 211,
    ServerReady = 220,
    ServerDisconnecting = 221,
    CommandOK = 250,
    UserNotLocal = 251,
    CannotVerifyUser = 252,

    ReadyForData = 354,
    
    ServerUnavailable = 421,
    MailboxUnavilable = 450,
    CommandErrorServer = 451,

    CommandInvalid = 500,
    CommandArgsInvalid = 501,
    CommandNotImplemented = 502,
    CommandSequenceInvalid = 503,
    AuthenticationRequired = 530,
    UserDoesNotExist = 550,
    TransactionFailed = 554,
}

public enum SMTPCommand
{
    HELO,
    EHLO,
    MAIL_FROM,
    RCPT_TO,
    DATA,
    VRFY,
    NOOP,
    QUIT,
    RSET,
    AUTH,
    SIZE,
}

public enum AuthMethod
{
    None,
    Plain,
    Login,
    DIGEST_MD5,
}