Feature: Report embed marker showcase
  Exercises every Ohirok.Reporting Embed* helper (D9 verification). Deliberately tagged with neither a Behavior
  nor a Suite filter option, so it also proves the reporting.filters "Other" bucket engages correctly for both
  configured groups.

  @report_demo
  Scenario: All embed helpers render in the generated report
    Given an image URL is embedded
    And an error callout is embedded
    And a warning callout is embedded
    And an info callout is embedded
    And raw HTML is embedded
