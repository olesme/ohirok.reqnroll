Feature: Soft-assert auto-discovery smoke test

  Scenario: Soft-tagged step passes, OnSoftFlush hook does not fire with zero captured failures
    Given the page has loaded
    #softassert
    Then the header is correct
    Then the page is still usable
    Then the flush hook recorded zero failures
