Feature: The skipped tests carry their reasons

    The reason is read from the compiled test method: the runner shows the same text, as the ignore message of NUnit and
    MSTest, TUnit's skip reason and xUnit's Skip.

Scenario: Each skipped test has its reason
    Then the test "A scenario with an alias is skipped" is skipped with the reason "Known product defect"
    And the test "An alias without a reason gives its name" is skipped with the reason "ignore:flaky"
    And the test "An alias next to ignore keeps its reason" is skipped with the reason "Known product defect"
    And the test "Every example of an outline is skipped" is skipped with the reason "Not implemented yet"
    And the test "A scenario in a rule with an alias is skipped" is skipped with the reason "Not implemented yet"
    And the test "A scenario in a feature with an alias is skipped" is skipped with the reason "Known product defect"
    And the test "A tag that is not configured skips nothing" is not skipped
