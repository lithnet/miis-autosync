function Get-RunProfileToExecute 
{
    # Runs a full import nightly at 2am
    $hourOfDay = 2; # 2am


    # Do not modify below this line

    [DateTime]$current = [DateTime]::Now;
    [DateTime]$target = $current.Date.AddHours($hourOfDay);
    if ($target -lt $current)
    {
        $target = $target.AddDays(1); 
    }
   
    [TimeSpan]$waitInterval = $target - $current;

    Sleep $waitInterval.TotalSeconds;

    Write-Output "FI";
}
