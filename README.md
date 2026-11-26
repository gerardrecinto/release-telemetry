# release-telemetry

[![CI](https://github.com/gerardrecinto/release-telemetry/actions/workflows/ci.yml/badge.svg)](https://github.com/gerardrecinto/release-telemetry/actions/workflows/ci.yml)

Release health on New Relic, as code: a C# tool that records a deployment on an entity, a Ruby audit that checks alert definitions before they ship, and Terraform that builds the alert policy from the same YAML.

See "What is verified".

## Run it

```bash
git clone https://github.com/gerardrecinto/release-telemetry && cd release-telemetry
# Ruby audit and specs (Ruby 3.x)
(cd ruby && bundle install && bundle exec rspec && ruby bin/alert-audit)
# C# tests and a dry run (.NET 8 SDK)
(cd dotnet && dotnet test DeployMarker.Tests && dotnet run --project DeployMarker -- --entity-guid GUID --version 1.4.2 --commit abc123 --dry-run)
# Terraform
(cd terraform && terraform init -backend=false && terraform validate)
```

No Ruby or .NET installed? Run the same commands inside `ruby:3.3` and `mcr.microsoft.com/dotnet/sdk:8.0` containers, as CI does for the checks.

## What is in it

| Piece | What it does |
|---|---|
| `alerts/alerts.yml` | Three alert conditions: error rate, p95 latency, no deploys for 7 days. Each has a query, threshold, window and runbook link. |
| `ruby/` | `alert-audit` applies rules A0 to A9 to the YAML: https runbook, no `SELECT *`, query scoped to an app, numeric threshold, window of 3 to 120 minutes, no repeated names. Exits 1 on any finding. 10 RSpec examples. |
| `terraform/` | `newrelic_alert_policy` and one `newrelic_nrql_alert_condition` per entry in the YAML. |
| `dotnet/DeployMarker` | C# CLI that calls NerdGraph `changeTrackingCreateDeployment` with version, commit and user. `--dry-run` prints the request. Treats a 200 with an `errors` array as a failure. 4 xUnit tests against a local fake server. |

## Design choices

- **One YAML for alerts.** The audit and Terraform read the same file, so a rule that passes the audit is the rule that ships. Cost: the YAML only covers static NRQL conditions. Anomaly and baseline conditions would need a second shape.
- **Audit before apply.** A bad alert is cheap to catch in review and expensive at 3 a.m. Cost: another gate to keep current. The rules are about noise (windows under 3 minutes flap) and ownership (no runbook, no alert).
- **Deployment marker in C#.** It runs in a release job and fails the job when New Relic rejects the event, so a missing marker is noticed. Cost: a failed marker can fail a release. Make the step non-blocking if the team would rather ship without it.

## What is verified

- **Run here and in CI:** the Ruby audit passes the shipped file and refuses each bad case; the C# tests send the request to a local fake server and check the header, body and error handling; `terraform validate` accepts the configuration.
- **Not done:** no call to a real New Relic account, so the NerdGraph mutation shape and the Terraform provider arguments are checked against documentation and the validator, not against a live API. No entity GUIDs exist for the example app. No dashboards, no agent instrumentation.

MIT licensed.
