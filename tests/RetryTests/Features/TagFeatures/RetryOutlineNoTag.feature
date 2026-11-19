@test_retry
Feature: Retry lifecycle only for rows that have a retry tag

# One Examples block has @retry:2, so the generated method is wrapped for every row.
# The row without a retry tag must run as a plain test: no retry loop, no loop hooks.
Scenario Outline: Retry lifecycle exists only for tagged rows <id>
    When operation fails 0 times <TestId_<id>>
    Then test_loop_started hook fires <loops> times

    @retry:2
    Examples: tagged
        | id | loops |
        | t  | 1     |

    Examples: untagged
        | id | loops |
        | u  | 0     |
