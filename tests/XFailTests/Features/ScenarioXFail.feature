Feature: Scenario-level expected and allowed failures
  Tags xfail / afail on a scenario judge the whole test method - Before hooks, steps and anything a plugin
  injects - instead of one step. Meant for failures that no step owns; for a step, prefer the #xfail hashtag.

  @xfail:InvalidOperationException
  Scenario: A failing step is the expected failure of the scenario
    Given step that throws InvalidOperationException
    Then this step should never execute

  @xfail @fails-in-before-hook
  Scenario: A failing Before hook is the expected failure of the scenario
    Given step that succeeds

  @afail:InvalidOperationException
  Scenario: An allowed failure that happens
    Given step that throws InvalidOperationException

  @afail
  Scenario: An allowed failure that does not happen
    Given step that succeeds

  @xfail:*Exception,!ArgumentException
  Rule: The tag of a rule applies to its scenarios

    Scenario: A scenario of a rule tagged @xfail
      Given step that throws InvalidOperationException
