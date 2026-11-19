@parallel_safe
Feature: Parallel Test Safety

@retry:2
Scenario Outline: Parallel test <id>
    When operation fails 1 times <TestId_<id>>
    Then verify attempt count is 2 <TestId_<id>>
    And verify no cross-thread contamination <id>

    Examples:
    | id  |
    | P01 |
    | P02 |
    | P03 |
    | P04 |
    | P05 |
    | P06 |
    | P07 |
    | P08 |
    | P09 |
    | P10 |

Scenario Outline: Parallel test <id> with block and step retry
#retry:2
    When operation fails 1 times <TestId_<id>>
#retryblock:3
    Then verify attempt count is 2 <TestId_<id>>

    And verify no cross-thread contamination <id>

    Examples:
    | id  |
    | P01 |
    | P02 |
