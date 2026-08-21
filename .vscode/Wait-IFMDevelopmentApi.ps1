[CmdletBinding()]
param(
    [string]$ReadinessUri = "http://localhost:22543/health/ready",
    [ValidateRange(1, 1800)]
    [int]$TimeoutSeconds = 300
)

$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
Write-Host "Waiting for the IFM Development API at $ReadinessUri ..."

do {
    try {
        $response = Invoke-WebRequest -Uri $ReadinessUri -UseBasicParsing -TimeoutSec 3
        if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 300) {
            Write-Host "IFM Development API is ready."
            exit 0
        }
    }
    catch {
        # Startup connection failures are expected until Kestrel and all hosted services are ready.
    }

    Start-Sleep -Milliseconds 500
} while ((Get-Date) -lt $deadline)

throw "IFM Development API did not become ready within $TimeoutSeconds seconds: $ReadinessUri"
