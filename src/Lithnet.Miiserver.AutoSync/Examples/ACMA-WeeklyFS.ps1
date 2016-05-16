function Get-RunProfileToExecute 
{
    # Runs a full sync every saturday night at 3am
    $targetDayOfWeek = [int32][DayOfWeek]::Saturday;
	$hourOfDay = 3;

	# Do not modify below this line
    [DateTime]$current = [DateTime]::Now;
    [DateTime]$target = $current.Date.AddHours($hourOfDay); 
    
    [int32]$startDay = [int32]$target.DayOfWeek;

    if ($targetDayOfWeek -lt $startDay)
    {
        $targetDayOfWeek += 7;
    }

    $target = $target.AddDays($targetDayOfWeek - $startDay);
   
    if ($target -lt $current)
    {
        $target = $target.AddDays(7);
    }

    [TimeSpan]$waitInterval = $target - $current;

    Sleep $waitInterval.TotalSeconds;

    # Create the execution parameters, specifying that this job should run exclusively. All other runs will need to complete
    # before this can start, and no other runs will be allowed to execute until this has completed
    $p = New-Object Lithnet.Miiserver.Autosync.ExecutionParameters;
    $p.RunProfileName = "FS";
    $p.Exclusive = $true;
    write-output $p
}
