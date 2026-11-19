@isolation
Feature: No Cross-Test Contamination

@retry:2
Scenario: Test A with retry
    When operation fails 1 time <TestId_ContamA>
    Then verify attempt count is 2 <TestId_ContamA>

Scenario: Test B without retry should not inherit context
    When operation fails 0 times <TestId_ContamB>
    Then verify attempt count is 1 <TestId_ContamB>
    And verify no retry context exists

@retry:3
Scenario: Test C with different retry count
    When operation fails 2 times <TestId_ContamC>
    Then verify attempt count is 3 <TestId_ContamC>

Scenario: Test D cleanup verification
    When check thread local is clean
    And check scenario context is clean
    And check thread local is clean
