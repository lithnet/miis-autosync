<#
.SYNOPSIS
    Validates the service account entered on the logon dialog.

.DESCRIPTION
    Runs as an immediate PowerShell custom action from the logon dialog's "Next" (DoAction). It:
      * checks the name format (DOMAIN\user or user@domain),
      * confirms the account exists (NTAccount -> SID),
      * supports gMSA: if the plain name isn't found, retries with a trailing '$', and uses
        NetQueryServiceAccount to confirm THIS computer can retrieve the managed password,
      * writes a human-readable problem into ACCOUNT_TEST_RESULT (empty == OK).

    It does NOT test the password (LogonUser): a gMSA has none, and for a normal account the
    password is confirmed when the service starts. The dialog shows a message box when
    ACCOUNT_TEST_RESULT is non-empty.

    Properties: reads/updates SERVICE_USERNAME, sets SERVICE_USER_SID and ACCOUNT_TEST_RESULT.
#>

Param()
$InformationPreference = "Continue"
$WarningPreference = "Continue"

$NativeServiceAccountMethods = @'
[DllImport("logoncli.dll", CharSet = CharSet.Auto)]
public static extern uint NetQueryServiceAccount(
    [In] string ServerName,
    [In] string AccountName,
    [In] uint InfoLevel,
    out IntPtr Buffer
);

[DllImport("netapi32.dll")]
public static extern uint NetApiBufferFree(
    [In] IntPtr Buffer
);

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public struct MSA_INFO
{
    public MSA_INFO_STATE State;
}

[Flags]
public enum MSA_INFO_STATE : uint
{
    MsaInfoNotExist = 1u,
    MsaInfoNotService = 2u,
    MsaInfoCannotInstall = 3u,
    MsaInfoCanInstall = 4u,
    MsaInfoInstalled = 5u
}
'@

Add-Type -MemberDefinition $NativeServiceAccountMethods -Name ServiceAccountUtils -Namespace Interop -ErrorAction Stop

try
{
    AI_SetMsiProperty ACCOUNT_TEST_RESULT ""
    $username = AI_GetMsiProperty SERVICE_USERNAME
    Write-Information "Validating username $username"

    if (($username -notlike "*\*") -and ($username -notlike "*@*"))
    {
        AI_SetMsiProperty ACCOUNT_TEST_RESULT "Please enter the username in the format DOMAIN\username"
        Write-Warning "Username was not in the correct format"
        return;
    }

    try
    {
        $account = New-Object "System.Security.Principal.NTAccount" $username
        $sid = $account.Translate([System.Security.Principal.SecurityIdentifier])
        if ($null -ne $sid)
        {
            AI_SetMsiProperty SERVICE_USER_SID $sid.ToString()
            Write-Information "Found account $account"
            Write-Information "Found sid $sid"
        }
    }
    catch [Exception]
    {
        Write-Warning "Could not find account name"
        Write-Warning $_.Exception.ToString()
    }

    if (-not $sid)
    {
        try
        {
            $username = "$username`$"
            $account = New-Object "System.Security.Principal.NTAccount" $($username)
            Write-Information "Found account $account"
            AI_SetMsiProperty SERVICE_USERNAME $username
            $sid = $account.Translate([System.Security.Principal.SecurityIdentifier])
            if ($null -ne $sid)
            {
                AI_SetMsiProperty SERVICE_USER_SID $sid.ToString()
                Write-Information "Found sid $sid"
            }
        }
        catch [Exception]
        {
            Write-Warning "Could not find account name"
            Write-Warning $_.Exception.ToString()
        }
    }

    if ($null -eq $sid)
    {
        AI_SetMsiProperty ACCOUNT_TEST_RESULT "The specified user could not be found in the directory"
        Write-Warning "Username was not found"
        return;
    }

    try
    {
        $ptr = [IntPtr]::Zero
        $returnValue = [Interop.ServiceAccountUtils]::NetQueryServiceAccount($null, $account, 0, [ref]$ptr)

        if ($returnValue -eq 0)
        {
            $result = [System.Runtime.InteropServices.Marshal]::PtrToStructure($ptr, [System.Type][Interop.ServiceAccountUtils+MSA_INFO])
            Write-Information "NetQueryServiceAccount returned state of $($result.State)"

            if ($result.State -eq [Interop.ServiceAccountUtils+MSA_INFO_STATE]::MsaInfoInstalled)
            {
                Write-Information "This computer can access the service account"
            }
            elseif ($result.State -eq [Interop.ServiceAccountUtils+MSA_INFO_STATE]::MsaInfoCannotInstall)
            {
                Write-Warning "This computer cannot access the service account"
                AI_SetMsiProperty ACCOUNT_TEST_RESULT "The computer does not have permission to access the service account password. Use the 'Set-ADServiceAccount' PowerShell cmdlet with the '-PrincipalsAllowedToRetrieveManagedPassword' parameter to grant this computer permission to read the password and try again"
            }
            elseif ($result.State -eq [Interop.ServiceAccountUtils+MSA_INFO_STATE]::MsaInfoCanInstall)
            {
                Write-Warning "This computer can access the service account, but is not assigned permission"
                AI_SetMsiProperty ACCOUNT_TEST_RESULT "The computer does not have permission to access the service account password. Use the 'Set-ADServiceAccount' PowerShell cmdlet with the '-PrincipalsAllowedToRetrieveManagedPassword' parameter to grant this computer permission to read the password and try again"
            }
            elseif ($result.State -eq [Interop.ServiceAccountUtils+MSA_INFO_STATE]::MsaInfoNotService)
            {
                Write-Information "The account specified was not a gMSA (a standard account password will be required)"
            }
            elseif ($result.State -eq [Interop.ServiceAccountUtils+MSA_INFO_STATE]::MsaInfoNotExist)
            {
                Write-Warning "The account was not found"
                AI_SetMsiProperty ACCOUNT_TEST_RESULT "The account specified could not be found"
            }
        }
        else
        {
            Write-Warning "Could not determine if this account was a service account (NetQueryServiceAccount returned $returnValue)"
        }
    }
    catch [Exception]
    {
        Write-Warning "Could not determine if this account was a service account"
        Write-Warning $_.Exception.ToString()
    }
    finally
    {
        if ($ptr -ne [IntPtr]::Zero)
        {
            [Interop.ServiceAccountUtils]::NetApiBufferFree($ptr) | Out-Null
        }
    }
}
catch [Exception]
{
    Write-Warning $_.Exception.ToString()
}
