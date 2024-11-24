namespace Watch2sftp.Core.Utils;

public static class Impersonate
{
    public static WindowsIdentity CreateIdentity(string username, string password, string domain = "")
    {
        var securePassword = new SecureString();
        foreach (char c in password)
        {
            securePassword.AppendChar(c);
        }

        nint tokenHandle = nint.Zero;

        bool success = LogonUser(
            username,
            domain,
            securePassword,
            LogonType.LOGON32_LOGON_NEW_CREDENTIALS, // Compatible avec Kerberos
            LogonProvider.LOGON32_PROVIDER_DEFAULT, // Utilise le protocole disponible (Kerberos priorisé)
            out tokenHandle);

        if (!success)
        {
            int errorCode = Marshal.GetLastWin32Error();
            throw new InvalidOperationException($"Failed to create Windows identity. Error code: {errorCode}");
        }

        return new WindowsIdentity(tokenHandle);
    }

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool LogonUser(
        string lpszUsername,
        string lpszDomain,
        SecureString lpszPassword,
        LogonType dwLogonType,
        LogonProvider dwLogonProvider,
        out nint phToken);

    private enum LogonType
    {
        LOGON32_LOGON_INTERACTIVE = 2,
        LOGON32_LOGON_NETWORK,
        LOGON32_LOGON_NEW_CREDENTIALS = 9 // Obligatoire pour delegation Kerberos
    }

    private enum LogonProvider
    {
        LOGON32_PROVIDER_DEFAULT = 0, // Utilise automatiquement Kerberos si disponible
        LOGON32_PROVIDER_WINNT50 = 3 // Specifique a NTLM
    }
}
