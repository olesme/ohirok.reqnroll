@named_policy
Feature: Named retry policies

    A tag such as @retry:flaky turns retry on, and a [BeforeRetryLoop("@retry:flaky")] hook gives the name its settings
    (RetryTests.Shared/Hooks/RetryPolicies.cs). Without the hook these would run with the configured default attempts,
    so failing 4 times passes only with the policy's 5 attempts.

Background:
    Given test storage is initialized

@retry:flaky
Scenario: Scenario retry with a named policy
    When operation fails 4 times <TestId_NP_A>
    Then verify attempt count is 5 <TestId_NP_A>
    And verify delay was at least 200ms <TestId_NP_A>

Scenario: Step retry with a named policy
    #retry:flaky
    When operation fails 4 times <TestId_NP_B>
    Then verify step succeeded <TestId_NP_B>

Scenario: Block retry with a named policy
    #retryblock:flaky
    When operation fails 4 times <TestId_NP_C>
    Then success
    #end
    Then verify attempt count is 5 <TestId_NP_C>

@retry:payments
Scenario: The policy's conditions decide what is retried
    When operation throws timeout <TestId_NP_D>
    Then verify attempt count is 3 <TestId_NP_D>
