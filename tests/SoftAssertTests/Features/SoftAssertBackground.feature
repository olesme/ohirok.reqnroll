@swallow_flush
Feature: Soft Assert in Background

Background:
    #softassert
    When the step throws InvalidOperationException

Scenario: Soft failure in a Background step does not interrupt the scenario
    Then the next step still executes
    And the soft state should have failures
