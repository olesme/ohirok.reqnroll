@block_retry @block_retry_hooks
Feature: Block Retry Hooks

Scenario: Block hooks execute correctly
    #retryblock:2
    When operation fails 2 times <TestId_A>
    Then success
    #end
    And block_loop_started hook fires 1 time
    And block_restarted hook fires 2 times
    And block_loop_ended hook fires 1 time
