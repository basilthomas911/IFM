# PowerShell script to update FundCommandActorTests.cs
# This script updates all test methods to use ICommand parameter instead of string verb

$filePath = "TomasAI.IFM.Domain.Fund.Actor.UnitTests\FundCommandActorTests.cs"
$content = Get-Content -Path $filePath -Raw

# Pattern replacements for all InvokeReceiveAsync calls
$content = $content -replace 'await actor\.InvokeReceiveAsync\(context, (?:fundState|state), AddOrderToFundCommand\.Verb\);', 'await actor.InvokeReceiveAsync(context, $1, addOrderCommand);'
$content = $content -replace 'await actor\.InvokeReceiveAsync\(context, fundState, AddTradeToFundOrderCommand\.Verb\);', 'await actor.InvokeReceiveAsync(context, fundState, addTradeCommand);'
$content = $content -replace 'await actor\.InvokeReceiveAsync\(context, fundState, ChangeFundOrderTradeStateCommand\.Verb\);', 'await actor.InvokeReceiveAsync(context, fundState, cmd);'
$content = $content -replace 'await actor\.InvokeReceiveAsync\(context, state, CloseFundOrderCommand\.Verb\);', 'await actor.InvokeReceiveAsync(context, state, cmd);'
$content = $content -replace 'await actor\.InvokeReceiveAsync\(context, state, CreateFundCommand\.Verb\s*\);', 'await actor.InvokeReceiveAsync(context, state, cmd);'
$content = $content -replace 'await actor\.InvokeReceiveAsync\(context, fundState, GenerateFundMaxProfitCommand\.Verb\);', 'await actor.InvokeReceiveAsync(context, fundState, cmd);'
$content = $content -replace 'await actor\.InvokeReceiveAsync\(context, state, RemoveOrderFromFundCommand\.Verb\);', 'await actor.InvokeReceiveAsync(context, state, cmd);'
$content = $content -replace 'await actor\.InvokeReceiveAsync\(context, (?:fundState|state), RemoveTradeFromFundOrderCommand\.Verb\);', 'await actor.InvokeReceiveAsync(context, $1, cmd);'

# Pattern replacements for all InvokeOnValidateAsync calls
$content = $content -replace 'await actor\.InvokeOnValidateAsync\(context, cmd\.Subject\.ThreadId, CreateFundCommand\.Verb\);', 'await actor.InvokeOnValidateAsync(context, cmd.Subject.ThreadId, cmd);'
$content = $content -replace 'await actor\.InvokeOnValidateAsync\(context, cmd\.Subject\.ThreadId, AddOrderToFundCommand\.Verb\);', 'await actor.InvokeOnValidateAsync(context, cmd.Subject.ThreadId, cmd);'
$content = $content -replace 'await actor\.InvokeOnValidateAsync\(context, cmd\.Subject\.ThreadId, RemoveOrderFromFundCommand\.Verb\);', 'await actor.InvokeOnValidateAsync(context, cmd.Subject.ThreadId, cmd);'
$content = $content -replace 'await actor\.InvokeOnValidateAsync\(context, cmd\.Subject\.ThreadId, AddTradeToFundOrderCommand\.Verb\);', 'await actor.InvokeOnValidateAsync(context, cmd.Subject.ThreadId, cmd);'
$content = $content -replace 'await actor\.InvokeOnValidateAsync\(context, cmd\.Subject\.ThreadId, ChangeFundOrderTradeStateCommand\.Verb\);', 'await actor.InvokeOnValidateAsync(context, cmd.Subject.ThreadId, cmd);'
$content = $content -replace 'await actor\.InvokeOnValidateAsync\(context, cmd\.Subject\.ThreadId, RemoveTradeFromFundOrderCommand\.Verb\);', 'await actor.InvokeOnValidateAsync(context, cmd.Subject.ThreadId, cmd);'
$content = $content -replace 'await actor\.InvokeOnValidateAsync\(context, cmd\.Subject\.ThreadId, CloseFundOrderCommand\.Verb\);', 'await actor.InvokeOnValidateAsync(context, cmd.Subject.ThreadId, cmd);'
$content = $content -replace 'await actor\.InvokeOnValidateAsync\(context, cmd\.Subject\.ThreadId, GenerateFundMaxProfitCommand\.Verb\);', 'await actor.InvokeOnValidateAsync(context, cmd.Subject.ThreadId, cmd);'

# Pattern replacements for OnValidateAsync calls with context null!
$content = $content -replace 'await actor\.InvokeOnValidateAsync\(null!, cmd!\.Subject\.ThreadId, CreateFundCommand\.Verb\);', 'await actor.InvokeOnValidateAsync(null!, cmd!.Subject.ThreadId, cmd);'
$content = $content -replace 'await actor\.InvokeOnValidateAsync\(context, null!, CreateFundCommand\.Verb\);', 'await actor.InvokeOnValidateAsync(context, null!, cmd);'

