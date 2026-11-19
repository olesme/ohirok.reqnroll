@swallow_flush
Feature: Soft Assert with BeforeStep hooks

# Bug signature: the soft step FAILS and the next step is SKIPPED when any BeforeStep/AfterStep hook is present
@withhook
Scenario: Soft failure does not interrupt the scenario when step hooks exist
    #softassert
    When the step throws InvalidOperationException
    Then the next step still executes
    And the next step should have executed
    And the soft state should have failures
