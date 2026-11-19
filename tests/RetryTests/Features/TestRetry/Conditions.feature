@test_retry
Feature: Test Retry Conditions

@retry:3 @condition:TimeoutException
Scenario: Positive condition matches
    When operation throws timeout <TestId_A>
    Then verify attempt count is 3 <TestId_A>

@retry:3 @condition:!ArgumentException
Scenario: Negative condition excludes retry on ArgumentException
    When operation throws invalid <TestId_B>
    Then verify attempt count is 3 <TestId_B>

@retry:3 @condition:*Exception
Scenario: Wildcard matches all exceptions
    When operation throws custom <TestId_C>
    Then verify attempt count is 3 <TestId_C>

@retry:3 @conditions[Timeout*,Invalid*]
Scenario: Multiple conditions
    When operation throws timeout or invalid <TestId_D>
    Then verify attempt count is 3 <TestId_D>