# Pattern replacements for all InvokeOnExceptionAsync calls
$content = $content -replace 'await actor\.InvokeOnExceptionAsync\(context, cmd\.Subject\.ThreadId, AddTradeToFundOrderCommand\.Verb, ex\);', 'await actor.InvokeOnExceptionAsync(context, cmd.Subject.ThreadId, cmd, ex);'
$content = $content -replace 'await actor\.InvokeOnExceptionAsync\(context, cmd\.Subject\.ThreadId, ChangeFundOrderTradeStateCommand\.Verb, ex\);', 'await actor.InvokeOnExceptionAsync(context, cmd.Subject.ThreadId, cmd, ex);'
$content = $content -replace 'await actor\.InvokeOnExceptionAsync\(context, cmd\.Subject\.ThreadId, RemoveTradeFromFundOrderCommand\.Verb, ex\);', 'await actor.InvokeOnExceptionAsync(context, cmd.Subject.ThreadId, cmd, ex);'
$content = $content -replace 'await actor\.InvokeOnExceptionAsync\(context, cmd\.Subject\.ThreadId, RemoveOrderFromFundCommand\.Verb, ex\);', 'await actor.InvokeOnExceptionAsync(context, cmd.Subject.ThreadId, cmd, ex);'
$content = $content -replace 'await actor\.InvokeOnExceptionAsync\(context, cmd\.Subject\.ThreadId, CreateFundCommand\.Verb, ex\);', 'await actor.InvokeOnExceptionAsync(context, cmd.Subject.ThreadId, cmd, ex);'
$content = $content -replace 'await actor\.InvokeOnExceptionAsync\(context, cmd\.Subject\.ThreadId, CloseFundOrderCommand\.Verb, ex\);', 'await actor.InvokeOnExceptionAsync(context, cmd.Subject.ThreadId, cmd, ex);'
$content = $content -replace 'await actor\.InvokeOnExceptionAsync\(context, threadId, CreateFundCommand\.Verb, ex\);', 'await actor.InvokeOnExceptionAsync(context, threadId, cmd, ex);'

# Pattern replacements for OnLoadStateAsync calls
$content = $content -replace '\(\(ICommandActor<FundCommandActor>\)actor\)\.OnLoadStateAsync\(context, cmd!\.Subject\.ThreadId, CreateFundCommand\.Verb\)', '((ICommandActor<FundCommandActor>)actor).OnLoadStateAsync(context, cmd!.Subject.ThreadId, cmd)'
$content = $content -replace '\(\(ICommandActor<FundCommandActor>\)actor\)\.OnLoadStateAsync\(null!, cmd!\.Subject\.ThreadId, CreateFundCommand\.Verb\)', '((ICommandActor<FundCommandActor>)actor).OnLoadStateAsync(null!, cmd!.Subject.ThreadId, cmd)'
$content = $content -replace '\(\(ICommandActor<FundCommandActor>\)actor\)\.OnLoadStateAsync\(context, null!, CreateFundCommand\.Verb\)', '((ICommandActor<FundCommandActor>)actor).OnLoadStateAsync(context, null!, cmd)'

# Pattern replacements for OnSaveStateAsync calls
$content = $content -replace '\(\(ICommandActor<FundCommandActor>\)actor\)\.OnSaveStateAsync\(context, cmd\.Subject\.ThreadId, fundState, CreateFundCommand\.Verb\)', '((ICommandActor<FundCommandActor>)actor).OnSaveStateAsync(context, cmd.Subject.ThreadId, fundState, cmd)'
$content = $content -replace '\(\(ICommandActor<FundCommandActor>\)actor\)\.OnSaveStateAsync\(null!, cmd!\.Subject\.ThreadId, fundState, CreateFundCommand\.Verb\)', '((ICommandActor<FundCommandActor>)actor).OnSaveStateAsync(null!, cmd!.Subject.ThreadId, fundState, cmd)'
$content = $content -replace '\(\(ICommandActor<FundCommandActor>\)actor\)\.OnSaveStateAsync\(context, null!, fundState, CreateFundCommand\.Verb\)', '((ICommandActor<FundCommandActor>)actor).OnSaveStateAsync(context, null!, fundState, cmd)'
$content = $content -replace '\(\(ICommandActor<FundCommandActor>\)actor\)\.OnSaveStateAsync\(context, cmd!\.Subject\.ThreadId, null!, CreateFundCommand\.Verb\)', '((ICommandActor<FundCommandActor>)actor).OnSaveStateAsync(context, cmd!.Subject.ThreadId, null!, cmd)'
$content = $content -replace '\(\(ICommandActor<FundCommandActor>\)actor\)\.OnSaveStateAsync\(context, cmd\.Subject\.ThreadId, notFundState, CreateFundCommand\.Verb\)', '((ICommandActor<FundCommandActor>)actor).OnSaveStateAsync(context, cmd.Subject.ThreadId, notFundState, cmd)'

# Remove duplicate lines that may have been created
$content = $content -replace '(?m)^(\s*)(var result = await actor\.InvokeReceiveAsync\(context, (?:state|fundState), (?:cmd|addOrderCommand|addTradeCommand)\);)\r?\n\1\1', '$1$2' + [Environment]::NewLine

# Save the updated content
Set-Content -Path $filePath -Value $content -NoNewline

Write-Host "File updated successfully: $filePath"
Write-Host "Please review the changes before committing."
