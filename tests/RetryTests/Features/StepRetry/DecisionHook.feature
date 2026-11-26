@step_retry @step_retry_decision
Feature: Step Retry Decision Hook

@condition:TimeoutException @force_retry_step
Scenario: Force retry overrides a non-matching condition for a step
    #retry:2
    When operation throws invalid <TestId_A>
    Then verify step succeeded <TestId_A>
