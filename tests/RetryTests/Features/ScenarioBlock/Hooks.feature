@retryblock:2 @print_block_loop_ended
Feature: Scenario Block Retry Hooks

Scenario: Block hooks execute correctly
    When operation fails 2 times <TestId_A>
    Then success
    And block_loop_started hook fires 1 time
    And block_restarted hook fires 2 times
    And block_loop_ended hook fires 0 times
