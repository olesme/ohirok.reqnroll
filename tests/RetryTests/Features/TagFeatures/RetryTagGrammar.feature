@test_retry @tag_grammar
Feature: Retry tag grammar

# Ordinary tags that merely START with a condition keyword (on, if, when, for, cond, except)
# must not become retry conditions. Bug signature: condition "line" blocks the retry, attempts = 1 -> red.
@retry:3 @online @only @format @iffy @whenever
Scenario: Ordinary tags starting with a condition keyword do not affect retry
    When operation fails 2 times <TestId_A>
    Then verify attempt count is 3 <TestId_A>

# Fluent form: a number followed by a word describes itself. Retry itself is still activated by @retry.
@retry @3times
Scenario: Fluent attempts tag
    When operation fails 2 times <TestId_B>
    Then verify attempt count is 3 <TestId_B>

# Value after a delimiter still works
@retry:3 @on:InvalidOperationException
Scenario: Condition after a delimiter
    When operation fails 2 times <TestId_C>
    Then verify attempt count is 3 <TestId_C>
