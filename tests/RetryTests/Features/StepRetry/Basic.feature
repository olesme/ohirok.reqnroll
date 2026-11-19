@step_retry
Feature: Step Retry Basic

Scenario: Single step retry
    #retry:3
    When operation fails 2 times <TestId_A>
    Then verify step succeeded <TestId_A>

Scenario: Multiple independent step retries
    #retry:2
    When operation fails 1 time <TestId_B1>
    #retry:3
    When operation fails 2 times <TestId_B2>
    Then verify both steps succeeded <TestId_B1> <TestId_B2>

Scenario: Step retry with condition
    #retry:3 #condition:TimeoutException
    When operation throws timeout <TestId_C>
    Then verify step succeeded <TestId_C>

Scenario: Step retry with timeout
    #retry:3 #timeout:2s
    When operation sleeps 200ms and fails 2 times <TestId_D>
    Then verify step succeeded <TestId_D>
