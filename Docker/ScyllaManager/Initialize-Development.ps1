[CmdletBinding()]
param(
    [string] $BackupRoot = 'E:\IFM\DatabaseBackup'
)

$ErrorActionPreference = 'Stop'
$resolvedRoot = [System.IO.Path]::GetFullPath($BackupRoot)
$expectedRoot = [System.IO.Path]::GetFullPath('E:\IFM\DatabaseBackup')
if (-not $resolvedRoot.StartsWith($expectedRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "The development Scylla Manager root must remain below $expectedRoot."
}

$directories = @(
    'scylla-manager\object-storage',
    'scylla-manager\safety',
    'scylla-manager\validation',
    'secrets',
    'tools\scylla-manager'
)
foreach ($relativePath in $directories) {
    $path = [System.IO.Path]::GetFullPath((Join-Path $resolvedRoot $relativePath))
    if (-not $path.StartsWith($resolvedRoot + '\', [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Resolved path is outside the approved backup root: $path"
    }
    [System.IO.Directory]::CreateDirectory($path) | Out-Null
}

function New-RandomBase64Secret([int] $byteCount) {
    $bytes = New-Object byte[] $byteCount
    $generator = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try {
        $generator.GetBytes($bytes)
    }
    finally {
        $generator.Dispose()
    }
    return [Convert]::ToBase64String($bytes)
}

$configPath = Join-Path $resolvedRoot 'secrets\scylla-manager-agent.yaml'
$minioEnvironmentPath = Join-Path $resolvedRoot 'secrets\scylla-manager-minio.env'
$existingConfiguration = if ([System.IO.File]::Exists($configPath)) {
    [System.IO.File]::ReadAllText($configPath)
}
else {
    ''
}

$existingTokenMatch = [regex]::Match($existingConfiguration, '(?m)^auth_token:\s*(?<token>\S+)\s*$')
$token = if ($existingTokenMatch.Success) { $existingTokenMatch.Groups['token'].Value } else { New-RandomBase64Secret 64 }

if (-not [System.IO.File]::Exists($minioEnvironmentPath)) {
    $minioUser = 'ifm-scylla-backup'
    $minioPassword = New-RandomBase64Secret 48
    $minioEnvironment = "MINIO_ROOT_USER=$minioUser`nMINIO_ROOT_PASSWORD=$minioPassword`n"
    $utf8WithoutBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($minioEnvironmentPath, $minioEnvironment, $utf8WithoutBom)
}

$minioVariables = @{}
foreach ($line in [System.IO.File]::ReadAllLines($minioEnvironmentPath)) {
    if ($line -match '^(?<name>[^#=]+)=(?<value>.+)$') {
        $minioVariables[$Matches['name']] = $Matches['value']
    }
}
if (-not $minioVariables.ContainsKey('MINIO_ROOT_USER') -or -not $minioVariables.ContainsKey('MINIO_ROOT_PASSWORD')) {
    throw "The MinIO environment file is missing required credentials: $minioEnvironmentPath"
}

$configuration = @"
auth_token: $token
https: 0.0.0.0:10001
scylla:
  api_address: 127.0.0.1
  api_port: 10000
s3:
  access_key_id: $($minioVariables['MINIO_ROOT_USER'])
  secret_access_key: $($minioVariables['MINIO_ROOT_PASSWORD'])
  provider: Minio
  endpoint: http://scylla-backup-s3:9000
"@

if ($configuration -ne $existingConfiguration) {
    $utf8WithoutBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($configPath, $configuration, $utf8WithoutBom)
}

Get-Item -LiteralPath $configPath | Select-Object FullName, Length, LastWriteTime
Get-Item -LiteralPath $minioEnvironmentPath | Select-Object FullName, Length, LastWriteTime
Get-ChildItem -Directory (Join-Path $resolvedRoot 'scylla-manager') | Select-Object FullName

$networkName = 'ifm_scylla_management'
$networkSubnet = '172.30.20.0/24'
$networkMatch = & docker network ls --filter "name=^$networkName`$" --format '{{.Name}}'
if ($networkMatch -ne $networkName) {
    & docker network create --driver bridge --subnet $networkSubnet $networkName | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Docker network '$networkName' could not be created."
    }
}
else {
    $networkObject = (& docker network inspect $networkName | ConvertFrom-Json | Select-Object -First 1)
    $configuredSubnet = $networkObject.IPAM.Config[0].Subnet
    if ($configuredSubnet -ne $networkSubnet) {
        throw "Docker network '$networkName' uses '$configuredSubnet', expected '$networkSubnet'."
    }
}

Write-Output "Docker network '$networkName' is ready on $networkSubnet."
