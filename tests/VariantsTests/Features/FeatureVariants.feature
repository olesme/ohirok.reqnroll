@Browser:Chrome @Browser:Firefox
Feature: Feature- and rule-level variants

Scenario: A scenario inherits the feature's variants
	Then the variant Browser is one of Chrome, Firefox

@Browser:Edge
Scenario: A scenario's own variants replace the feature's
	Then the variant Browser is one of Edge

@OS:Linux
Scenario: A scenario adds a key and keeps the feature's other key
	Then the variant Browser is one of Chrome, Firefox
	And the variant OS is one of Linux

@Browser:Safari
Rule: A rule's variants replace the feature's

	Scenario: A scenario in the rule uses the rule's variants
		Then the variant Browser is one of Safari

	@Browser:Opera
	Scenario: A scenario in the rule can still replace them
		Then the variant Browser is one of Opera
