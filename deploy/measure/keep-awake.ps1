# Windows stand-in for `caffeinate -dimsu`: keeps the system and display awake while it runs a bash
# script, then releases the request. Started hidden by run-overnight.sh, so closing the terminal
# does not stop it.
param(
    [Parameter(Mandatory = $true)][string]$Bash,
    [Parameter(Mandatory = $true)][string]$Script,
    [Parameter(Mandatory = $true)][string]$Duration,
    [Parameter(Mandatory = $true)][string]$Log
)

Add-Type -Namespace SatTrakk -Name Power -MemberDefinition @'
[System.Runtime.InteropServices.DllImport("kernel32.dll")]
public static extern uint SetThreadExecutionState(uint esFlags);
'@

# ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED, held by this thread until reset.
[void][SatTrakk.Power]::SetThreadExecutionState([uint32]"0x80000003")
try {
    # measure.sh appends its own output to MEASURE_LOG. No pipe or redirection here: PowerShell
    # would wait for every process still holding the pipe (orphaned `sleep`s after a stop), and
    # its own redirection writes UTF-16.
    $env:MEASURE_LOG = $Log
    & $Bash $Script $Duration
}
finally {
    [void][SatTrakk.Power]::SetThreadExecutionState([uint32]"0x80000000")
}
