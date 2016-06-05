function Get-RunProfileToExecute 
{
	$table = Invoke-SQL "localhost" "lithnet.acma" "SELECT TOP 1 rowversion FROM DBO.MA_OBJECTS_DELTA";

	if ($table.Rows.Count -gt 0)
	{
        $thisRowVersion = [System.Convert]::ToBase64String($table.Rows[0].rowversion);

        if($lastRowVersion -ne $thisRowVersion)
        {
            $lastRowVersion = $thisRowVersion;
			$p = New-Object Lithnet.Miiserver.Autosync.ExecutionParameters;
			$p.RunProfileType = "DeltaImport";
			$p.Exclusive = $false;
    		write-output $p
        }
	}
}

function Invoke-SQL {
    param(
        [string] $dataSource = "localhost",
        [string] $database = "Lithnet.Acma",
        [string] $sqlCommand = $(throw "Please specify a query.")
      )

    $connectionString = "Data Source=$dataSource; " +
            "Integrated Security=SSPI; " +
            "Initial Catalog=$database"

    $connection = new-object system.data.SqlClient.SQLConnection($connectionString)
    $command = new-object system.data.sqlclient.sqlcommand($sqlCommand,$connection)
    $connection.Open()

    $adapter = New-Object System.Data.sqlclient.sqlDataAdapter $command
    $dataset = New-Object System.Data.DataSet
    $adapter.Fill($dataSet) | Out-Null

    $connection.Close()
    $dataSet.Tables
}

$lastRowVersion = "";
