# Checks alert definitions before Terraform turns them into New Relic conditions. Each rule has an id so a failure can be cited.
require "yaml"
require "uri"

module AlertAudit
  Finding = Struct.new(:condition, :rule, :message)

  def self.audit(doc)
    conditions = doc.fetch("conditions", [])
    findings = []
    seen = {}
    conditions.each do |c|
      name = c["name"].to_s
      add = ->(rule, msg) { findings << Finding.new(name, rule, msg) }

      add.("A1", "name is missing") if name.strip.empty?
      add.("A2", "name is used twice") if seen[name]
      seen[name] = true

      uri = (URI.parse(c["runbook_url"].to_s) rescue nil)
      add.("A3", "runbook_url must be an https link") unless uri.is_a?(URI::HTTPS) && uri.host

      nrql = c["nrql"].to_s
      add.("A4", "query must be SELECT ... FROM ...") unless nrql =~ /\ASELECT\s.+\sFROM\s/i
      add.("A5", "query must not use SELECT *") if nrql =~ /\ASELECT\s+\*/i
      add.("A6", "query must filter on appName or entityGuid so it cannot cover every service") unless nrql =~ /\bWHERE\b.*\b(appName|entityGuid)\b/i

      add.("A7", "threshold must be a number") unless c["threshold"].is_a?(Numeric)
      d = c["duration_minutes"]
      add.("A8", "duration_minutes must be 3 to 120") unless d.is_a?(Integer) && d.between?(3, 120)
      op = c.fetch("operator", "ABOVE")
      add.("A9", "operator must be ABOVE, BELOW or BELOW_OR_EQUALS") unless %w[ABOVE BELOW BELOW_OR_EQUALS].include?(op)
    end
    findings << Finding.new("(file)", "A0", "policy name is missing") if doc["policy"].to_s.strip.empty?
    findings
  end

  def self.audit_file(path)
    audit(YAML.safe_load(File.read(path)))
  end
end
