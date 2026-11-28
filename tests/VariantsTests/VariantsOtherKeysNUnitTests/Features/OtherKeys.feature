Feature: Variant keys of another project

@Browser:Chrome @Browser:Firefox @Region:EU @Region:US
Scenario: Only this project's keys are variants
	Then the variant Region is one of EU, US
	And the variant Browser is not set
