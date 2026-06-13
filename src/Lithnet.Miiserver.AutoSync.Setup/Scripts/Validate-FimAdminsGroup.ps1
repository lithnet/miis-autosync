<#
.SYNOPSIS
    Validates that the FIM Sync administrators group entered/detected on the dialog exists and
    is actually a group.

.DESCRIPTION
    Runs as an immediate PowerShell custom action from the dialog's "Next" (DoAction). It:
      * resolves GROUP_FIM_SYNC_ADMINS_NAME to a SID (NTAccount -> SID),
      * uses LookupAccountSid to confirm the SID is a Group (not a user), and
      * sets GROUP_FIM_SYNC_ADMINS_EXISTS (1/0); on success also canonicalises the name and
        records the SID.

    The group is normally pre-filled from the MIM Sync DB but may be typed manually when the
    installing user lacks DB read access. The dialog shows a "group not found" message box when
    GROUP_FIM_SYNC_ADMINS_EXISTS is 0.

    Properties: reads/updates GROUP_FIM_SYNC_ADMINS_NAME, sets GROUP_FIM_SYNC_ADMINS_EXISTS and
    GROUP_FIM_SYNC_ADMINS_SID.
#>

$dllimport = @'

public enum PrincipalType
{
    Unknown = 0,
    User = 1,
    Group = 2
}

        public static PrincipalType GetPrincipalType(SecurityIdentifier sid)
        {
            int cchName = 0;
            int cchReferencedDomainName = 0;
            SID_NAME_USE sidType;

            var sidBuffer = new byte[sid.BinaryLength];
            sid.GetBinaryForm(sidBuffer, 0);

            if (!LookupAccountSid(null, sidBuffer, null, ref cchName, null, ref cchReferencedDomainName, out sidType))
            {
                int error = Marshal.GetLastWin32Error();
                if (error != 122) //ERROR_INSUFFICIENT_BUFFER)
                {
                    throw new Win32Exception(error);
                }
            }

            var referencedDomainName = new StringBuilder(cchReferencedDomainName);
            var name = new StringBuilder(256);

            if (!LookupAccountSid(null, sidBuffer, name, ref cchName, referencedDomainName, ref cchReferencedDomainName, out sidType))
            {
                int error = Marshal.GetLastWin32Error();
                throw new Win32Exception(error);
            }

            switch (sidType)
            {
                case SID_NAME_USE.SidTypeGroup:
                case SID_NAME_USE.SidTypeWellKnownGroup:
                case SID_NAME_USE.SidTypeAlias:
                    return PrincipalType.Group;

                case SID_NAME_USE.SidTypeUser:
                    return PrincipalType.User;
            }

            return 0;
        }

        private enum SID_NAME_USE
        {
            SidTypeUser = 1,
            SidTypeGroup,
            SidTypeDomain,
            SidTypeAlias,
            SidTypeWellKnownGroup,
            SidTypeDeletedAccount,
            SidTypeInvalid,
            SidTypeUnknown,
            SidTypeComputer
        }

        [DllImport("advapi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool LookupAccountSid(
                string lpSystemName,
                [MarshalAs(UnmanagedType.LPArray)] byte[] Sid,
                StringBuilder lpName,
                ref int cchName,
                StringBuilder ReferencedDomainName,
                ref int cchReferencedDomainName,
                out SID_NAME_USE peUse);
'@;

$InformationPreference = "Continue"
$WarningPreference = "Continue"

try
{
    Add-Type -MemberDefinition $dllimport -Name GroupUtils -Namespace Interop -UsingNamespace @("System.Text", "System.Security.Principal", "System.ComponentModel") -ErrorAction Stop

    $groupname = AI_GetMsiProperty GROUP_FIM_SYNC_ADMINS_NAME
    $account = New-Object "System.Security.Principal.NTAccount" $groupname
    $sid = $account.Translate([System.Security.Principal.SecurityIdentifier])

    if ($null -ne $sid)
    {
        Write-Information "Mapped group $groupname to SID $sid";
        $objUser = $sid.Translate([System.Security.Principal.NTAccount])

        Write-Information "Mapped SID $sid to NTAccount $($objUser.Value)";
        $type = [Interop.GroupUtils]::GetPrincipalType($sid)

        Write-Information "Principal type was $type";
        if ($type -eq [Interop.GroupUtils+PrincipalType]::Group)
        {
            AI_SetMsiProperty GROUP_FIM_SYNC_ADMINS_EXISTS 1
            AI_SetMsiProperty GROUP_FIM_SYNC_ADMINS_SID $sid.ToString()
            AI_SetMsiProperty GROUP_FIM_SYNC_ADMINS_NAME $objUser.Value
            return;
        }
    }
}
catch [Exception]
{
    Write-Warning "Unable to complete group resolution process"
    Write-Warning $_.Exception.ToString()
}

AI_SetMsiProperty GROUP_FIM_SYNC_ADMINS_EXISTS 0
