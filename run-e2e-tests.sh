#!/bin/bash

# Run E2E tests using Docker Compose
# This script starts all required services and runs the tests

set -e

# Create timestamped test results directory
timestamp=$(date +"%Y-%m-%d_%H-%M-%S")
resultsDir="test-results/$timestamp"
mkdir -p "$resultsDir"

echo "Starting E2E Test Suite..."
echo "Results will be saved to: $resultsDir"
echo ""

# Clean up any existing containers
echo "Cleaning up existing containers..."
docker-compose -f docker-compose.test.yml down -v

# Build and run tests
echo ""
echo "Building services and running tests..."

# Update docker-compose to use timestamped directory
export TEST_RESULTS_DIR="$resultsDir"
docker-compose -f docker-compose.test.yml up --build --abort-on-container-exit --exit-code-from test_runner

# Capture exit code
TEST_EXIT_CODE=$?

# Clean up
echo ""
echo "Cleaning up containers..."
docker-compose -f docker-compose.test.yml down -v

# Parse and display test results
echo ""
echo "================================================================"
echo "                    TEST RESULTS SUMMARY                        "
echo "================================================================"
echo ""

# Check if TRX file exists
trxFile=$(find "$resultsDir" -name "e2e-results.trx" -type f 2>/dev/null | head -n 1)

if [ -n "$trxFile" ]; then
    # Parse TRX file using grep and sed
    total=$(grep -oP 'total="\K[^"]+' "$trxFile" | head -n 1)
    passed=$(grep -oP 'passed="\K[^"]+' "$trxFile" | head -n 1)
    failed=$(grep -oP 'failed="\K[^"]+' "$trxFile" | head -n 1)

    echo "Total Tests: $total"
    echo "Passed: $passed"
    if [ "$failed" -gt 0 ]; then
        echo "Failed: $failed"
    else
        echo "Failed: 0"
    fi
    echo ""

    # List failed tests if any
    if [ "$failed" -gt 0 ]; then
        echo "Failed Tests:"
        grep -oP 'testName="\K[^"]+' "$trxFile" | while read -r testName; do
            outcome=$(grep "$testName" "$trxFile" | grep -oP 'outcome="\K[^"]+' | head -n 1)
            if [ "$outcome" = "Failed" ]; then
                echo "  - $testName"
            fi
        done
        echo ""
    fi

    echo "Detailed Results:"
    echo "  TRX Report: $trxFile"

    htmlFile=$(find "$resultsDir" -name "e2e-results.html" -type f 2>/dev/null | head -n 1)
    if [ -n "$htmlFile" ]; then
        echo "  HTML Report: $htmlFile"
    fi
else
    echo "Could not find test results file"
fi

echo ""
echo "================================================================"
echo ""

if [ $TEST_EXIT_CODE -eq 0 ]; then
    echo "E2E tests passed!"
else
    echo "E2E tests failed with exit code $TEST_EXIT_CODE"
fi

exit $TEST_EXIT_CODE
