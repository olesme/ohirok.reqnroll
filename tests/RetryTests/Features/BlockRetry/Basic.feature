@block_retry
Feature: Block Retry Basic

Background:
    Given counter is incremented <TestId_A>

Scenario: Single block retry
    #retryblock:2
    When operation fails 2 times <TestId_B>
    Then success
    #end
    And counter should be 1 <TestId_A>

Scenario: Single block retry without end mark
    #retryblock:2
    When operation fails 2 times <TestId_C>
    Then success

Scenario: Block with multiple steps
    #retryblock:2
    When operation fails 1 time <TestId_D1>
    And operation fails 1 time <TestId_D2>
    #end
    Then verify both steps succeeded <TestId_D1> <TestId_D2>
    And counter should be 1 <TestId_A>

Scenario: Block fails at different steps on different attempts
    #retryblock:2
    When operation fails at 1 attempt <TestId_E>
    And operation fails at 2 attempt <TestId_E>
    Then the block attempt number is 3

# The attempt count comes from the hashtag, not from the configured default (3).
Scenario: Step block honours an attempt count above the default
    #retryblock:3
    When operation fails 3 times <TestId_Z>
    #end
    Then verify attempt count is 4 <TestId_Z>
