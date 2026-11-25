# Runbook

## Error rate
Open the app in New Relic, check Errors inbox for the new error class, and compare with the last deployment marker. If the rate rose right after a deploy, roll back first and investigate second.

## Latency
Check the slowest transaction traces and database time. A rise right after a deploy points at the release; a rise with no deploy points at a dependency.

## No deploys
No deployment marker for 7 days means either nothing shipped or the marker step is failing. Check the release job log for `New Relic rejected the deployment`.
