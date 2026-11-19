@step_retry @step_retry_hooks
Feature: Step Retry Hooks

Scenario: Step hooks execute per step
    #retry:2
    When operation fails 1 time <TestId_A>
    Then step_loop_started hook fires 1 time
    And step_restarted hook fires 1 time
    And step_loop_ended hook fires 1 time
    #retry:3
    When operation fails 2 times <TestId_B>
    Then step_loop_started hook fires 2 times
    And step_restarted hook fires 3 times
    And step_loop_ended hook fires 2 times
