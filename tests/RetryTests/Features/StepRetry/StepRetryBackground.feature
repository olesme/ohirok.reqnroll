@step_retry
Feature: Step Retry in Background

Background:
    #retry:3
    When operation fails 2 times <TestId_A>

Scenario: The background step is retried
    Then verify step succeeded <TestId_A>
