Feature: Step Hashtags

Scenario: Hashtags available in step definition
    #first-tag
    #second-tag
    Given step with hashtags validates "first-tag,second-tag"
    Given step without hashtags validates none

Scenario: Hashtags available in hooks
    #hook-test
    When step triggers hook validation

Scenario: A # inside a word is not a hashtag
    # see issue#123 and http://example.com/#anchor
    Then the step has no hashtags
	
Scenario: Hashtag with a quote and a backslash is generated safely
    #softassert"x
    Then this step always executes

Scenario: Step hashtag with quote and backslash is passed through
    # note #note:a"b\c
    Then this step always executes