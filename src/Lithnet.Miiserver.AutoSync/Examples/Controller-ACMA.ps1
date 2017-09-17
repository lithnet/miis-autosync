# This function is called by the MA controller before a run profile is executed. Return $false to prevent the run profile from being executed
# Returning $true or nothing will allow the run profile to execute
function ShouldExecute
{
	param([string]$runProfileName)


	Write-Object $true;
}

# This function is called by the MA controller after a run profile is executed. Execution of the MA or the entire service can be halted by throwing an
# UnexpectedChangeException
function ExecutionComplete
{
	param([Lithnet.Miiserver.Client.RunDetails]$lastRunDetails)
	
	# If the run did not return 'success' then send an email with the details of the run
	if ($lastRunDetails.LastStepStatus -ne 'success')
	{
		$maName = $lastRunDetails.MAName;
		$runProfile = $lastRunDetails.RunProfileName;
		$runProfileResult = $lastRunDetails.LastStepStatus;

		$message = "The management agent $maname returned $runProfileResult from run profile $runProfile"
		Send-MailMessage `
						-to $mailTo `
						-from $mailFrom `
						-subject "$maName $runProfile $runProfileResult" `
						-SmtpServer $smtpServer `
						-Body $message
	}

	# If the run was an import that returned more than 1000 changes, throw an exception that will terminate the service
	foreach ($step in $lastRunDetails.StepDetails)
	{
	if ($step.StepDefinition.Type -eq 'DeltaImport' -or
		$step.StepDefinition.Type -eq 'FullImport')
		{
			if ($step.StagingCounters.StageChanged -gt 1000)
			{
				$maName = $lastRunDetails.MAName;
				$runProfile = $lastRunDetails.RunProfileName;

				$message = "The management agent $maname has over 1000 pending import changes from run profile $runProfile and has been stopped. Perform a manual synchronization if these changes are expected and restart the auto sync service"
				Send-MailMessage `
								-to $mailTo `
								-from $mailFrom `
								-subject "AUTOSYNC SERVICE STOPPED - $maName" `
								-SmtpServer $smtpServer `
								-Body $message
				
				# Throwing the exception with a $true parameter will force the service to stop
				# Throwing the exception with a $false parameter will stop further runs on this MA only until the service is restarted.

				throw [Lithnet.Miiserver.AutoSync.UnexpectedChangeException] $true;
			}
		}
		elseif ($step.StepDefinition.Type -eq 'Synchronization')
		{
			foreach($flows in $step.OutboundFlowCounters)
			{
				if ($flows.OutboundFlowChanges -gt 100)
				{
					$maName = $lastRunDetails.MAName;
					$runProfile = $lastRunDetails.RunProfileName;
					$outboundMAName = $flows.ManagementAgent
					$message = "The synchronization operation $runProfile on management agent $maname triggered over 100 pending export operations on $outboundMAName and has been stopped. Perform a manual export if these changes are expected and restart the auto sync service"
					Send-MailMessage `
									-to $mailTo `
									-from $mailFrom `
									-subject "AUTOSYNC SERVICE STOPPED - $maName" `
									-SmtpServer $smtpServer `
									-Body $message
				
					# Throwing the exception with a $true parameter will force the service to stop
					# Throwing the exception with a $false parameter will stop further runs on this MA only until the service is restarted.

					throw [Lithnet.Miiserver.AutoSync.UnexpectedChangeException] $true;
				}
			}
		}
	}
}

$mailTo = "ryan@lithiumblue.com";
$mailFrom = "$env:computername@autosync.lithiumblue.com";
$smtpServer = "smtp.lithiumblue.com";
