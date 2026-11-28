Feature: Ignore tags with reasons

    reqnroll.json: @ignore:bug = "Known product defect", @wip = "Not implemented yet", @ignore:flaky without a reason.
    Every skipped scenario here fails if it runs; Reasons.feature checks the reason each one is skipped with.

@ignore:bug
Scenario: A scenario with an alias is skipped
    When this step must not run

@ignore:flaky
Scenario: An alias without a reason gives its name
    When this step must not run

@ignore:bug @ignore
Scenario: An alias next to ignore keeps its reason
    When this step must not run

@wip
Scenario Outline: Every example of an outline is skipped
    When this step must not run <n>

Examples:
    | n |
    | 1 |
    | 2 |

@ignore:other
Scenario: A tag that is not configured skips nothing
    Then this scenario ran

@wip
Rule: A rule with an alias

    Scenario: A scenario in a rule with an alias is skipped
        When this step must not run
