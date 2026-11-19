Feature: Soft Assert Basics

@swallow_flush
Scenario: Step continues after a captured soft failure, hooks fire
    #softassert
    When the step throws InvalidOperationException
    Then the next step still executes
    And the soft state should have failures
    And soft_failure_fired hook fires 1 time(s)

@swallow_flush
@tag_filtered
Scenario: Tag-filtered soft-assert hooks fire only for matching scenarios
    #softassert
    When the step throws InvalidOperationException
    Then the next step still executes
    And soft_failure_tag_filtered_fired hook fires 1 time(s)

@swallow_flush
Scenario: Exception-type filtered hook fires for a matching exception type
    #softassert
    When the step throws TimeoutException
    Then the next step still executes
    And soft_failure_timeout_fired hook fires 1 time(s)
