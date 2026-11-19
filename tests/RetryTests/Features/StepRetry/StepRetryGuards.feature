@step_retry
Feature: Step Retry Guards

# Step retry must work when BeforeStep/AfterStep hooks exist (hooks go through the same binding invoker as steps)
@withhook
Scenario: Step retry works when BeforeStep/AfterStep hooks exist
    #retry:3
    When operation fails 2 times <TestId_A>
    Then verify step succeeded <TestId_A>

Scenario: Control, same without any hook
    #retry:3
    When operation fails 2 times <TestId_A>
    Then verify step succeeded <TestId_A>
