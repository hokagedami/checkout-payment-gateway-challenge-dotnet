# Run E2E tests using Docker Compose
# This script starts all required services and runs the tests

$ErrorActionPreference = "Stop"

# Create timestamped test results directory
$timestamp = Get-Date -Format "yyyy-MM-dd_HH-mm-ss"
$resultsDir = "test-results\$timestamp"
New-Item -ItemType Directory -Force -Path $resultsDir | Out-Null

Write-Host "Starting E2E Test Suite..." -ForegroundColor Cyan
Write-Host "Results will be saved to: $resultsDir" -ForegroundColor Cyan
Write-Host ""

# Clean up any existing containers
Write-Host "Cleaning up existing containers..." -ForegroundColor Yellow
docker-compose -f docker-compose.test.yml down -v

# Build and run tests
Write-Host ""
Write-Host "Building services and running tests..." -ForegroundColor Yellow

# Update docker-compose to use timestamped directory
$env:TEST_RESULTS_DIR = $resultsDir
docker-compose -f docker-compose.test.yml up --build --abort-on-container-exit --exit-code-from test_runner

# Capture exit code
$TEST_EXIT_CODE = $LASTEXITCODE

# Clean up
Write-Host ""
Write-Host "Cleaning up containers..." -ForegroundColor Yellow
docker-compose -f docker-compose.test.yml down -v

# Parse and display test results
Write-Host ""
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "                    TEST RESULTS SUMMARY                        " -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""

# Check if TRX file exists
$trxFile = Get-ChildItem -Path $resultsDir -Filter "e2e-results.trx" -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1

if ($trxFile) {
    [xml]$trxContent = Get-Content $trxFile.FullName
    $counters = $trxContent.TestRun.ResultSummary.Counters

    $total = $counters.total
    $passed = $counters.passed
    $failed = $counters.failed
    $duration = [datetime]$trxContent.TestRun.Times.finish - [datetime]$trxContent.TestRun.Times.start

    Write-Host "Total Tests: $total" -ForegroundColor White
    Write-Host "Passed: $passed" -ForegroundColor Green
    if ($failed -gt 0) {
        Write-Host "Failed: $failed" -ForegroundColor Red
    } else {
        Write-Host "Failed: 0" -ForegroundColor Gray
    }
    Write-Host "Duration: $([math]::Round($duration.TotalSeconds, 2))s" -ForegroundColor White
    Write-Host ""

    # List failed tests if any
    if ($failed -gt 0) {
        Write-Host "Failed Tests:" -ForegroundColor Red
        $failedTests = $trxContent.TestRun.Results.UnitTestResult | Where-Object { $_.outcome -eq "Failed" }
        foreach ($test in $failedTests) {
            $testName = $test.testName
            $errorMessage = $test.Output.ErrorInfo.Message
            Write-Host "  - $testName" -ForegroundColor Red
            if ($errorMessage) {
                Write-Host "    $errorMessage" -ForegroundColor DarkRed
            }
        }
        Write-Host ""
    }

    Write-Host "Detailed Results:" -ForegroundColor Cyan
    Write-Host "  TRX Report: $($trxFile.FullName)" -ForegroundColor Gray

    $htmlFile = Get-ChildItem -Path $resultsDir -Filter "e2e-results.html" -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($htmlFile) {
        Write-Host "  HTML Report: $($htmlFile.FullName)" -ForegroundColor Gray
    }
} else {
    Write-Host "Could not find test results file" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""

if ($TEST_EXIT_CODE -eq 0) {
    Write-Host "E2E tests passed!" -ForegroundColor Green
} else {
    Write-Host "E2E tests failed with exit code $TEST_EXIT_CODE" -ForegroundColor Red
}

exit $TEST_EXIT_CODE
