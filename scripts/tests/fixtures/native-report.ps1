param([int]$ExitCode = 0)
[Console]::Error.WriteLine('benign diagnostic from native stderr')
[Console]::Out.WriteLine('{"version":1,"projects":[{"path":"sample.csproj"}]}')
exit $ExitCode
