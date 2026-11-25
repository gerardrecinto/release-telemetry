require_relative "../lib/alert_audit"

RSpec.describe AlertAudit do
  good = {
    "name" => "orders-api error rate above 5 percent",
    "nrql" => "SELECT percentage(count(*), WHERE error IS true) FROM Transaction WHERE appName = 'orders-api'",
    "threshold" => 5, "duration_minutes" => 5,
    "runbook_url" => "https://example.com/runbook"
  }
  def audit(*conds) = AlertAudit.audit({ "policy" => "p", "conditions" => conds }).map(&:rule)

  it "passes the shipped alert file" do
    path = File.expand_path("../../alerts/alerts.yml", __dir__)
    expect(AlertAudit.audit_file(path)).to be_empty
  end

  it("accepts a good condition") { expect(audit(good)).to be_empty }
  it("A3 refuses a missing or http runbook") do
    expect(audit(good.merge("runbook_url" => nil))).to eq(["A3"])
    expect(audit(good.merge("runbook_url" => "http://example.com"))).to eq(["A3"])
  end
  it("A5 refuses SELECT *") { expect(audit(good.merge("nrql" => "SELECT * FROM Transaction WHERE appName = 'a'"))).to include("A5") }
  it("A6 refuses a query that covers every service") do
    expect(audit(good.merge("nrql" => "SELECT count(*) FROM Transaction"))).to eq(["A6"])
  end
  it("A7 refuses a threshold that is a string") { expect(audit(good.merge("threshold" => "5"))).to eq(["A7"]) }
  it("A8 refuses a 1 minute window that would flap") { expect(audit(good.merge("duration_minutes" => 1))).to eq(["A8"]) }
  it("A9 refuses an unknown operator") { expect(audit(good.merge("operator" => "NEAR"))).to eq(["A9"]) }
  it("A2 refuses a repeated name") { expect(audit(good, good)).to eq(["A2"]) }
  it("A0 refuses a file with no policy") do
    expect(AlertAudit.audit({ "conditions" => [] }).map(&:rule)).to eq(["A0"])
  end
end
