param(
    [string]$ServiceId = $env:RENDER_SERVICE_ID,
    [string]$ApiKey = $env:RENDER_API_KEY,
    [string]$Branch = "main"
)

if (-not $ApiKey) {
    Write-Error "Render API key not provided. Set RENDER_API_KEY environment variable or pass -ApiKey."
    exit 1
}

if (-not $ServiceId) {
    Write-Error "Render Service ID not provided. Set RENDER_SERVICE_ID environment variable or pass -ServiceId."
    exit 1
}

$uri = "https://api.render.com/v1/services/$ServiceId/deploys"

$body = @{ branch = $Branch } | ConvertTo-Json

try {
    $response = Invoke-RestMethod -Uri $uri -Method Post -Body $body -ContentType 'application/json' -Headers @{ Authorization = "Bearer $ApiKey" }
    Write-Output "Deploy triggered successfully. Deploy id: $($response.id)"
    Write-Output "Status: $($response.status)"
    Write-Output "See service dashboard: https://dashboard.render.com/services/$ServiceId"
} catch {
    Write-Error "Failed to trigger deploy: $_"
    if ($_.Exception.Response) {
        $resp = $_.Exception.Response.GetResponseStream() | New-Object System.IO.StreamReader
        Write-Error $resp.ReadToEnd()
    }
    exit 1
}
