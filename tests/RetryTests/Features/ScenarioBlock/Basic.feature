@scenario_block
Feature: Scenario Block Retry

Background:
    Given counter is incremented <TestId_A>

@retryblock:3
Scenario: State persists across retries
    When counter is set to 5 <TestId_B>
    And operation fails 2 times <TestId_B>
    Then counter should be 5 <TestId_B>
    And counter should be 3 <TestId_A>

@retryblock:3 @assert_clean_context
Scenario: Different steps fail on different attempts
    When operation fails at 1 attempt <TestId_C>
    And operation fails at 2 attempt <TestId_C>
    Then the block attempt number is 3

@retry:3 @backup
Scenario: Scenario context backup works
    When 'backup' key added to scenario context on 1 attempt
    And operation fails 2 times <TestId_D>
    Then the test attempt number is 3
    And 'backup' key persists in scenario context with value '1 attempt'

# The attempt count comes from the tag, not from the configured default (3).
@retryblock:5
Scenario: Scenario block honours an attempt count above the default
    When operation fails 4 times <TestId_E>
    Then verify attempt count is 5 <TestId_E>
