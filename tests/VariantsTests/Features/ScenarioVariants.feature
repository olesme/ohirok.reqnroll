Feature: Scenario-level variants

Background:
	Given the background ran

@Browser:Chrome @Browser:Firefox
Scenario: One test per browser
	Then the variant Browser is one of Chrome, Firefox
	And the step argument "<Browser>" is the variant Browser

@Browser:Chrome @Browser:Firefox @OS:Linux @OS:Windows
Scenario: Two keys give every combination
	Then the variant Browser is one of Chrome, Firefox
	And the variant OS is one of Linux, Windows

@browser:Edge
Scenario: Keys match without regard to case
	Then the variant Browser is one of Edge

@smoke @Browser:Chrome @Browser:Firefox
Scenario Outline: An outline runs every row in every variant
	Then the variant Browser is one of Chrome, Firefox
	And the step argument "<amount>" is the variant amount

	@prices
	Examples: Prices
		| amount |
		| 10     |
		| 20     |

@Browser:Chrome @Browser:Firefox
Scenario Outline: A column with the key's name takes its values from the table
	Then the variant Browser is one of Safari, Opera

	Examples:
		| Browser |
		| Safari  |
		| Opera   |

Scenario: A scenario without variant tags is unchanged
	Then the variant Browser is not set

@issue:123
Scenario: Tags of keys that are not configured are left alone
	Then the variant Browser is not set
