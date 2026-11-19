Feature: Ignore Step

Scenario: Mid-scenario step skip does not skip the whole scenario
    Given this step always executes
    #ignore
    Given this step should never execute
    Given this step always executes

Scenario: Embedding a real image attachment
    Given a real image is embedded
