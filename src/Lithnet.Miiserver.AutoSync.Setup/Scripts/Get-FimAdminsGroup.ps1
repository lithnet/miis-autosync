<#
.SYNOPSIS
    Resolves the MIM Synchronization Service "administrators" group and writes its
    account name into the GROUP_FIM_SYNC_ADMINS_NAME installer property.

.DESCRIPTION
    The group is stored as a SID in the MIM Sync database. The script reads the DB
    connection parameters from the FIM Synchronization Service registry key, queries the
    administrators_sid column, converts the SID to an NT account name, and sets the
    GROUP_FIM_SYNC_ADMINS_NAME property.

    Runs as an immediate action (AI_SetMsiProperty only works for immediate actions) in
    the installing user's impersonated context, because the SQL connection uses Integrated
    Security and must not run as SYSTEM.

    This only pre-fills the default for the service-details dialog; if detection fails the
    user types the group in manually. Failures are logged and swallowed so they do not
    abort the install, and no fabricated default is substituted.
#>

Set-StrictMode -Version Latest

function Get-MiisAdministratorsGroupName {
    [CmdletBinding()]
    param()

    $regPath = 'HKLM:\SYSTEM\CurrentControlSet\services\FIMSynchronizationService\Parameters'

    if (-not (Test-Path $regPath)) {
        throw "The FIM Synchronization Service is not installed on this machine (registry key '$regPath' not found)."
    }

    $params = Get-ItemProperty -Path $regPath

    $server   = if ([string]::IsNullOrWhiteSpace($params.Server)) { 'localhost' } else { $params.Server }
    $instance = $params.SQLInstance
    $database = if ([string]::IsNullOrWhiteSpace($params.DBName)) { 'FIMSynchronizationService' } else { $params.DBName }

    $dataSource = if ([string]::IsNullOrWhiteSpace($instance)) { $server } else { "$server\$instance" }
    $connectionString = "Server=$dataSource;Database=$database;Integrated Security=true;"

    $connection = New-Object System.Data.SqlClient.SqlConnection $connectionString
    try {
        $command = $connection.CreateCommand()
        $command.CommandText = 'SELECT TOP 1 administrators_sid FROM dbo.mms_server_configuration'
        $connection.Open()

        $sidBytes = $command.ExecuteScalar()
        if ($null -eq $sidBytes -or $sidBytes -isnot [byte[]]) {
            return $null
        }

        $sid = New-Object System.Security.Principal.SecurityIdentifier ($sidBytes, 0)
        return $sid.Translate([System.Security.Principal.NTAccount]).Value
    }
    finally {
        $connection.Dispose()
    }
}

# --- Advanced Installer glue (best-effort; never fail the install) -------------
try {
    $groupName = Get-MiisAdministratorsGroupName
    if (-not [string]::IsNullOrWhiteSpace($groupName)) {
        AI_SetMsiProperty GROUP_FIM_SYNC_ADMINS_NAME $groupName
    }
}
catch {
    # Log and continue: the dialog lets the admin enter the group manually.
    Write-Output "Could not auto-detect the MIM sync administrators group: $($_.Exception.Message)"
}
