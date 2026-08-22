[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string[]] $StackNames,
    [Parameter(Mandatory)] [string] $Region
)

$ErrorActionPreference = 'Stop'
$safe = [ordered]@{}
foreach ($stackName in $StackNames) {
    $stack = (& aws cloudformation describe-stacks --stack-name $stackName --region $Region --output json | ConvertFrom-Json).Stacks[0]
    foreach ($output in $stack.Outputs) {
        if ($output.OutputKey -notmatch '(Arn|Name)$') { continue }
        $safe[$output.OutputKey] = $output.OutputValue
    }
}
$safe | ConvertTo-Json -Depth 4
