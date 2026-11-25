terraform {
  required_version = ">= 1.5"
  required_providers {
    newrelic = {
      source  = "newrelic/newrelic"
      version = "~> 3.50"
    }
  }
}

variable "account_id" {
  type = number
}

variable "api_key" {
  type      = string
  sensitive = true
}

variable "region" {
  type    = string
  default = "US"
}

provider "newrelic" {
  account_id = var.account_id
  api_key    = var.api_key
  region     = var.region
}

locals {
  alerts = yamldecode(file("${path.module}/../alerts/alerts.yml"))
}

resource "newrelic_alert_policy" "this" {
  name = local.alerts.policy
}

resource "newrelic_nrql_alert_condition" "this" {
  for_each = { for c in local.alerts.conditions : c.name => c }

  policy_id   = newrelic_alert_policy.this.id
  name        = each.value.name
  type        = "static"
  enabled     = true
  runbook_url = each.value.runbook_url

  nrql {
    query = each.value.nrql
  }

  critical {
    operator              = try(each.value.operator, "ABOVE")
    threshold             = each.value.threshold
    threshold_duration    = each.value.duration_minutes * 60
    threshold_occurrences = "ALL"
  }
}
