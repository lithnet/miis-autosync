<#
.SYNOPSIS
    Adds the AutoSync service account to the MIM Sync administrators group (idempotent).

.DESCRIPTION
    Resolves the group and user DOMAIN-first, then local MACHINE. Membership changes are
    idempotent: an existing member (matched by SID) is a no-op, and COM error 0x80071392
    ("object is already a member") is treated as success. Local groups (WinNT provider)
    are added by "WINNT://<userSid>"; domain groups are added by the user's directory path.

    The script runs as two deferred actions that share this file, distinguished by -Context.
    The impersonated (Caller) action runs with the installing user's non-elevated token and
    can write a DOMAIN group; the SYSTEM action runs as the machine account and can write a
    LOCAL group. Each instance resolves the group and acts only when the context matches the
    group type:
        local group  -> handled by the System instance
        domain group -> handled by the Caller instance
    The non-matching instance is a no-op. A group that cannot be resolved is treated as
    local, so the System instance reports the "group not found" warning.

    A failure to add the account is warned, not failed, and the install continues, because
    the installing user may not have rights to modify the group. The AutoSync service will
    not start until the account is a member of the group, so the warning instructs the
    operator to add it manually.

    -Account, -GroupName and -Context are supplied via CustomActionData because deferred
    actions cannot read live MSI properties.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Account,
    [Parameter(Mandatory)][string]$GroupName,
    [Parameter(Mandatory)][ValidateSet('System', 'Caller')][string]$Context
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.DirectoryServices.AccountManagement

# Resolve a principal DOMAIN-first, then local MACHINE. Returns the principal (or $null)
# and whether it should be treated as local. IsLocal is true whenever the domain lookup
# fails, including the not-found-anywhere case.
function Resolve-Principal {
    param([Parameter(Mandatory)][string]$Name)

    $domainContext = New-Object System.DirectoryServices.AccountManagement.PrincipalContext ([System.DirectoryServices.AccountManagement.ContextType]::Domain)
    $principal = [System.DirectoryServices.AccountManagement.Principal]::FindByIdentity($domainContext, $Name)
    if ($null -ne $principal) {
        return [pscustomobject]@{ Principal = $principal; IsLocal = $false }
    }

    $machineContext = New-Object System.DirectoryServices.AccountManagement.PrincipalContext ([System.DirectoryServices.AccountManagement.ContextType]::Machine)
    $principal = [System.DirectoryServices.AccountManagement.Principal]::FindByIdentity($machineContext, $Name)
    return [pscustomobject]@{ Principal = $principal; IsLocal = $true }
}

function Add-UserToGroup {
    param(
        [Parameter(Mandatory)][string]$Account,
        [Parameter(Mandatory)]$Group,   # GroupPrincipal
        [Parameter(Mandatory)]$User     # Principal
    )

    $groupEntry  = $Group.GetUnderlyingObject()   # System.DirectoryServices.DirectoryEntry
    $nativeGroup = $groupEntry.NativeObject        # IADsGroup (COM)

    # Idempotency: already a member? Compare by SID.
    foreach ($member in $nativeGroup.Members()) {
        $sidBytes = $member.GetType().InvokeMember('ObjectSid', [System.Reflection.BindingFlags]::GetProperty, $null, $member, $null)
        $memberSid = New-Object System.Security.Principal.SecurityIdentifier ($sidBytes, 0)
        if ($memberSid -eq $User.Sid) {
            Write-Output "User '$Account' is already a member of '$($Group.Name)'."
            return
        }
    }

    try {
        if ($groupEntry.Path -like 'WinNT:*') {
            $nativeGroup.Add("WINNT://$($User.Sid)")
        }
        else {
            $userEntry = $User.GetUnderlyingObject()
            $nativeGroup.Add($userEntry.Path)
        }
        Write-Output "Added user '$Account' to group '$($Group.Name)'."
    }
    catch [System.Runtime.InteropServices.COMException] {
        if ($_.Exception.HResult -eq -2147019886) {   # 0x80071392 - already a member
            Write-Output "User '$Account' is already a member of '$($Group.Name)' (0x80071392)."
            return
        }
        throw
    }
}

try {
    $groupResult = Resolve-Principal -Name $GroupName

    # Self-guard: act only in the context that matches the group type. The other
    # deferred instance handles the other type.
    $expectedContext = if ($groupResult.IsLocal) { 'System' } else { 'Caller' }
    if ($Context -ne $expectedContext) {
        Write-Output "Group '$GroupName' is $($expectedContext.ToLower())-handled; skipping in the $Context context."
        exit 0
    }

    if ($null -eq $groupResult.Principal) {
        throw "The group '$GroupName' could not be found."
    }

    $userResult = Resolve-Principal -Name $Account
    if ($null -eq $userResult.Principal) {
        throw "The user '$Account' could not be found."
    }

    Add-UserToGroup -Account $Account -Group $groupResult.Principal -User $userResult.Principal
    exit 0
}
catch {
    # The installing user may lack rights to modify the group; that does not block the
    # install. Warn and continue. The AutoSync service enforces FIMSyncAdmins/Operators
    # membership at startup and will not start until this account is a member, so the
    # warning must be acted on.
    #
    # Only the Caller context can fail here (a domain group the installing user cannot
    # modify); a local-group add as SYSTEM cannot. The Caller instance runs as an immediate
    # custom action and records the failure in a property for the ExitDialog message box.
    # AI_SetMsiProperty does nothing in a deferred/System context, so it is guarded.
    if ($Context -eq 'Caller') {
        try { AI_SetMsiProperty AI_FIM_ADMINS_ADD_FAILED 1 } catch { }
    }
    Write-Warning ("Could not add '{0}' to the group '{1}'. The AutoSync service will NOT start until this account is a member of that group -- add it manually (the installing user may lack permission to modify the group). Details: {2}" -f $Account, $GroupName, $_.Exception.Message)
    exit 0
}
