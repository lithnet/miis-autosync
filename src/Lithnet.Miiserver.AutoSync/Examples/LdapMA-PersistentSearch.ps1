$hostname = "fimldap";
$baseDN = "o=Lithnet, c=AU"
$username = "" #"cn=Directory Manager,o=Lithnet";
$password =  ""
$filter =  "(objectClass=*)"

<# DO NOT MODIFY BELOW THIS LINE #>
add-type -assemblyname system.directoryservices.protocols
add-type -assemblyname system.net

$searchRequest = New-Object System.DirectoryServices.Protocols.SearchRequest;
$control = New-Object Lithnet.Miiserver.AutoSync.PersistentSearchControl;
$searchRequest.Controls.Add($control);
$searchRequest.DistinguishedName = $baseDN;
$searchRequest.Filter = $filter;
$searchRequest.Scope = "Subtree";
$searchRequest.SizeLimit = 0;
$searchRequest.TimeLimit = 0;
$searchRequest.Attributes.add("dn");
$global:haschanged = $false;

if ($username -ne "")
{
	$creds = New-Object System.Net.NetworkCredential $username, $password
}
else
{
	$creds = $null;
}

$bridge = [Lithnet.Miiserver.AutoSync.CallBackEventBridge]::Create()

Register-ObjectEvent -input $bridge -EventName callbackcomplete -action {
  param([IAsyncResult]$asyncResult)
    
    $ldapConnection.GetPartialResults($asyncResult);
    $global:haschanged = $true;
  }

$ldapIdentifier = New-Object System.DirectoryServices.Protocols.LdapDirectoryIdentifier $hostname;


if ($creds -eq $null)
{
	$ldapConnection = New-Object System.DirectoryServices.Protocols.LdapConnection $ldapIdentifier;
	$ldapConnection.AuthType = "Anonymous"    
}
else
{
	$ldapConnection = New-Object System.DirectoryServices.Protocols.LdapConnection $ldapIdentifier, $creds;
	$ldapConnection.AuthType = "Basic"    
}

$ldapConnection.AutoBind = $true;
$ldapConnection.SessionOptions.ProtocolVersion = 3
$r = $ldapConnection.BeginSendRequest($searchRequest, [TimeSpan]::FromDays(1000), "ReturnPartialResultsAndNotifyCallback", $bridge.Callback, $searchRequest)

function Get-RunProfileToExecute 
{
	if ($global:haschanged -eq $true)
	{
		$global:haschanged = $false;
		$p = New-Object Lithnet.Miiserver.Autosync.ExecutionParameters;
		$p.RunProfileType = "DeltaImport";
    	write-output $p
	}
}