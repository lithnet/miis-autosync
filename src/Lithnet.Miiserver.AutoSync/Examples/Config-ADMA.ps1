function Get-MAConfiguration
{

    Write-Output @{
    # The run profile to execute after an export to confirm the exported changed
    ConfirmingImportRunProfileName = "DI";
            
    # The run profile to execute when polling for changes
    ScheduledImportRunProfileName = "DI";

    # The run profile to use to export pending changes 
    ExportRunProfileName = "EALL";

    # The management agent's delta import profile. Specify the name of the full import profile if delta imports are not supported
    DeltaImportRunProfileName = "DI";

    # The management agent's delta sync profile
    DeltaSyncRunProfileName =  "DS";
                
    # The management agent's full import profile 
    FullImportRunProfileName = "FI";

    # The management agent's full sync profile
    FullSyncRunProfileName = "FS";

    # Indicates that the management agent should be executed 
    Disabled = $false;

    # Specifies the type of import scheduling to use. 
    #	 Disabled = Stops the configuration of an automatic import schedule
    #	 Enabled =  Enables the automatic import schedule. The schedule will be triggered according to the AutoImportIntervalMinutes value 
    #    Default =  Enables the automatic import schedule only if the MA has more import attribute flows than export attribute flows. If the MA has more EAFs, it is
    #			    considered a 'target' system, and only confirming imports will be performed.
    AutoImportScheduling = "Default";

    # The amount of time that can pass before an import is performed. Note that a scheduled import is only performed if another operation has not triggered
    # an import within the specified interval period. If this value is 0, then the scheduler will examine the run history to determine how often imports have
    # been run in the past, and derrive an appropriate value accordingly.
    AutoImportIntervalMinutes = 15;

    # Setting this to true will prevent automatic change detection in Active Directory and FIM Service
    DisableDefaultTriggers = $true;
    }
    
    # Create a new ADListenerConfig item to override the automatic discovery;
    
    $ADListenerConfig = New-Object Lithnet.Miiserver.AutoSync.ActiveDirectoryChangeTrigger;
    $ADListenerConfig.BaseDN = "OU=IdM Managed Objects,dc=zaf,dc=lithnet,dc=local";
    $ADListenerConfig.HostName = "zaf.lithnet.local";
    $ADListenerConfig.LastLogonTimestampOffset = New-Object System.TimeSpan -ArgumentList @(0,0,120);
    $ADListenerConfig.MaximumTriggerInterval= New-Object System.TimeSpan -ArgumentList @(0,0,600);
    $ADlistenerConfig.ObjectClasses = @("user","group");
    $ADListenerConfig.Credentials = New-Object System.Net.NetworkCredential "svc-FIMADAccess", "<password>";

    # Send the object to the pipeline
    Write-Output $ADListenerConfig;
}
